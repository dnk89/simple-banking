using BankGateway.Worker.Configuration;
using BankGateway.Worker.Data;
using Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddKafkaOptions(builder.Configuration);
builder.Services.AddOutboxOptions(builder.Configuration);
builder.Services.AddRetryOptions(builder.Configuration);
builder.Services.AddWorkerOptions(builder.Configuration);

builder.Services.AddDbContext<BankGatewayDbContext>((services, options) =>
{
    var connectionString = services.GetRequiredService<IOptions<ConnectionStringsOptions>>().Value.BankGateway;
    BankGatewayDbContext.ConfigureDatabase(options, connectionString);
});
builder.Services.AddDatabaseMigrations<BankGatewayDbContext>();

var host = builder.Build();
host.Run();
