using BankGateway.Worker.Configuration;
using Messaging.Configuration;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddKafkaOptions(builder.Configuration);
builder.Services.AddOutboxOptions(builder.Configuration);
builder.Services.AddRetryOptions(builder.Configuration);
builder.Services.AddWorkerOptions(builder.Configuration);

var host = builder.Build();
host.Run();
