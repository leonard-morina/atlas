using Atlas.Onboarding.Api.Extensions;
using Atlas.Onboarding.Api.Features.Applications;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddVersionedApi();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapVersionedApiDocs();
app.MapApplicationsApi();

app.Run();
