var builder = DistributedApplication.CreateBuilder(args);

var backend = builder.AddProject<Projects.Backend>("backend")
    .WithReplicas(2);

var postgres = builder
    .AddAzurePostgresFlexibleServer("postgres")
    .RunAsContainer(cfg => cfg.WithDataVolume("postgresdata", isReadOnly: false));
var aspiredb = postgres.AddDatabase("aspiredb");
var postgresdb = postgres.AddDatabase(name: "postgresdb", databaseName: "postgres");

var cache = builder
    .AddAzureRedis("cache")
    .RunAsContainer();

var webapi = builder.AddProject<Projects.WebApi>("webapi")
    .WithReference(backend)
    .WithReplicas(2)
    .WithReference(postgresdb)
    .WaitFor(postgresdb)
    .WithReference(aspiredb)
    .WaitFor(aspiredb)
    .WithReference(cache)
    .WaitFor(cache);

var frontend = builder.AddNpmApp("frontend", "../Frontend")
    .WithReference(webapi)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
