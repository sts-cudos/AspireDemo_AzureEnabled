using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddHttpClient("backend", client =>
{
    // Uses .NET service discovery (https://learn.microsoft.com/en-us/dotnet/core/extensions/service-discovery)
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
builder.Services.AddCors();
var app = builder.Build();

app.MapDefaultEndpoints();
app.UseHttpsRedirection();
app.UseSession();
app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

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

app.MapGet("/ip", async (IConfiguration config) =>
{
    // See also https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/app-host-overview?tabs=docker#service-endpoint-environment-variable-format
    // We could also access the environment variable services__backend__https__0 directly.
    var serviceReference = config["Services:backend:https:0"];
    serviceReference = serviceReference?.Replace("https://", string.Empty);
    var port = serviceReference?[(serviceReference.IndexOf(':') + 1)..] ?? string.Empty;
    serviceReference = serviceReference![..^(port.Length + 1)];

    var ipAddress = string.Empty;
    try
    {
        if (!string.IsNullOrEmpty(serviceReference))
        {
            var hostEntry = await Dns.GetHostEntryAsync(serviceReference);
            ipAddress = hostEntry.AddressList.FirstOrDefault()?.ToString() ?? "Not found";
        }
        else
        {
            ipAddress = "Service reference not available";
        }
    }
    catch (Exception ex)
    {
        ipAddress = $"Error resolving IP: {ex.Message}";
    }

    return Results.Ok(new { 
        ServiceReference = serviceReference,
        Port = port,
        IpAddress = ipAddress
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
