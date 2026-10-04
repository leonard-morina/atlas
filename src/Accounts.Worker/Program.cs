using Atlas.Accounts.Application;
using Atlas.Accounts.Infrastructure;
using Atlas.Accounts.Infrastructure.Persistence;
using Atlas.Accounts.Worker.Consumers;
using Atlas.Messaging;
using Atlas.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddApplicationLayer();
builder.AddInfrastructureLayer();
builder.AddMessaging<AccountsDbContext>(bus =>
{
    bus.AddConsumer<AccountOpeningRequestedConsumer>();
    bus.AddBroadcastConsumer<AccountOpeningQueuedConsumer>();
});

// Stopping finishes the core banking calls in flight (AccountOpeningProcessor), so the host waits for the longest.
builder.WaitOnShutdown(AccountOpeningOptions.From(builder.Configuration).LongestCall + TimeSpan.FromSeconds(30));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await app.MigrateDatabaseAsync<AccountsDbContext>();
}

// No public API: the worker reacts to messages and works through its queue. HTTP is only for health checks.
app.MapDefaultEndpoints();

app.Run();
