using MapApp.Api.Contracts;
using MapApp.Api.Models;
using MapApp.Api.Validation;
using MongoDB.Driver.GeoJsonObjectModel;

namespace MapApp.Api.Services;

/// <summary>
/// Converts between API contracts (plain GeoJSON arrays) and MongoDB GeoJSON object model types.
/// GeoJSON positions are always <c>[longitude, latitude]</c>.
/// </summary>
public static class GeoJsonMapper
{
    public static GeoJsonPoint<GeoJson2DGeographicCoordinates> ToMongoPoint(Position position) =>
        GeoJson.Point(GeoJson.Geographic(position.Longitude, position.Latitude));

    /// <param name="closedRing">A closed ring (first position equals last position).</param>
    public static GeoJsonPolygon<GeoJson2DGeographicCoordinates> ToMongoPolygon(IReadOnlyList<Position> closedRing) =>
        GeoJson.Polygon(closedRing.Select(p => GeoJson.Geographic(p.Longitude, p.Latitude)).ToArray());

    public static PolygonDto ToDto(PolygonDocument document)
    {
        var ring = document.Geometry.Coordinates.Exterior.Positions
            .Select(p => new[] { p.Longitude, p.Latitude })
            .ToArray();

        return new PolygonDto(
            document.Id,
            document.Name,
            new PolygonGeometry(GeoJsonTypes.Polygon, [ring]),
            document.CreatedAt);
    }

    public static MapObjectDto ToDto(MapObjectDocument document)
    {
        var coordinates = document.Location.Coordinates;

        return new MapObjectDto(
            document.Id,
            document.Name,
            document.Symbol,
            new PointGeometry(GeoJsonTypes.Point, [coordinates.Longitude, coordinates.Latitude]),
            document.CreatedAt);
    }
}
