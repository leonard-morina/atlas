using System.Data.Common;
using Atlas.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

var env = DotEnv.Load(builder.AppHostDirectory);

var instance = AtlasInstance.From(builder.Configuration["Atlas:Instance"]);

const string onboardingDbConnectionStringName = "onboarding-db";
const string verificationDbConnectionStringName = "verification-db";
const string accountsDbConnectionStringName = "accounts-db";
const string rabbitMqConnectionStringName = "rabbitmq";
const string seqConnectionStringName = "seq";
const string documentsConnectionStringName = "documents";
const string redisConnectionStringName = "redis";

string[] databases =
[
    instance.Database("atlas_onboarding"),
    instance.Database("atlas_verification"),
    instance.Database("atlas_accounts"),
];

builder.Configuration[$"ConnectionStrings:{onboardingDbConnectionStringName}"] = SqlDatabase(databases[0]);
builder.Configuration[$"ConnectionStrings:{verificationDbConnectionStringName}"] = SqlDatabase(databases[1]);
builder.Configuration[$"ConnectionStrings:{accountsDbConnectionStringName}"] = SqlDatabase(databases[2]);
builder.Configuration[$"ConnectionStrings:{rabbitMqConnectionStringName}"] =
    $"amqp://{Uri.EscapeDataString(env["RABBITMQ_USER"])}:{Uri.EscapeDataString(env["RABBITMQ_PASSWORD"])}" +
    $"@localhost:{env["RABBITMQ_PORT"]}" +
    (instance.IsDevelopment ? "" : $"/{Uri.EscapeDataString(instance.VirtualHost)}");
builder.Configuration[$"ConnectionStrings:{seqConnectionStringName}"] = $"http://localhost:{env["SEQ_PORT"]}";
builder.Configuration[$"ConnectionStrings:{redisConnectionStringName}"] = $"localhost:{env["REDIS_PORT"]}";
// Azurite's fixed development account (public, documented by Microsoft; it only exists in the emulator).
builder.Configuration[$"ConnectionStrings:documents"] =
    "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
    "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
    $"BlobEndpoint=http://127.0.0.1:{env["AZURITE_BLOB_PORT"]}/devstoreaccount1;";

var onboardingDb = builder.AddConnectionString(onboardingDbConnectionStringName);
var verificationDb = builder.AddConnectionString(verificationDbConnectionStringName);
var accountsDb = builder.AddConnectionString(accountsDbConnectionStringName);
var rabbitmq = builder.AddConnectionString(rabbitMqConnectionStringName);
var seq = builder.AddConnectionString(seqConnectionStringName);
var documents = builder.AddConnectionString(documentsConnectionStringName);
var redis = builder.AddConnectionString(redisConnectionStringName);

// A named instance needs its own virtual host. On request (Atlas:ResetInstance, the integration tests) it starts from
// nothing instead: its databases and virtual host are deleted, then rebuilt by the services.
var rabbitMqManagement =
    new RabbitMqManagement(int.Parse(env["RABBITMQ_MANAGEMENT_PORT"]), env["RABBITMQ_USER"], env["RABBITMQ_PASSWORD"]);
if (bool.TryParse(builder.Configuration["Atlas:ResetInstance"], out var reset) && reset && !instance.IsDevelopment)
{
    await instance.ResetAsync(SqlDatabase("master"), databases, rabbitMqManagement);
}
else
{
    await instance.EnsureCreatedAsync(rabbitMqManagement);
}

var stubs = builder.AddProject<Projects.Stubs>("stubs")
    .WithInstance(instance)
    .WithReference(seq)
    .WithHttpHealthCheck("/health");

var onboardingApi = builder.AddProject<Projects.Onboarding_Api>("onboarding-api")
    .WithReference(onboardingDb).WithReference(documents).WithReference(rabbitmq).WithReference(seq)
    .WithForwardedHeaders()
    .WithInstance(instance)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Verification_Worker>("verification-worker")
    .WithReference(verificationDb).WithReference(documents).WithReference(rabbitmq).WithReference(seq)
    .WithReference(stubs)
    .WithInstance(instance)
    .WithHttpHealthCheck("/health");

// Opens the accounts of approved applications in core banking (the stand-in in stubs locally).
builder.AddProject<Projects.Accounts_Worker>("accounts-worker")
    .WithReference(accountsDb).WithReference(rabbitmq).WithReference(seq)
    .WithReference(stubs)
    .WithInstance(instance)
    .WithHttpHealthCheck("/health");

// This is the Yarp reverse proxy, its the only reachable service, and it maps the versioned API from onboarding to the unversioned one
builder.AddProject<Projects.Gateway>("gateway")
    .WithReference(onboardingApi).WaitFor(onboardingApi)
    .WithReference(redis)
    .WithReference(seq)
    .WithInstance(instance)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

builder.Build().Run();
return;

// We use on Sql Server instance with multiple databases per service(Onboarding, Verification, Accounts)
string SqlDatabase(string database) => new DbConnectionStringBuilder
{
    ["Server"] = $"localhost,{env["SQL_PORT"]}",
    ["Database"] = database,
    ["User Id"] = "sa",
    ["Password"] = env["SQL_PASSWORD"],
    ["TrustServerCertificate"] = "True"
}.ConnectionString;
