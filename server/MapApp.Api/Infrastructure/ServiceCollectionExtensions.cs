using MapApp.Api.Configuration;
using MapApp.Api.Data;
using MapApp.Api.Models;
using MapApp.Api.Realtime;
using MapApp.Api.Services;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace MapApp.Api.Infrastructure;

/// <summary>
/// Dependency-injection registrations, grouped by layer to keep <c>Program.cs</c> short.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers the MongoDB client, collections, repositories and index initializer.</summary>
    public static IServiceCollection AddMongoPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        MongoConventions.Register();

        services.AddOptions<MongoDbSettings>()
            .Bind(configuration.GetSection(MongoDbSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IMongoClient>(sp =>
        {
            var settings = MongoClientSettings.FromConnectionString(sp.GetRequiredService<IOptions<MongoDbSettings>>().Value.ConnectionString);
            // Fail fast (instead of the 30 s default) so the client gets a 503 quickly when the database is down.
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            return new MongoClient(settings);
        });

        services.AddSingleton(sp =>
            sp.GetRequiredService<IMongoClient>().GetDatabase(sp.GetRequiredService<IOptions<MongoDbSettings>>().Value.DatabaseName));

        services.AddSingleton(sp => sp.GetRequiredService<IMongoDatabase>()
            .GetCollection<PolygonDocument>(sp.GetRequiredService<IOptions<MongoDbSettings>>().Value.PolygonsCollection));

        services.AddSingleton(sp => sp.GetRequiredService<IMongoDatabase>()
            .GetCollection<MapObjectDocument>(sp.GetRequiredService<IOptions<MongoDbSettings>>().Value.ObjectsCollection));

        services.AddSingleton(typeof(IRepository<>), typeof(MongoRepository<>));
        services.AddHostedService<MongoIndexInitializer>();

        return services;
    }

    /// <summary>Registers the application services.</summary>
    public static IServiceCollection AddMapServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ChangeNotifier>();
        services.AddSingleton<IChangeNotifier>(sp => sp.GetRequiredService<ChangeNotifier>());
        services.AddScoped<IPolygonService, PolygonService>();
        services.AddScoped<IMapObjectService, MapObjectService>();
        return services;
    }
}
