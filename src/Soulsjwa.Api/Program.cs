using System.Reflection;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;
using Serilog;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Common.Extensions;
using Soulsjwa.Api.Diagnostics;
using Soulsjwa.Api.Extensions;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Features.TwitchExtension;
using Soulsjwa.Api.Features.TwitchExtension.Services;
using Soulsjwa.Api.Infrastructure.Auth;
using Soulsjwa.Api.Infrastructure.Data;
using Soulsjwa.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();

// Conservative global request-body cap: no legitimate payload approaches 1 MiB
// except image uploads, which override it explicitly (see
// MediaEndpoint.MaxUploadRequestBytes).
const int GlobalMaxRequestBytes = 1024 * 1024;
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = GlobalMaxRequestBytes);

// Forwarded headers (X-Forwarded-For / X-Forwarded-Proto). Behind a
// TLS-terminating reverse proxy, without this the rate limiter partitions every
// request by the proxy's IP, making the per-IP limit useless.
//
// The limit must be 1 (the immediate proxy only). Without it any client could
// spoof their own IP by injecting a forged X-Forwarded-For header — the
// framework would accept the leftmost value as the "real" client IP, letting an
// attacker rotate IPs at will and trivially defeat rate limiting.
// KnownProxies/KnownNetworks are intentionally cleared so the allow-list
// applies; operators with a non-trivial topology should configure these
// explicitly via env vars.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

// DataProtection backs the signed OAuth-state cookie below. The default
// file-system key ring suits a single instance; multi-instance deployments must
// persist it to a shared store (Redis, blob, …) so every node can decrypt
// cookies issued by any other node.
builder.Services.AddDataProtection();

// OAuth state travels in a short-lived signed+encrypted cookie (see
// OAuthStateCookie), so the callback needs no server-side session and works
// behind a load balancer without sticky sessions.
builder.Services.AddSingleton<OAuthStateCookie>();

// Retention: deletes expired refresh tokens (past a grace window) and,
// only when explicitly configured, old audit rows. See Retention:* settings.
builder.Services.AddHostedService<RetentionService>();

// Audit log writer (centralised). Stateless so a singleton is fine.
builder.Services.AddSingleton<Soulsjwa.Api.Features.Audits.Services.IAuditService,
    Soulsjwa.Api.Features.Audits.Services.AuditService>();

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<Soulsjwa.Api.Features.Media.Services.IMediaStore,
    Soulsjwa.Api.Features.Media.Services.MediaStore>();

// Twitch is the login flow's only external dependency, so its HttpClient gets a
// resilience pipeline: HttpClient's own defaults are a 100s timeout with no
// retry or circuit breaker, so a hanging Twitch endpoint would tie up a request
// thread for 100s per attempt and keep being hammered.
var twitchClientTimeoutSeconds = ConfigurationValues.ReadPositiveInt(builder.Configuration, "Twitch:ClientTimeoutSeconds", TwitchClientOptions.DefaultClientTimeoutSeconds);
var twitchRetryAttempts = ConfigurationValues.ReadPositiveInt(builder.Configuration, "Twitch:RetryAttempts", TwitchClientOptions.DefaultRetryAttempts);
var twitchAttemptTimeoutSeconds = ConfigurationValues.ReadPositiveInt(builder.Configuration, "Twitch:AttemptTimeoutSeconds", TwitchClientOptions.DefaultAttemptTimeoutSeconds);
builder.Services.AddHttpClient<TwitchAuthService>()
    .AddStandardResilienceHandler(options => TwitchClientOptions.ConfigureResilience(
        options, twitchRetryAttempts, twitchAttemptTimeoutSeconds, twitchClientTimeoutSeconds));

// --migrate: apply migrations + seed, then exit without ever serving a request
// (init container / the Compose `migrate` service / a pre-deploy step). Read
// here, ahead of the checks below, because that process needs only a
// connection string: demanding the signing secret and Twitch credentials from
// a job that never signs a token or talks to Twitch made the Compose migrate
// service fail on any host where those were not yet filled in.
var migrateOnly = args.Contains("--migrate");

// The Twitch extension backend is optional (see TwitchExtensionOptions): with
// nothing configured its routes are simply not mapped. Read once here, since
// the bearer scheme below needs the signing keys before the container exists.
var twitchExtensionOptions = migrateOnly
    ? TwitchExtensionOptions.Disabled
    : TwitchExtensionOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(twitchExtensionOptions);
