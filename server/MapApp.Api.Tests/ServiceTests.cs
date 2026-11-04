using MapApp.Api.Contracts;
using MapApp.Api.Models;
using MapApp.Api.Services;
using MapApp.Api.Validation;

namespace MapApp.Api.Tests;

public sealed class ServiceTests
{
    private static readonly PolygonGeometry Square =
        new(GeoJsonTypes.Polygon, [[[0, 0], [1, 0], [1, 1], [0, 1]]]);

    private static readonly PolygonGeometry BowTie =
        new(GeoJsonTypes.Polygon, [[[0, 0], [1, 1], [1, 0], [0, 1]]]);

    [Fact]
    public async Task Saving_polygons_returns_the_full_updated_list()
    {
        var repository = new InMemoryRepository<PolygonDocument>();
        var service = new PolygonService(repository, TimeProvider.System);

        await service.CreateManyAsync([new CreatePolygonRequest("A", Square)], CancellationToken.None);
        var result = await service.CreateManyAsync([new CreatePolygonRequest("  B  ", Square)], CancellationToken.None);

        Assert.Equal(["A", "B"], result.Select(p => p.Name));
        Assert.All(result, p => Assert.Equal(5, p.Geometry.Coordinates[0].Length));
    }

    [Fact]
    public async Task One_invalid_polygon_rejects_the_whole_batch()
    {
        var repository = new InMemoryRepository<PolygonDocument>();
        var service = new PolygonService(repository, TimeProvider.System);

        var error = await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateManyAsync(
            [new CreatePolygonRequest("ok", Square), new CreatePolygonRequest("bad", BowTie)],
            CancellationToken.None));

        Assert.True(error.Errors.ContainsKey("[1].geometry.coordinates"));
        Assert.Empty(await repository.GetAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Empty_or_null_batch_is_rejected()
    {
        var service = new PolygonService(new InMemoryRepository<PolygonDocument>(), TimeProvider.System);

        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateManyAsync([], CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateManyAsync(null, CancellationToken.None));
    }

    [Fact]
    public async Task Too_long_name_is_rejected()
    {
        var service = new PolygonService(new InMemoryRepository<PolygonDocument>(), TimeProvider.System);

        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateManyAsync(
            [new CreatePolygonRequest(new string('x', 101), Square)], CancellationToken.None));
    }

    [Fact]
    public async Task Objects_default_to_marker_symbol_and_name()
    {
        var service = new MapObjectService(new InMemoryRepository<MapObjectDocument>(), TimeProvider.System);

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
        var service = new MapObjectService(new InMemoryRepository<MapObjectDocument>(), TimeProvider.System);

        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateManyAsync(
            [new CreateMapObjectRequest("x", (MapObjectSymbol)42, new PointGeometry(GeoJsonTypes.Point, [0, 0]))],
            CancellationToken.None));
    }

    [Fact]
    public async Task Delete_removes_only_the_selected_object()
    {
        var service = new MapObjectService(new InMemoryRepository<MapObjectDocument>(), TimeProvider.System);
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
