using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var launchProfile = builder.Configuration["DOTNET_LAUNCH_PROFILE"] ??
                    builder.Configuration["AppHost:DefaultLaunchProfileName"];

// var backend = builder.AddProject<Projects.Backend>("backend")
//     .WithReplicas(2);
var backend = builder.AddConnectionString("backend", "services__backend__https__0");

var user = builder.AddParameter("postgresuser", "postgres");
var pw = builder.AddParameter("postgrespassword", secret: true);

var postgres = builder
    .AddAzurePostgresFlexibleServer("postgres")
    .RunAsContainer(configure =>
    {
        configure
            .WithDataVolume("postgresdata", isReadOnly: false)
            .WithContainerName("postgres-local");
    });

if (builder.ExecutionContext.IsPublishMode)
{
    postgres.WithPasswordAuthentication(user, pw);
}

var aspiredb = postgres.AddDatabase("aspiredb");
var postgresdb = postgres.AddDatabase(name: "postgresdb", databaseName: "aspirepostgres");

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
    .PublishAsDockerFile();

if (builder.ExecutionContext.IsPublishMode)
{
    frontend.WithHttpsEndpoint(env: "PORT")
        .WithExternalHttpEndpoints();
}
else
{
    frontend.WithHttpEndpoint(env: "PORT")
        .WithExternalHttpEndpoints();
}

builder.Build().Run();
