using Atlas.Messaging;
using Atlas.Persistence;
using Atlas.Verification.Application;
using Atlas.Verification.Infrastructure;
using Atlas.Verification.Infrastructure.Persistence;
using Atlas.Verification.Infrastructure.Providers;
using Atlas.Verification.Worker.Consumers;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddApplicationLayer();
builder.AddInfrastructureLayer();
builder.AddMessaging<VerificationDbContext>(bus => bus.AddConsumer<ApplicationSubmittedConsumer>());

// Stopping finishes the message in progress, which may be waiting on a provider, so the host waits for the slowest.
builder.WaitOnShutdown(ProviderOptions.LongestTimeout(builder.Configuration) + TimeSpan.FromSeconds(15));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await app.MigrateDatabaseAsync<VerificationDbContext>();
}

// No public API: the worker reacts to messages. HTTP is only for health checks.
app.MapDefaultEndpoints();

app.Run();
