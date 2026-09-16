using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Npgsql;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;
using Soulsjwa.Api.Diagnostics;

namespace Soulsjwa.Api.Extensions;

public static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        var serviceName = builder.Configuration["OpenTelemetry:ServiceName"] ?? DiagnosticsConfig.ServiceName;
        var serviceVersion = builder.Configuration["OpenTelemetry:ServiceVersion"] ?? "1.0.0";

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.With<SensitiveQueryStringRedactor>()
            .Enrich.WithProperty("ServiceName", serviceName)
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .WriteTo.Console(new RenderedCompactJsonFormatter())
            .CreateLogger();

        builder.Host.UseSerilog();

        // Lets an operator disable telemetry export entirely — e.g. a self-hosted
        // instance with no OTLP collector to send to — without touching code.
        // Console/Serilog logging above is unaffected; this only skips the
        // OpenTelemetry tracing/metrics/logging pipeline and its OTLP exporters.
        var otelEnabled = builder.Configuration.GetValue("OpenTelemetry:Enabled", defaultValue: true);
        if (!otelEnabled)
        {
            return builder;
        }

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: serviceName,
                    serviceVersion: serviceVersion,
                    serviceInstanceId: Environment.MachineName)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = builder.Environment.EnvironmentName,
                }))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation()
                    // Database spans come from Npgsql's own instrumentation.
                    // The SqlClient instrumentation that used to sit here is
                    // SQL Server only and never produced a span for this app.
                    .AddNpgsql()
                    .AddSource(DiagnosticsConfig.ServiceName)
                    .AddSource(serviceName);

                if (builder.Environment.IsDevelopment())
                {
                    tracing.AddConsoleExporter();
                }

                tracing.AddOtlpExporter(options => ConfigureOtlpExporter(options, builder.Configuration));
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddMeter(DiagnosticsConfig.ServiceName)
                    .AddMeter(serviceName);

                if (builder.Environment.IsDevelopment())
                {
                    metrics.AddConsoleExporter();
                }

                metrics.AddOtlpExporter(options => ConfigureOtlpExporter(options, builder.Configuration));
            });

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.ParseStateValues = true;
            logging.AddOtlpExporter(options => ConfigureOtlpExporter(options, builder.Configuration));
        });

        return builder;
    }

    private static void ConfigureOtlpExporter(OtlpExporterOptions options, IConfiguration configuration)
    {
        var endpoint = configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";
        options.Endpoint = new Uri(endpoint);
        options.Protocol = OtlpExportProtocol.Grpc;

        var token = configuration["OpenTelemetry:ApiToken"];
        if (!string.IsNullOrWhiteSpace(token))
        {
            options.Headers = $"Authorization=Api-Token {token}";
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
        }
    }
}
