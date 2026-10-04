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
builder.AddMessaging<AccountsDbContext>(bus => bus.AddConsumer<AccountOpeningRequestedConsumer>());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await app.MigrateDatabaseAsync<AccountsDbContext>();
}

// No public API: the worker reacts to messages and works through its queue. HTTP is only for health checks.
app.MapDefaultEndpoints();

app.Run();
