# Create empty project with web API

Let's create a new project with .NET Aspire and add a simple web API.

## Create project

```sh
dotnet new aspire

mkdir WebApi
cd WebApi
dotnet new webapi
dotnet add reference ../AspireWalkthrough.ServiceDefaults
cd ..

dotnet sln add WebApi

cd AspireWalkthrough.AppHost
dotnet add reference ../WebApi
cd ..
```

## Web API

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults(); // Note: Service defaults come from shared project
var app = builder.Build();

app.MapDefaultEndpoints();
app.UseHttpsRedirection();

app.MapGet("/ping", () => "pong");

app.Run();
```

## App Host

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var webapi = builder.AddProject<Projects.WebApi>("webapi")
    .WithReplicas(2);

builder.Build().Run();
```

## Run

```sh
dotnet run --project AspireWalkthrough.AppHost/
```

# Add backend service

Let's add another web API that we can access from the first one. We will see how we can reference APIs from another. Additionally, we try debugging.

## Create project

```sh
mkdir Backend
cd Backend
dotnet new webapi
dotnet add reference ../AspireWalkthrough.ServiceDefaults

dotnet sln add Backend

cd AspireWalkthrough.AppHost
dotnet add reference ../Backend
cd ..
```

## Backend

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
var app = builder.Build();

app.MapDefaultEndpoints();
app.UseHttpsRedirection();

app.MapGet("/data", () =>
{
    return Results.Ok(new {
        X = 10,
        Y = 20,
    });
});

app.Run();
```

## App Host

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var backend = builder.AddProject<Projects.Backend>("backend")
    .WithReplicas(2);

var webapi = builder.AddProject<Projects.WebApi>("webapi")
    .WithReference(backend)
    .WithReplicas(2);

builder.Build().Run();
```

## Web API

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddHttpClient("backend", client =>
{
    client.BaseAddress = new("https://backend");
});
var app = builder.Build();

app.MapDefaultEndpoints();
app.UseHttpsRedirection();

app.MapGet("/ping", () => "pong");

app.MapGet("/add", async (IHttpClientFactory factory) =>
{
    var backend = factory.CreateClient("backend");
    var dataResponse = await backend.GetFromJsonAsync<ResultDto>("/data");
    return Results.Ok(new
    {
        Sum = dataResponse?.X ?? 0 + dataResponse?.Y ?? 0,
    });
});

app.Run();

record ResultDto(int X, int Y);
```


# Add Postgres

Let's add a Postgres database to our project. We will see how we can use the database in our services.

## Add NuGet for Postgres

```sh
cd AspireWalkthrough.AppHost
dotnet add package Aspire.Hosting.PostgreSQL
cd ..

cd WebApi
dotnet add package Aspire.Npgsql
cd ..
```

## App Host

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var backend = builder.AddProject<Projects.Backend>("backend")
    .WithReplicas(2);

var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume("postgresdata", isReadOnly: false);
var aspiredb = postgres.AddDatabase("aspiredb");
var postgresdb = postgres.AddDatabase(name: "postgresdb", databaseName: "postgres");

var webapi = builder.AddProject<Projects.WebApi>("webapi")
    .WithReference(backend)
    .WithReplicas(2)
    .WithReference(postgresdb)
    .WaitFor(postgresdb)
    .WithReference(aspiredb)
    .WaitFor(aspiredb);

builder.Build().Run();
```

## Web API

```csharp
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddHttpClient("backend", client =>
{
    client.BaseAddress = new("https://backend");
});
builder.AddKeyedNpgsqlDataSource("aspiredb");
builder.AddKeyedNpgsqlDataSource("postgresdb");
var app = builder.Build();

app.MapDefaultEndpoints();
app.UseHttpsRedirection();

app.MapGet("/ping", () => "pong");

app.MapGet("/add", async (IHttpClientFactory factory) =>
{
    var backend = factory.CreateClient("backend");
    var dataResponse = await backend.GetFromJsonAsync<ResultDto>("/data");
    return Results.Ok(new
    {
        Sum = dataResponse?.X ?? 0 + dataResponse?.Y ?? 0,
    });
});

app.MapGet("/answer-from-db", async ([FromKeyedServices("postgresdb")] NpgsqlDataSource postgres, [FromKeyedServices("aspiredb")] NpgsqlDataSource aspire) =>
{
    // Ensure that database "aspiredb" exists
    var csb = new NpgsqlConnectionStringBuilder(aspire.ConnectionString);
    var databaseName = csb.Database ?? throw new InvalidOperationException("Connection string is null");
    if (!await CheckDatabaseExists(postgres, databaseName))
    {
        await using var createCommand = postgres.CreateCommand($"CREATE DATABASE {databaseName}");
        await createCommand.ExecuteNonQueryAsync();
    }

    // Simulate getting some data from the database
    await using var command = aspire.CreateCommand();
    command.CommandText = "SELECT 42";
    var result = await command.ExecuteScalarAsync();
    return Results.Ok(new { Answer = result, });
});

app.Run();

/// <summary>
/// Helper method to check if a database exists
/// </summary>
static async Task<bool> CheckDatabaseExists(NpgsqlDataSource postgres, string dbName)
{
    await using var cmd = postgres.CreateCommand("SELECT 1 FROM pg_database WHERE datname = @dbName");
    cmd.Parameters.AddWithValue("@dbName", dbName);
    var result = await cmd.ExecuteScalarAsync();
    return result != null;
}

record ResultDto(int X, int Y);
```

