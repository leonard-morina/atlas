using Atlas.Stubs;
using Atlas.Stubs.Calls;
using Atlas.Stubs.IdNow;
using Atlas.Stubs.WorldCheck;

// Stand-ins for IDNow and World-Check
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.Configure<StubOptions>(builder.Configuration.GetSection("Stubs"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<CallLog>();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapIdNow();
app.MapWorldCheck();
app.MapCallLog();

app.Run();
