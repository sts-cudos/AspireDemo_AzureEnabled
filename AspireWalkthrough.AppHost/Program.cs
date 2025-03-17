using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var backend = builder.AddProject<Projects.Backend>("backend")
    .WithReplicas(2);

var user = builder.AddParameter("postgresuser", "postgres");
var pw = builder.AddParameter("postgrespassword", secret: true);

var postgres = builder
    .AddAzurePostgresFlexibleServer("postgres");

if (builder.Environment.IsDevelopment())
{
    postgres.RunAsContainer(configure =>
    {
        configure
            .WithDataVolume("postgresdata", isReadOnly: false)
            .WithContainerName("postgres-local");
    });
}
else{
    postgres.WithPasswordAuthentication(user, pw);
}

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

var frontend = builder.AddNpmApp("frontend", "../AngularFE")
    .WithReference(webapi)
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

if(builder.Environment.IsDevelopment())
{
    frontend.WithHttpEndpoint(env: "PORT");
}
else{
    frontend.WithHttpsEndpoint(env: "PORT", port: 80);
}

builder.Build().Run();
