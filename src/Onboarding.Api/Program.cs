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
builder.AddApplicationsApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await app.MigrateDatabaseAsync<OnboardingDbContext>();
}

app.UseHttpApiConventions();
app.MapDefaultEndpoints();
app.MapVersionedApiDocs();
app.MapApplicationsApi();

app.Run();
