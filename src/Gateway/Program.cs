using Atlas.Gateway.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddReverseProxyRoutes();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapReverseProxy();

app.Run();
