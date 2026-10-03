using System.Data.Common;
using Atlas.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

var env = DotEnv.Load(builder.AppHostDirectory);

// One SQL Server (platform/), one database per service: services share the server, never each other's data.
string SqlDatabase(string database) => new DbConnectionStringBuilder
{
    ["Server"] = $"localhost,{env["SQL_PORT"]}",
    ["Database"] = database,
    ["User Id"] = "sa",
    ["Password"] = env["SQL_PASSWORD"],
    ["TrustServerCertificate"] = "True"
}.ConnectionString;

builder.Configuration["ConnectionStrings:onboarding-db"] = SqlDatabase("atlas_onboarding");

builder.Configuration["ConnectionStrings:rabbitmq"] =
    $"amqp://{Uri.EscapeDataString(env["RABBITMQ_USER"])}:{Uri.EscapeDataString(env["RABBITMQ_PASSWORD"])}" +
    $"@localhost:{env["RABBITMQ_PORT"]}";

builder.Configuration["ConnectionStrings:seq"] = $"http://localhost:{env["SEQ_PORT"]}";

// Azurite's fixed development account (public, documented by Microsoft; it only exists in the emulator).
builder.Configuration["ConnectionStrings:documents"] =
    "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
    "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
    $"BlobEndpoint=http://127.0.0.1:{env["AZURITE_BLOB_PORT"]}/devstoreaccount1;";

var onboardingDb = builder.AddConnectionString("onboarding-db");
var rabbitmq = builder.AddConnectionString("rabbitmq");
var seq = builder.AddConnectionString("seq");
var documents = builder.AddConnectionString("documents");

var onboardingApi = builder.AddProject<Projects.Onboarding_Api>("onboarding-api")
    .WithReference(onboardingDb).WithReference(documents).WithReference(rabbitmq).WithReference(seq)
    .WithForwardedHeaders()
    .WithHttpHealthCheck("/health");

// This is the Yarp reverse proxy, its the only reachable service, and it maps the versioned API from onboarding to the unversioned one
builder.AddProject<Projects.Gateway>("gateway")
    .WithReference(onboardingApi).WaitFor(onboardingApi)
    .WithReference(seq)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

builder.Build().Run();
