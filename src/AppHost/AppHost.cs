using System.Data.Common;
using Atlas.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

var env = DotEnv.Load(builder.AppHostDirectory);

const string onboardingDbConnectionStringName = "onboarding-db";
const string verificationDbConnectionStringName = "verification-db";
const string rabbitMqConnectionStringName = "rabbitmq";
const string seqConnectionStringName = "seq";
const string documentsConnectionStringName = "documents";

builder.Configuration[$"ConnectionStrings:{onboardingDbConnectionStringName}"] = SqlDatabase("atlas_onboarding");
builder.Configuration["$ConnectionStrings:{verificationDbConnectionStringName}"] = SqlDatabase("atlas_verification");
builder.Configuration[$"ConnectionStrings:{rabbitMqConnectionStringName}"] =
    $"amqp://{Uri.EscapeDataString(env["RABBITMQ_USER"])}:{Uri.EscapeDataString(env["RABBITMQ_PASSWORD"])}" +
    $"@localhost:{env["RABBITMQ_PORT"]}";
builder.Configuration[$"ConnectionStrings:{seqConnectionStringName}"] = $"http://localhost:{env["SEQ_PORT"]}";
// Azurite's fixed development account (public, documented by Microsoft; it only exists in the emulator).
builder.Configuration[$"ConnectionStrings:documents"] =
    "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
    "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
    $"BlobEndpoint=http://127.0.0.1:{env["AZURITE_BLOB_PORT"]}/devstoreaccount1;";

var onboardingDb = builder.AddConnectionString(onboardingDbConnectionStringName);
var verificationDb = builder.AddConnectionString(verificationDbConnectionStringName);
var rabbitmq = builder.AddConnectionString(rabbitMqConnectionStringName);
var seq = builder.AddConnectionString(seqConnectionStringName);
var documents = builder.AddConnectionString(documentsConnectionStringName);

var stubs = builder.AddProject<Projects.Stubs>("stubs")
    .WithReference(seq)
    .WithHttpHealthCheck("/health");

var onboardingApi = builder.AddProject<Projects.Onboarding_Api>("onboarding-api")
    .WithReference(onboardingDb).WithReference(documents).WithReference(rabbitmq).WithReference(seq)
    .WithForwardedHeaders()
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Verification_Worker>("verification-worker")
    .WithReference(verificationDb).WithReference(documents).WithReference(rabbitmq).WithReference(seq)
    .WithReference(stubs)
    .WithHttpHealthCheck("/health");

// This is the Yarp reverse proxy, its the only reachable service, and it maps the versioned API from onboarding to the unversioned one
builder.AddProject<Projects.Gateway>("gateway")
    .WithReference(onboardingApi).WaitFor(onboardingApi)
    .WithReference(seq)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

builder.Build().Run();
return;

// We use on Sql Server instance with multiple databases per service(Onboarding, Verification)
string SqlDatabase(string database) => new DbConnectionStringBuilder
{
    ["Server"] = $"localhost,{env["SQL_PORT"]}",
    ["Database"] = database,
    ["User Id"] = "sa",
    ["Password"] = env["SQL_PASSWORD"],
    ["TrustServerCertificate"] = "True"
}.ConnectionString;
