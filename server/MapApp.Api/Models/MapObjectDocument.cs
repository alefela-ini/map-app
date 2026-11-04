using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;

namespace MapApp.Api.Models;

/// <summary>
/// A map object (marker or custom symbol) persisted in MongoDB. The location is stored
/// as a GeoJSON <c>Point</c> (longitude/latitude order) and indexed with a <c>2dsphere</c> index.
/// </summary>
public sealed class MapObjectDocument : IDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <inheritdoc />
    [BsonIgnoreIfNull]
    public string? ClientId { get; init; }

    public MapObjectSymbol Symbol { get; init; }

    public required GeoJsonPoint<GeoJson2DGeographicCoordinates> Location { get; init; }

    public DateTime CreatedAt { get; init; }
}
