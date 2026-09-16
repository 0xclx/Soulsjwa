using Microsoft.Extensions.Http.Resilience;

namespace Soulsjwa.Api.Common;

/// <summary>
/// Tunables for the Twitch HTTP client's resilience pipeline. <c>Twitch:ClientTimeoutSeconds</c>,
/// <c>Twitch:RetryAttempts</c> and <c>Twitch:AttemptTimeoutSeconds</c> override the
/// matching <c>Default*</c> constants; the circuit-breaker knobs are fixed, as they
/// don't need per-deployment tuning.
/// </summary>
public static class TwitchClientOptions
{
    /// <summary>Bounds the whole outbound call (all retry attempts included), replacing HttpClient's 100s default.</summary>
    public const int DefaultClientTimeoutSeconds = 10;

    /// <summary>Retry attempts for the idempotent GET; the POST token exchange is never retried (see <see cref="ConfigureResilience"/>).</summary>
    public const int DefaultRetryAttempts = 2;

    /// <summary>Per-attempt timeout, shorter than the total so a hang doesn't consume the whole budget on one try.</summary>
    public const int DefaultAttemptTimeoutSeconds = 4;

    public const double CircuitBreakerFailureRatio = 0.5;
    public const int CircuitBreakerMinimumThroughput = 4;
    public const int CircuitBreakerSamplingDurationSeconds = 30;
    public const int CircuitBreakerBreakDurationSeconds = 15;

    /// <summary>
    /// Shared by Program.cs and TwitchAuthServiceTests so the tests cover the
    /// pipeline actually registered in production, not a parallel copy.
    /// </summary>
    public static void ConfigureResilience(
        HttpStandardResilienceOptions options, int retryAttempts, int attemptTimeoutSeconds, int totalTimeoutSeconds)
    {
        options.Retry.MaxRetryAttempts = retryAttempts;
        options.Retry.UseJitter = true;
        // ExchangeCodeAsync POSTs a single-use OAuth authorization code. Retrying
        // after a response was produced but lost would replay the code, fail with
        // invalid_grant and mask the real error, so POST is never retried. The
        // client's only other call (GetUserInfoAsync's GET) is idempotent.
        options.Retry.DisableFor(HttpMethod.Post);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(attemptTimeoutSeconds);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(totalTimeoutSeconds);
        options.CircuitBreaker.FailureRatio = CircuitBreakerFailureRatio;
        options.CircuitBreaker.MinimumThroughput = CircuitBreakerMinimumThroughput;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(CircuitBreakerSamplingDurationSeconds);
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(CircuitBreakerBreakDurationSeconds);
    }
}
