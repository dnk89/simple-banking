using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// MockBank does not reference Messaging (no Kafka or EF dependency), so it wires the same OTel setup
// as Messaging.Observability.OpenTelemetryExtensions.AddServiceObservability inline.
OpenTelemetry.Sdk.SetDefaultTextMapPropagator(new TraceContextPropagator());
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("mockbank"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter("Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.Server.Kestrel")
        .AddOtlpExporter());
builder.Logging.AddOpenTelemetry(logging => logging.AddOtlpExporter());

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// MockBank has no database, so ready has no checks of its own yet.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");

app.Run();
