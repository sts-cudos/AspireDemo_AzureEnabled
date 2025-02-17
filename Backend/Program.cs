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