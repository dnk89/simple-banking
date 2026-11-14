using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Messaging.Observability;

public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Traces, metrics and logs to the OTLP endpoint named by <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>.
    /// Also sets the W3C trace-context propagator as the process default: the Kafka header carrier
    /// (M1) relies on it to carry <c>traceparent</c> between services.
    /// <para>
    /// ASP.NET Core instrumentation is deliberately not added here: it pulls in the ASP.NET Core framework,
    /// which the worker's runtime image does not have. Web hosts register it themselves.
    /// </para>
    /// </summary>
    public static IHostApplicationBuilder AddServiceObservability(this IHostApplicationBuilder builder, string serviceName)
    {
        Sdk.SetDefaultTextMapPropagator(new TraceContextPropagator());

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddHttpClientInstrumentation()
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                // HttpClient emits metrics through a built-in meter, not an instrumentation hook.
                .AddMeter("System.Net.Http")
                .AddOtlpExporter());

        builder.Logging.AddOpenTelemetry(logging => logging.AddOtlpExporter());

        return builder;
    }
}
