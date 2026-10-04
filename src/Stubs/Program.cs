using Atlas.Stubs;
using Atlas.Stubs.Calls;
using Atlas.Stubs.CoreBanking;
using Atlas.Stubs.IdNow;
using Atlas.Stubs.WorldCheck;

// Stand-ins for IDNow, World-Check and the core banking ACCOUNTS service (no sandbox exists for any of them).
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.Configure<StubOptions>(builder.Configuration.GetSection("Stubs"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<CallLog>();
builder.Services.Configure<CoreBankingOptions>(builder.Configuration.GetSection("Stubs:CoreBanking"));
builder.Services.AddSingleton<CoreBankingLedger>();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapIdNow();
app.MapWorldCheck();
app.MapCoreBanking();
app.MapCallLog();

app.Run();
