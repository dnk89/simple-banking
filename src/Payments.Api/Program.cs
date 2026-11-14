using Messaging.Configuration;
using Messaging.Observability;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Payments.Api.Configuration;
using Payments.Api.Data;
using Payments.Api.Health;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceObservability("payments-api");
builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddAspNetCoreInstrumentation());
builder.Services.ConfigureOpenTelemetryMeterProvider(metrics =>
    metrics.AddMeter("Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.Server.Kestrel"));

builder.Services.AddKafkaOptions(builder.Configuration);
builder.Services.AddOutboxOptions(builder.Configuration);
builder.Services.AddPaymentsOptions(builder.Configuration);

builder.Services.AddDbContext<PaymentsDbContext>((services, options) =>
{
    var connectionString = services.GetRequiredService<IOptions<ConnectionStringsOptions>>().Value.Payments;
    PaymentsDbContext.ConfigureDatabase(options, connectionString);
});
builder.Services.AddDatabaseMigrations<PaymentsDbContext>();

// Live only checks that the process responds. Ready also requires the database.
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();
