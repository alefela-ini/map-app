using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MongoDB.Bson;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace MapApp.Api.Tests;

/// <summary>Starts one real MongoDB in Docker for all integration tests of a class.</summary>
public sealed class MongoContainerFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder("mongo:8.0")
        .WithUsername(string.Empty) // no authentication, like docker-compose
        .WithPassword(string.Empty)
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

/// <summary>
/// The whole API against a real MongoDB (Testcontainers): the real repository and the real index initializer,
/// which the other API tests replace. Needs Docker. Run without them: <c>dotnet test --filter Category!=Integration</c>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MongoIntegrationTests(MongoContainerFixture mongo) : IClassFixture<MongoContainerFixture>
{
    [Fact]
    public async Task Startup_creates_all_indexes_before_the_first_request()
    {
        var database = NewDatabaseName();
        await using var api = CreateApi(database);
        _ = api.CreateClient(); // starts the host; StartAsync of the initializer has completed when this returns

        var db = new MongoClient(mongo.ConnectionString).GetDatabase(database);
        var polygonIndexes = await IndexKeys(db, "polygons");
        var objectIndexes = await IndexKeys(db, "mapObjects");

        Assert.Contains("""{ "geometry" : "2dsphere" }""", polygonIndexes);
        Assert.Contains("""{ "location" : "2dsphere" }""", objectIndexes);
        Assert.Contains("""{ "clientId" : 1 }""", polygonIndexes);
        Assert.Contains("""{ "clientId" : 1 }""", objectIndexes);
    }

    [Fact]
    public async Task Repeated_save_with_the_same_client_id_stores_one_document()
    {
        var database = NewDatabaseName();
        await using var api = CreateApi(database);
        var client = api.CreateClient();
        var body = """
            [{ "name": "Zone", "clientId": "draft-integration-1",
               "geometry": { "type": "Polygon", "coordinates": [[[34.78,32.08],[34.80,32.08],[34.80,32.06]]] } }]
            """;

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/polygons", Json(body))).StatusCode);
        var retry = await client.PostAsync("/api/polygons", Json(body)); // response "lost", user retries
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);

        Assert.Single((await retry.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        var stored = await new MongoClient(mongo.ConnectionString).GetDatabase(database)
            .GetCollection<BsonDocument>("polygons").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        Assert.Equal(1, stored);
    }

    [Fact]
    public async Task Startup_fails_with_a_clear_message_when_stored_data_blocks_the_index()
    {
        var database = NewDatabaseName();
        // A self-intersecting polygon stored before the index existed: the 2dsphere index cannot be built.
        await new MongoClient(mongo.ConnectionString).GetDatabase(database).GetCollection<BsonDocument>("polygons")
            .InsertOneAsync(BsonDocument.Parse("""
                { "name": "bad", "geometry": { "type": "Polygon", "coordinates": [[[0,0],[1,1],[1,0],[0,1],[0,0]]] } }
                """));

        await using var api = CreateApi(database);
        var error = Assert.ThrowsAny<Exception>(() => api.CreateClient());

        Assert.Contains("indexes cannot be created", AllMessages(error));
    }

    private WebApplicationFactory<Program> CreateApi(string database) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseSetting("MongoDb:ConnectionString", mongo.ConnectionString)
            .UseSetting("MongoDb:DatabaseName", database));

    private static string NewDatabaseName() => $"it_{Guid.NewGuid():N}";

    private static async Task<List<string>> IndexKeys(IMongoDatabase db, string collection)
    {
        var indexes = await (await db.GetCollection<BsonDocument>(collection).Indexes.ListAsync()).ToListAsync();
        return indexes.Select(i => i["key"].AsBsonDocument.ToJson()).ToList();
    }

    private static string AllMessages(Exception error)
    {
        var messages = new List<string>();
        for (var e = error; e is not null; e = e.InnerException)
        {
            messages.Add(e.Message);
        }

        return string.Join(" | ", messages);
    }

    private static StringContent Json(string json) => new(json, System.Text.Encoding.UTF8, "application/json");
}
