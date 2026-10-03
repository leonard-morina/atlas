using Atlas.Onboarding.Api.Extensions;
using Atlas.Onboarding.Api.Features.Applications;
using Atlas.Onboarding.Application;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddHttpApiConventions();
builder.AddVersionedApi();
builder.AddMarkets();
builder.AddApplicationLayer();
builder.AddApplicationsApi();

var app = builder.Build();

app.UseHttpApiConventions();
app.MapDefaultEndpoints();
app.MapVersionedApiDocs();
app.MapApplicationsApi();

app.Run();
