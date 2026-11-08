using MapApp.Api.Contracts;
using MapApp.Api.Models;
using MapApp.Api.Realtime;
using MapApp.Api.Services;
using MapApp.Api.Validation;

namespace MapApp.Api.Tests;

public sealed class ServiceTests
{
    private static readonly PolygonGeometry Square =
        new(GeoJsonTypes.Polygon, [[[0, 0], [1, 0], [1, 1], [0, 1]]]);

    private static readonly PolygonGeometry BowTie =
        new(GeoJsonTypes.Polygon, [[[0, 0], [1, 1], [1, 0], [0, 1]]]);

    /// <summary>Records published topics, to check which writes notify connected clients.</summary>
    private sealed class RecordingNotifier : IChangeNotifier
    {
        public List<string> Topics { get; } = [];

        public void Publish(string topic) => Topics.Add(topic);
    }

    [Fact]
    public async Task Saving_returns_only_the_items_of_this_batch()
    {
        var repository = new InMemoryRepository<PolygonDocument>();
        var service = new PolygonService(repository, TimeProvider.System, new RecordingNotifier());

        await service.CreateManyAsync([new CreatePolygonRequest("A", Square)], CancellationToken.None);
        var result = await service.CreateManyAsync([new CreatePolygonRequest("  B  ", Square)], CancellationToken.None);

        Assert.Equal(["B"], result.Select(p => p.Name)); // "Return updated data": the saved items, not the collection
        Assert.Equal(5, result[0].Geometry.Coordinates[0].Length);
        Assert.Equal(2, (await repository.GetAllAsync(CancellationToken.None)).Count);
    }

    [Fact]
    public async Task Writes_notify_clients_but_rejected_or_empty_ones_do_not()
    {
        var notifier = new RecordingNotifier();
        var service = new PolygonService(new InMemoryRepository<PolygonDocument>(), TimeProvider.System, notifier);

        var saved = await service.CreateManyAsync([new CreatePolygonRequest("A", Square)], CancellationToken.None);
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.CreateManyAsync([new CreatePolygonRequest("bad", BowTie)], CancellationToken.None));
        await service.DeleteAsync("6aeafc7c256f30bae76b3377", CancellationToken.None); // nothing to delete
        await service.DeleteAsync(saved[0].Id, CancellationToken.None);
        await service.DeleteAllAsync(CancellationToken.None); // already empty