builder.Services.AddSingleton<TwitchExtensionBundle>();
if (twitchExtensionOptions.CanPush)
{
    // Push pings ride on Twitch's Extension PubSub. Same resilience pipeline
    // as the login client (a hanging Twitch must never pin the notifier); the
    // POST is not retried, so a lost ping is simply covered by the next poll.
    builder.Services.AddHttpClient(TwitchExtensionPushNotifier.HttpClientName)
        .AddStandardResilienceHandler(options => TwitchClientOptions.ConfigureResilience(
            options, twitchRetryAttempts, twitchAttemptTimeoutSeconds, twitchClientTimeoutSeconds));
    builder.Services.AddSingleton<TwitchExtensionPushNotifier>();
    builder.Services.AddSingleton<ITwitchExtensionPushNotifier>(sp => sp.GetRequiredService<TwitchExtensionPushNotifier>());
    builder.Services.AddHostedService(sp => sp.GetRequiredService<TwitchExtensionPushNotifier>());
}
else
{
    builder.Services.AddSingleton<ITwitchExtensionPushNotifier, NullTwitchExtensionPushNotifier>();
}

if (!migrateOnly)
{
    // Validation parameters are built once via JwtTokenService.BuildValidationParameters
    // so the JwtBearer middleware and JwtTokenService cannot drift out of sync.
    var jwtSecretError = JwtSecretValidator.Validate(builder.Configuration["Jwt:Secret"], builder.Environment.IsDevelopment());
    if (jwtSecretError is not null)
        throw new InvalidOperationException(jwtSecretError);

    // Fail fast on missing Twitch credentials: readiness only checks the database,
    // so a misconfigured deployment would otherwise look healthy until the first
    // real sign-in attempt.
    foreach (var twitchKey in new[] { "Twitch:ClientId", "Twitch:ClientSecret", "Twitch:RedirectUri" })
    {
        if (string.IsNullOrWhiteSpace(builder.Configuration[twitchKey]))
            throw new InvalidOperationException($"{twitchKey} must be configured.");
    }
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Smart";
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddPolicyScheme("Smart", "Smart Auth", options =>
{
    options.ForwardDefaultSelector = ctx =>
    {
        if (ctx.Request.Headers.ContainsKey(ApiKeyAuthHandler.HeaderName))
            return ApiKeyAuthHandler.SchemeName;
        return JwtBearerDefaults.AuthenticationScheme;
    };
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = JwtTokenService.BuildValidationParameters(builder.Configuration);
    options.Events = new JwtBearerEvents
    {
        // Access tokens live 15 minutes, so without this check a de-allowlisted user
        // keeps working until their token expires. Costs one indexed primary-key
        // lookup per authenticated JWT request — the same lookup the API-key path
        // already does — in exchange for immediate deprovisioning.
        OnTokenValidated = async context =>
        {
            // The "sub" claim this token was issued with arrives here as
            // ClaimTypes.NameIdentifier, not the literal "sub": JwtBearerHandler
            // applies the standard inbound claim-type mapping before
            // OnTokenValidated runs. Going through the same helper as every other
            // authenticated endpoint keeps that consistent and reuses its
            // malformed-claim handling.
            if (context.Principal is null || !EventOwnership.TryGetUserId(context.Principal, out var userId))
            {
                context.Fail("Invalid token subject.");
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var isAllowlisted = await db.Users
                .Where(u => u.Id == userId)
                .Select(u => (bool?)u.IsAllowlisted)
                .FirstOrDefaultAsync();

            if (isAllowlisted is not true)
                context.Fail("User is no longer permitted to sign in.");
        },
    };
})
.AddScheme<ApiKeyAuthOptions, ApiKeyAuthHandler>(ApiKeyAuthHandler.SchemeName, _ => { })
// Tokens Twitch issues to viewers of the extension. A separate scheme that
// only the extension route group opts into (see TwitchExtensionAuth), so it
// can never authenticate a request to any other route. Inbound claim mapping
// is off: the claims are read under the names Twitch gives them.
.AddJwtBearer(TwitchExtensionAuth.SchemeName, options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = TwitchExtensionAuth.BuildValidationParameters(twitchExtensionOptions);
});

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(TwitchExtensionAuth.ViewerPolicy, policy => policy
        .AddAuthenticationSchemes(TwitchExtensionAuth.SchemeName)
        .RequireAuthenticatedUser()
        .RequireClaim(TwitchExtensionAuth.Claims.ChannelId))
    .AddPolicy(TwitchExtensionAuth.BroadcasterPolicy, policy => policy
        .AddAuthenticationSchemes(TwitchExtensionAuth.SchemeName)
        .RequireAuthenticatedUser()
        .RequireClaim(TwitchExtensionAuth.Claims.ChannelId)
        .RequireRole(TwitchExtensionAuth.Roles.Broadcaster));

// Rate limiting. Every budget below is configurable, with today's values as defaults.
var globalPerUserPermitLimit = ConfigurationValues.ReadPositiveInt(builder.Configuration, "RateLimits:GlobalPerUserPerMinute", RateLimitPolicies.DefaultGlobalPerUserPermitLimit);
var globalPerIpPermitLimit = ConfigurationValues.ReadPositiveInt(builder.Configuration, "RateLimits:GlobalPerIpPerMinute", RateLimitPolicies.DefaultGlobalPerIpPermitLimit);
var authPermitLimit = ConfigurationValues.ReadPositiveInt(builder.Configuration, "RateLimits:AuthPerIpPerMinute", RateLimitPolicies.DefaultAuthPermitLimit);
var connectorPermitLimit = ConfigurationValues.ReadPositiveInt(builder.Configuration, "RateLimits:ConnectorPerMinute", RateLimitPolicies.DefaultConnectorPermitLimit);
var twitchExtensionPermitLimit = ConfigurationValues.ReadPositiveInt(builder.Configuration, "RateLimits:TwitchExtensionPerViewerPerMinute", RateLimitPolicies.DefaultTwitchExtensionPermitLimit);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global fallback: partition by authenticated user ID, or by remote IP for
    // anonymous requests. Endpoints that carry their own named policy are
    // exempted rather than stacked — otherwise the two limiters fight (the
    // global 100/min bucket exhausting before the connector's own 120/min
    // policy ever could) and a user's browser traffic shares a bucket with
    // their own connector's submissions.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        if (ctx.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>() is not null)
            return RateLimitPartition.GetNoLimiter(RateLimitPolicies.ExemptFromGlobalLimiterPartition);

        var userId = ctx.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                $"user_{userId}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = globalPerUserPermitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
        }

        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? RateLimitPolicies.UnknownIpPartition;
        return RateLimitPartition.GetFixedWindowLimiter(
            $"ip_{ip}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = globalPerIpPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
    });

    // Auth endpoints: strict anti-brute-force limit, partitioned per client IP
    // (post-proxy, per ForwardLimit = 1 above) so one abusive host cannot deny
    // login/refresh to every other user.
    options.AddPolicy(RateLimitPolicies.Auth, ctx =>
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? RateLimitPolicies.UnknownIpPartition;
        return RateLimitPartition.GetFixedWindowLimiter(
            $"auth_{ip}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
    });

    // Connector endpoints: more permissive to avoid blocking frequent game-state submissions.
    options.AddPolicy(RateLimitPolicies.Connector, ctx =>
    {
        var userId = ctx.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? RateLimitPolicies.AnonymousUserPartition;
        return RateLimitPartition.GetFixedWindowLimiter(
            $"connector_{userId}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = connectorPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 2,
            });
    });

    // Twitch extension: every viewer of a channel polls, so partition per
    // viewer (the opaque id in the Twitch-issued token) rather than per IP —
    // a shared NAT full of viewers must not be one bucket. Anonymous calls
    // (only the status probe) fall back to the IP.
    options.AddPolicy(RateLimitPolicies.TwitchExtension, ctx =>
    {
        var viewer = ctx.User is null ? null : TwitchExtensionAuth.GetOpaqueUserId(ctx.User);
        var key = viewer is not null
            ? $"twitch_viewer_{viewer}"
            : $"twitch_ip_{ctx.Connection.RemoteIpAddress?.ToString() ?? RateLimitPolicies.UnknownIpPartition}";
        return RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = twitchExtensionPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
    });

    // Retry-After lets a throttled client (the connector especially) back off
    // instead of guessing; the log makes throttling observable.
    options.OnRejected = (context, ct) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

        var policy = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
            ?? "global";
        var userId = context.HttpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var caller = !string.IsNullOrEmpty(userId)
            ? $"user_{userId}"
            : $"ip_{context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? RateLimitPolicies.UnknownIpPartition}";

        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.RateLimitRejected(caller, context.HttpContext.Request.Method, context.HttpContext.Request.Path, policy);

        return ValueTask.CompletedTask;
    };
});

