using Atlas.Messaging;
using Atlas.Persistence;
using Atlas.Verification.Application;
using Atlas.Verification.Infrastructure;
using Atlas.Verification.Infrastructure.Persistence;
using Atlas.Verification.Worker.Consumers;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddApplicationLayer();
builder.AddInfrastructureLayer();
builder.AddMessaging<VerificationDbContext>(bus => bus.AddConsumer<ApplicationSubmittedConsumer>());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await app.MigrateDatabaseAsync<VerificationDbContext>();
}

// No public API: the worker reacts to messages. HTTP is only for health checks.
app.MapDefaultEndpoints();

app.Run();