        Assert.Equal([ChangeTopics.Polygons, ChangeTopics.Polygons], notifier.Topics); // the save and the real delete
    }

    [Fact]
    public async Task One_invalid_polygon_rejects_the_whole_batch()
    {
        var repository = new InMemoryRepository<PolygonDocument>();
        var service = new PolygonService(repository, TimeProvider.System, new RecordingNotifier());

        var error = await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateManyAsync(
            [new CreatePolygonRequest("ok", Square), new CreatePolygonRequest("bad", BowTie)],
            CancellationToken.None));

        Assert.True(error.Errors.ContainsKey("[1].geometry.coordinates"));
        Assert.Empty(await repository.GetAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Empty_or_null_batch_is_rejected()
    {
        var service = new PolygonService(new InMemoryRepository<PolygonDocument>(), TimeProvider.System, new RecordingNotifier());

        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateManyAsync([], CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateManyAsync(null, CancellationToken.None));
    }

    [Fact]
    public async Task Retrying_a_save_with_the_same_client_ids_does_not_create_duplicates()
    {
        var service = new PolygonService(new InMemoryRepository<PolygonDocument>(), TimeProvider.System, new RecordingNotifier());
        CreatePolygonRequest[] batch = [new("A", Square, "draft-1"), new("B", Square, "draft-2")];

        var first = await service.CreateManyAsync(batch, CancellationToken.None);
        // The response was "lost": the client sends the same drafts again, plus a new one.
        var result = await service.CreateManyAsync([.. batch, new("C", Square, "draft-3")], CancellationToken.None);

        Assert.Equal(["A", "B", "C"], result.Select(p => p.Name));
        Assert.Equal(first.Select(p => p.Id), result.Take(2).Select(p => p.Id)); // the stored A and B, not copies
        Assert.Equal(3, (await service.GetAllAsync(CancellationToken.None)).Count);
    }

    [Fact]
    public async Task Client_id_repeated_inside_one_batch_is_rejected()
    {
        var service = new PolygonService(new InMemoryRepository<PolygonDocument>(), TimeProvider.System, new RecordingNotifier());

        var error = await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateManyAsync(
            [new CreatePolygonRequest("A", Square, "same"), new CreatePolygonRequest("B", Square, "same")],
            CancellationToken.None));

        Assert.True(error.Errors.ContainsKey("[1].clientId"));
    }

    [Fact]
    public async Task Batch_with_too_many_vertices_in_total_is_rejected_before_validation()
    {
        var service = new PolygonService(new InMemoryRepository<PolygonDocument>(), TimeProvider.System, new RecordingNotifier());
        // 11 polygons × 1000 positions = 11 000 > 10 000; each one alone would be valid size-wise.
        var big = new PolygonGeometry(GeoJsonTypes.Polygon, [Enumerable.Range(0, 1000).Select(i => new double[] { i * 1e-4, 0 }).ToArray()]);

        var error = await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateManyAsync(
            Enumerable.Range(0, 11).Select(i => new CreatePolygonRequest($"P{i}", big)).ToList(),
            CancellationToken.None));

        Assert.True(error.Errors.ContainsKey("$"));
    }

    [Fact]
    public async Task Too_long_name_is_rejected()
    {
        var service = new PolygonService(new InMemoryRepository<PolygonDocument>(), TimeProvider.System, new RecordingNotifier());

        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateManyAsync(
            [new CreatePolygonRequest(new string('x', 101), Square)], CancellationToken.None));
    }

    [Fact]
    public async Task Objects_default_to_marker_symbol_and_name()
    {
        var service = new MapObjectService(new InMemoryRepository<MapObjectDocument>(), TimeProvider.System, new RecordingNotifier());

        var result = await service.CreateManyAsync(
            [new CreateMapObjectRequest(null, null, new PointGeometry(GeoJsonTypes.Point, [34.78, 32.08]))],
            CancellationToken.None);

        var created = Assert.Single(result);
        Assert.Equal(MapObjectSymbol.Marker, created.Symbol);
        Assert.Equal("Marker", created.Name);
        Assert.Equal([34.78, 32.08], created.Geometry.Coordinates);
    }

    [Fact]
    public async Task Undefined_symbol_value_is_rejected()
    {
        var service = new MapObjectService(new InMemoryRepository<MapObjectDocument>(), TimeProvider.System, new RecordingNotifier());

        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateManyAsync(
            [new CreateMapObjectRequest("x", (MapObjectSymbol)42, new PointGeometry(GeoJsonTypes.Point, [0, 0]))],
            CancellationToken.None));
    }

    [Fact]
    public async Task Delete_removes_only_the_selected_object()
    {
        var service = new MapObjectService(new InMemoryRepository<MapObjectDocument>(), TimeProvider.System, new RecordingNotifier());
        var created = await service.CreateManyAsync(
        [
            new CreateMapObjectRequest("a", MapObjectSymbol.Marker, new PointGeometry(GeoJsonTypes.Point, [0, 0])),
            new CreateMapObjectRequest("b", MapObjectSymbol.Jeep, new PointGeometry(GeoJsonTypes.Point, [1, 1])),
        ], CancellationToken.None);

        Assert.True(await service.DeleteAsync(created[0].Id, CancellationToken.None));
        Assert.False(await service.DeleteAsync(created[0].Id, CancellationToken.None));

        var remaining = Assert.Single(await service.GetAllAsync(CancellationToken.None));
        Assert.Equal("b", remaining.Name);
    }
}