// CORS for React dev server
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var frontendUrl = builder.Configuration["Frontend:Url"] ?? "http://localhost:5173";
        policy.WithOrigins(frontendUrl)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              // ETag isn't on the CORS default-exposed header list, so without
              // this a cross-origin frontend can't read it — and therefore
              // can't send If-Match back.
              .WithExposedHeaders("ETag");
    });

    // The Twitch-hosted extension front end runs on Twitch's CDN origin (plus
    // a local-test origin while developing). No credentials: it authenticates
    // with a bearer token, never a cookie. Always registered — with no origins
    // when the feature is off — so the route group's RequireCors resolves.
    options.AddPolicy(CorsPolicies.TwitchExtension, policy =>
    {
        policy.WithOrigins([.. twitchExtensionOptions.AllowedOrigins])
              .WithHeaders(HeaderNames.Authorization, HeaderNames.ContentType, HeaderNames.IfNoneMatch)
              .WithMethods(HttpMethods.Get, HttpMethods.Put)
              .WithExposedHeaders(HeaderNames.ETag);
    });
});

builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Soulsjwa API",
        Version = "v1",
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {access_token}",
    });

    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = ApiKeyAuthHandler.HeaderName,
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = $"Provide an API key in the {ApiKeyAuthHandler.HeaderName} header",
    });

    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer"),
            []
        },
    });

    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("ApiKey"),
            []
        },
    });
});

