using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MapApp.Api.Data;
using MapApp.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace MapApp.Api.Tests;

/// <summary>
/// HTTP-level tests of the REST contract (routing, JSON shape, status codes) with an in-memory store.
/// </summary>
public sealed class ApiTests : IClassFixture<ApiTests.Factory>
{
    private readonly HttpClient _client;

    public ApiTests(Factory factory)
    {
        _client = factory.CreateClient();
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // No real MongoDB in these tests.
                services.RemoveAll<IHostedService>();
                services.RemoveAll(typeof(IRepository<>));
                services.AddSingleton(typeof(IRepository<>), typeof(InMemoryRepository<>));
            });
        }
    }

    [Fact]
    public async Task Polygon_lifecycle_create_read_delete()
    {
        await _client.DeleteAsync("/api/polygons");

        var post = await _client.PostAsync("/api/polygons", Json("""
            [{ "name": "Zone", "geometry": { "type": "Polygon", "coordinates": [[[34.78,32.08],[34.80,32.08],[34.80,32.06]]] } }]
            """));
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);

        var saved = await post.Content.ReadFromJsonAsync<JsonElement>();
        var polygon = Assert.Single(saved.EnumerateArray());
        Assert.Equal("Zone", polygon.GetProperty("name").GetString());
        Assert.Equal("Polygon", polygon.GetProperty("geometry").GetProperty("type").GetString());
        var id = polygon.GetProperty("id").GetString();

        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/polygons/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/polygons/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/polygons/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/polygons/{id}")).StatusCode);
    }

    [Fact]
    public async Task Object_symbol_is_serialized_as_camel_case_string()
    {
        var post = await _client.PostAsync("/api/objects", Json("""
            [{ "name": "J", "symbol": "jeep", "geometry": { "type": "Point", "coordinates": [34.79, 32.07] } }]
            """));
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);

        var saved = await post.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(saved.EnumerateArray(), o => o.GetProperty("symbol").GetString() == "jeep");
    }

    [Theory]
    [InlineData("""[{ "symbol": "tank", "geometry": { "type": "Point", "coordinates": [0, 0] } }]""")]
    [InlineData("""[{ "symbol": 1, "geometry": { "type": "Point", "coordinates": [0, 0] } }]""")]
    [InlineData("""[{ "symbol": "marker", "geometry": { "type": "Point", "coordinates": [0, 95] } }]""")]
    [InlineData("""[{ "symbol": "marker" }]""")]
    [InlineData("""[]""")]
    [InlineData("""[null]""")]
    [InlineData("""{ "not": "an array" }""")]
    public async Task Invalid_objects_return_validation_problem(string body)
    {
        var response = await _client.PostAsync("/api/objects", Json(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Self_intersecting_polygon_returns_400_with_field_error()
    {
        var response = await _client.PostAsync("/api/polygons", Json("""
            [{ "geometry": { "type": "Polygon", "coordinates": [[[0,0],[1,1],[1,0],[0,1]]] } }]
            """));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("[0].geometry.coordinates", out _));
    }

    [Fact]
    public async Task Unknown_or_malformed_id_returns_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/objects/not-an-id")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync("/api/objects/6aeafc7c256f30bae76b3377")).StatusCode);
    }

    private static StringContent Json(string json) => new(json, System.Text.Encoding.UTF8, "application/json");
}
