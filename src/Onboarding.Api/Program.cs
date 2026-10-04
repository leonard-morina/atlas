using Atlas.Documents;
using Atlas.Messaging;
using Atlas.Onboarding.Api.Consumers;
using Atlas.Onboarding.Api.Extensions;
using Atlas.Onboarding.Api.Features.Applications;
using Atlas.Onboarding.Application;
using Atlas.Onboarding.Infrastructure;
using Atlas.Onboarding.Infrastructure.Persistence;
using Atlas.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddHttpApiConventions();
builder.AddVersionedApi();
builder.AddMarkets();
builder.AddApplicationLayer();
builder.AddInfrastructureLayer();
builder.AddMessaging<OnboardingDbContext>(bus =>
{
    bus.AddConsumer<VerificationCompletedConsumer>();
    bus.AddConsumer<AccountOpenedConsumer>();
    bus.AddBroadcastConsumer<ApplicationDecidedConsumer>();
});
builder.AddApplicationsApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await app.MigrateDatabaseAsync<OnboardingDbContext>();
    // If we were to be in prod, we wouldn't need to create the docs container like this, but since we're running the azurite-storage behind a container we create this here
    await app.CreateDocumentContainerAsync();
}

app.UseHttpApiConventions();
app.MapDefaultEndpoints();
app.MapVersionedApiDocs();
app.MapApplicationsApi();

app.Run();