builder.Services.AddProblemDetails();

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// Output caching for public endpoints (e.g. scoreboard scores).
//
// The default output-cache store is in-memory and per-node, so EvictByTagAsync
// (see CacheTags.Scoreboard call sites) only clears the node it runs on: every
// other node keeps serving a stale scoreboard until its Expire window elapses.
// A shared store (e.g. Redis) is a prerequisite for running more than one
// instance — see docs/deployment.md.
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("Scoreboard", builder =>
        builder.Expire(TimeSpan.FromHours(1))
               .AddPolicy<PerEventScoreboardCachePolicy>());

    // Overlay scoreboard is gated by a per-event token, sent either as the
    // X-Overlay-Token header (preferred) or the ?token= query parameter (OBS
    // browser source fallback) — vary by both so two different tokens, however
    // each is sent, never share a cache entry. The short expiry keeps DB load
    // low while still letting a token revoke take effect within seconds.
    options.AddPolicy("OverlayScoreboard", builder =>
        builder.Expire(TimeSpan.FromSeconds(5))
               .SetVaryByQuery("token")
               .SetVaryByHeader(OverlayToken.HeaderName)
               .AddPolicy<PerEventScoreboardCachePolicy>());

    // Twitch extension viewer reads: one entry per channel, re-enabled for
    // bearer-authenticated requests by the policy itself (see
    // PerTwitchChannelCachePolicy for why that is safe here).
    options.AddPolicy(PerTwitchChannelCachePolicy.PolicyName, builder =>
        builder.Expire(TimeSpan.FromSeconds(5))
               .AddPolicy<PerTwitchChannelCachePolicy>());

    // Global calendar: short expiry absorbs bursts from an anonymous,
    // unauthenticated audience without letting a write go stale for long.
    options.AddPolicy("Calendar", builder =>
        builder.Expire(TimeSpan.FromSeconds(60))
               .SetVaryByQuery("from", "to")
               .Tag(CacheTags.Calendar));

    // Predefined-objective catalog: static data that changes only when an admin
    // adds a template, so a long expiry is safe; eviction on write
    // (CreatePredefined) keeps it from serving stale data.
    options.AddPolicy("PredefinedObjectives", builder =>
        builder.Expire(TimeSpan.FromHours(24))
               .SetVaryByQuery("gameId")
               .AddPolicy<PerGamePredefinedObjectivesCachePolicy>());
});

// Every scoreboard write already evicts the event's cache tag; wrapping the
// store turns that one signal into the Twitch push, whatever store
// AddOutputCache registered (the in-memory default, or a shared one later).
if (twitchExtensionOptions.CanPush)
    builder.Services.DecorateOutputCacheStoreForPush();

var app = builder.Build();

// Host filtering disabled outside local development is a defence-in-depth gap:
// nothing builds an absolute URL from the request host today, so it isn't
// currently exploitable. A warning, not a throw — an operator behind a trusted
// proxy that already filters hosts has a legitimate reason to leave it open.
if (!builder.Environment.IsDevelopment() && builder.Configuration["AllowedHosts"] == "*")
    Log.Warning("AllowedHosts is \"*\" outside Development. Set it to the deployment's real hostname(s) unless a trusted proxy already filters the Host header.");