## Check Docker

```sh
docker ps
docker volume ls
```

# Add Redis

Let's add a Redis database to our project. We will see how we can use it for caching in ASP.NET Core.

## Add NuGet for Redis

```sh
cd AspireWalkthrough.AppHost
dotnet add package Aspire.Hosting.Redis
cd ..

cd WebApi
dotnet add package Aspire.StackExchange.Redis.DistributedCaching
cd ..
```

## App Host

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var backend = builder.AddProject<Projects.Backend>("backend")
    .WithReplicas(2);

var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume("postgresdata", isReadOnly: false);
var aspiredb = postgres.AddDatabase("aspiredb");
var postgresdb = postgres.AddDatabase(name: "postgresdb", databaseName: "postgres");

var cache = builder.AddRedis("cache");

var webapi = builder.AddProject<Projects.WebApi>("webapi")
    .WithReference(backend)
    .WithReplicas(2)
    .WithReference(postgresdb)
    .WaitFor(postgresdb)
    .WithReference(aspiredb)
    .WaitFor(aspiredb)
    .WithReference(cache)
    .WaitFor(cache);

builder.Build().Run();
```

## Web API

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR.Protocol;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddHttpClient("backend", client =>
{
    client.BaseAddress = new("https://backend");
});
builder.AddKeyedNpgsqlDataSource("aspiredb");
builder.AddKeyedNpgsqlDataSource("postgresdb");
builder.AddRedisDistributedCache("cache");
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromSeconds(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
var app = builder.Build();

app.MapDefaultEndpoints();
app.UseHttpsRedirection();
app.UseSession();

app.MapGet("/ping", () => "pong");

app.MapGet("/add", async (IHttpClientFactory factory) =>
{
    var backend = factory.CreateClient("backend");
    var dataResponse = await backend.GetFromJsonAsync<ResultDto>("/data");
    return Results.Ok(new
    {
        Sum = dataResponse?.X ?? 0 + dataResponse?.Y ?? 0,
    });
});

app.MapGet("/answer-from-db", async ([FromKeyedServices("postgresdb")] NpgsqlDataSource postgres, [FromKeyedServices("aspiredb")] NpgsqlDataSource aspire) =>
{
    // Ensure that database "aspiredb" exists
    var csb = new NpgsqlConnectionStringBuilder(aspire.ConnectionString);
    var databaseName = csb.Database ?? throw new InvalidOperationException("Connection string is null");
    if (!await CheckDatabaseExists(postgres, databaseName))
    {
        await using var createCommand = postgres.CreateCommand($"CREATE DATABASE {databaseName}");
        await createCommand.ExecuteNonQueryAsync();
    }

    // Simulate getting some data from the database
    await using var command = aspire.CreateCommand();
    command.CommandText = "SELECT 42";
    var result = await command.ExecuteScalarAsync();
    return Results.Ok(new { Answer = result, });
});

app.MapGet("/set-session", ([FromQuery(Name = "key")] string keyFromQueryString, HttpContext context) =>
{
    context.Session.SetString("key", keyFromQueryString);
    return Results.Ok();
});

app.MapGet("/get-session", (HttpContext context) =>
{
    var value = context.Session.GetString("key");
    return Results.Ok(new { Value = value, });
});

app.Run();

/// <summary>
/// Helper method to check if a database exists
/// </summary>
static async Task<bool> CheckDatabaseExists(NpgsqlDataSource postgres, string dbName)
{
    await using var cmd = postgres.CreateCommand("SELECT 1 FROM pg_database WHERE datname = @dbName");
    cmd.Parameters.AddWithValue("@dbName", dbName);
    var result = await cmd.ExecuteScalarAsync();
    return result != null;
}

record ResultDto(int X, int Y);
```


# Add Web Frontend

Let's add a web frontend to our project. We will learn more about networking.

## Create project

```sh
mkdir Frontend
cd Frontend
npm init -y
npm install -D vite
cd ..
```

Copy the content of the following files:

* [Frontend/package.json](Frontend/package.json)
* [Frontend/vite.config.js](Frontend/vite.config.js)
* [Frontend/index.html](Frontend/index.html)
* [Frontend/src/index.js](Frontend/src/index.js)

## Add NuGet for Node projects

```sh
cd AspireWalkthrough.AppHost
dotnet add package Aspire.Hosting.NodeJs
cd ..
```

## App Host

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var backend = builder.AddProject<Projects.Backend>("backend")
    .WithReplicas(2);

var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume("postgresdata", isReadOnly: false);
var aspiredb = postgres.AddDatabase("aspiredb");
var postgresdb = postgres.AddDatabase(name: "postgresdb", databaseName: "postgres");

var cache = builder.AddRedis("cache");

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
    .WithExternalHttpEndpoints();

builder.Build().Run();
```

## Web API

Add CORS to the web API:

```csharp
...
builder.Services.AddCors();
...
app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
...
```

## Deployment (App Host)

Not a focus of this walkthrough.

```sh
dotnet add package Aspire.Hosting.Azure.PostgreSQL
dotnet add package Aspire.Hosting.Azure.Redis
```

```cs
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
```

```sh
azd config set alpha.infraSynth on
azd infra synth
```
