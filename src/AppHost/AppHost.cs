var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddConnectionString("sql");
var rabbitmq = builder.AddConnectionString("rabbitmq");

builder.AddProject<Projects.Onboarding_Api>("onboarding-api")
    .WithReference(sql).WithReference(rabbitmq)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
