using System.Data.Common;
using Atlas.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

var env = DotEnv.Load(builder.AppHostDirectory);

builder.Configuration["ConnectionStrings:sql"] = new DbConnectionStringBuilder
{
    ["Server"] = $"localhost,{env["SQL_PORT"]}",
    ["User Id"] = "sa",
    ["Password"] = env["SQL_PASSWORD"],
    // The SQL Server container uses a self-signed certificate.
    ["TrustServerCertificate"] = "True",
}.ConnectionString;

builder.Configuration["ConnectionStrings:rabbitmq"] =
    $"amqp://{Uri.EscapeDataString(env["RABBITMQ_USER"])}:{Uri.EscapeDataString(env["RABBITMQ_PASSWORD"])}" +
    $"@localhost:{env["RABBITMQ_PORT"]}";

var sql = builder.AddConnectionString("sql");
var rabbitmq = builder.AddConnectionString("rabbitmq");

builder.AddProject<Projects.Onboarding_Api>("onboarding-api")
    .WithReference(sql).WithReference(rabbitmq)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
