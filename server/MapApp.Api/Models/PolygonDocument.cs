using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;

namespace MapApp.Api.Models;

/// <summary>
/// A polygon persisted in MongoDB. The geometry is stored as a GeoJSON <c>Polygon</c>
/// (longitude/latitude order) and indexed with a <c>2dsphere</c> index.
/// </summary>
public sealed class PolygonDocument : IDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required GeoJsonPolygon<GeoJson2DGeographicCoordinates> Geometry { get; init; }

    public DateTime CreatedAt { get; init; }
}
