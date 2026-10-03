using Atlas.Onboarding.Api.Extensions;
using Atlas.Onboarding.Api.Features.Applications;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddHttpApiConventions();
builder.AddVersionedApi();
builder.AddMarkets();
builder.AddMediatorPipeline();
builder.AddApplicationsApi();

var app = builder.Build();

app.UseHttpApiConventions();
app.MapDefaultEndpoints();
app.MapVersionedApiDocs();
app.MapApplicationsApi();

app.Run();
