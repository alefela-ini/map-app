using System.Text.Json;
using System.Text.Json.Serialization;
using MapApp.Api.Configuration;
using MapApp.Api.Infrastructure;
using MongoDB.Bson;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Symbols are exchanged as "marker" / "jeep"; numeric values are rejected.
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
    });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();

builder.Services.AddMongoPersistence(builder.Configuration);
builder.Services.AddMapServices();

var corsSettings = builder.Configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>() ?? new CorsSettings();
builder.Services.AddCors(options => options.AddPolicy(CorsSettings.PolicyName, policy => policy
    .WithOrigins(corsSettings.AllowedOrigins)
    .AllowAnyHeader()
    .WithMethods("GET", "POST", "DELETE")));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // OpenAPI document at /openapi/v1.json
}

app.UseCors(CorsSettings.PolicyName);
app.MapControllers();

// Liveness + database connectivity check (used by docker compose).
app.MapGet("/health", async (IMongoDatabase database, CancellationToken cancellationToken) =>
{
    try
    {
        await database.RunCommandAsync((Command<BsonDocument>)"{ ping: 1 }", cancellationToken: cancellationToken);
        return Results.Ok(new { status = "healthy" });
    }
    catch (Exception)
    {
        return Results.Problem("Database is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();

/// <summary>Entry point; declared partial so integration tests can use <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program;
