using Atlas.Gateway.Extensions;
using Atlas.Gateway.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSubmissionRateLimiting();
builder.AddReverseProxyRoutes();

var app = builder.Build();

app.UseRateLimiter();
app.MapDefaultEndpoints();
app.MapReverseProxy();

app.Run();