// Must run before any middleware that reads the remote IP or the request
// scheme — that includes rate limiting and CORS.
app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        diagnosticContext.Set("CorrelationId", httpContext.Items[CorrelationIdMiddleware.HeaderName]);
        diagnosticContext.Set("TraceId", System.Diagnostics.Activity.Current?.TraceId.ToString());
        diagnosticContext.Set("SpanId", System.Diagnostics.Activity.Current?.SpanId.ToString());
    };
});

// Combined with AddProblemDetails() above, this emits an RFC 7807
// `application/problem+json` response and logs the exception. UseStatusCodePages
// does the same for bodyless non-success responses (e.g. a bare 404 / 403).
app.UseExceptionHandler();
app.UseStatusCodePages();

// --migrate (read above): apply migrations + seed then exit.
// APPLY_MIGRATIONS=true or Development: apply migrations + seed then continue running.
var applyMigrations = migrateOnly
    || app.Environment.IsDevelopment()
    || string.Equals(app.Configuration["APPLY_MIGRATIONS"], "true", StringComparison.OrdinalIgnoreCase);

if (applyMigrations)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seederLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("PredefinedObjectiveSeeder");
    db.Database.Migrate();
    await Soulsjwa.Api.Features.Games.Services.PredefinedObjectiveSeeder.SeedAsync(db, seederLogger);
}

if (migrateOnly)
{
    // Return from the entry point with exit code 0 so Docker Compose marks
    // the migrate service as service_completed_successfully and starts the api.
    return;
}

app.UseResponseCompression();

app.UseCors();

// Security headers
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    // Same-origin framing only: the overlay designer previews the overlay
    // route in an iframe of its own page. No other origin may frame anything.
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "accelerometer=(), browsing-topics=(), camera=(), geolocation=(), gyroscope=(), interest-cohort=(), magnetometer=(), microphone=(), payment=(), usb=()";

    // CSP: deny by default, allow same-origin assets and the connect endpoints we use.
    // Swagger UI in development needs 'unsafe-inline' for its bundled scripts/styles.
    // img-src is scoped to self, data: URIs and Twitch's profile-image CDN rather
    // than a blanket https:, since all other images are self-hosted. Add any further
    // Twitch host explicitly — never widen this back to a bare https: scheme.
    if (app.Environment.IsDevelopment())
    {
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: https://static-cdn.jtvnw.net; " +
            "font-src 'self' data:; " +
            "connect-src 'self' http://localhost:5173 ws://localhost:5173; " +
            "frame-ancestors 'self'; " +
            "base-uri 'self'; " +
            "form-action 'self' https://id.twitch.tv";
    }
    else
    {
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: https://static-cdn.jtvnw.net; " +
            "font-src 'self' data:; " +
            "connect-src 'self'; " +
            "frame-ancestors 'self'; " +
            "base-uri 'self'; " +
            "form-action 'self' https://id.twitch.tv";
    }

    await next();
});

// Static files sit ahead of authentication/rate limiting so the dozens of
// code-split JS/CSS chunks a page load pulls in don't eat into the API
// rate-limit budget alongside real requests.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// Must run after UseAuthentication so the global limiter's per-user partition
// (keyed off ClaimTypes.NameIdentifier) can actually see the authenticated
// user instead of always falling back to the stricter per-IP anonymous bucket.
app.UseRateLimiter();

app.UseOutputCache();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// /health      - back-compat aggregate (all checks)
// /health/live - liveness probe: process is up (no checks)
// /health/ready- readiness probe: DB and other "ready" tagged checks pass
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

app.MapEndpoints();

// Any /api/v1/* request that reached routing without matching an endpoint is a
// genuine 404 and must not fall through to the SPA shell below. GET-only on
// purpose: an any-method wildcard would become the sole remaining candidate for
// e.g. DELETE /api/v1/events once method-filtering eliminates that route's own
// endpoints, suppressing ASP.NET Core's 405 in favour of this 404. GET-only
// lets a verb mismatch fall past this (and past the equally GET-only
// MapFallbackToFile below) to the framework's own 405 handling.
app.MapMethods(ApiRoutes.Prefix + "/{**catchAll}", [HttpMethods.Get], () => Results.Problem(
    detail: "Endpoint not found.",
    statusCode: StatusCodes.Status404NotFound));

app.MapFallbackToFile("index.html");

app.Lifetime.ApplicationStarted.Register(() =>
{
    var urls = string.Join(", ", app.Urls);
    Log.Information("Server started, listening on {Urls}", urls);
});

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
