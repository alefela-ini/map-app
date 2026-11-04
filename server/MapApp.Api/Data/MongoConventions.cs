using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;

namespace MapApp.Api.Data;

/// <summary>
/// Registers BSON conventions for the application's document types:
/// camelCase element names, enums stored as strings and tolerance of unknown fields.
/// </summary>
public static class MongoConventions
{
    private static int _registered;

    public static void Register()
    {
        // Conventions are global and must be registered only once, before the first class map is built.
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        var pack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new EnumRepresentationConvention(BsonType.String),
            new IgnoreExtraElementsConvention(true),
        };

        ConventionRegistry.Register(
            "MapAppConventions",
            pack,
            type => type.Namespace?.StartsWith("MapApp.Api.Models", StringComparison.Ordinal) == true);
    }
}
