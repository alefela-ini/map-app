using System.ComponentModel.DataAnnotations;

namespace MapApp.Api.Configuration;

/// <summary>
/// Strongly typed MongoDB settings bound from the <c>MongoDb</c> configuration section.
/// </summary>
public sealed class MongoDbSettings
{
    public const string SectionName = "MongoDb";

    /// <summary>MongoDB connection string, e.g. <c>mongodb://localhost:27017</c>.</summary>
    [Required]
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>Name of the database that holds the map collections.</summary>
    [Required]
    public string DatabaseName { get; init; } = string.Empty;

    /// <summary>Collection that stores polygons.</summary>
    [Required]
    public string PolygonsCollection { get; init; } = "polygons";

    /// <summary>Collection that stores map objects (markers / symbols).</summary>
    [Required]
    public string ObjectsCollection { get; init; } = "mapObjects";
}
