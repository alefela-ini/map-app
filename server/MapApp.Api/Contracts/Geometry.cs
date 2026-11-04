namespace MapApp.Api.Contracts;

/// <summary>
/// GeoJSON geometry type names used by the API.
/// </summary>
public static class GeoJsonTypes
{
    public const string Point = "Point";
    public const string Polygon = "Polygon";
}

/// <summary>
/// GeoJSON <c>Point</c> geometry. <see cref="Coordinates"/> is <c>[longitude, latitude]</c>.
/// </summary>
public sealed record PointGeometry(string Type, double[] Coordinates);

/// <summary>
/// GeoJSON <c>Polygon</c> geometry. <see cref="Coordinates"/> is an array of linear rings,
/// each ring an array of <c>[longitude, latitude]</c> positions. Only a single exterior ring is supported.
/// </summary>
public sealed record PolygonGeometry(string Type, double[][][] Coordinates);
