using MapApp.Api.Models;
using MongoDB.Driver;

namespace MapApp.Api.Data;

/// <summary>
/// Creates the <c>2dsphere</c> indexes on startup. Retries in the background until MongoDB is reachable,
/// so the API can start before the database (e.g. under docker compose).
/// </summary>
public sealed class MongoIndexInitializer(
    IMongoCollection<PolygonDocument> polygons,
    IMongoCollection<MapObjectDocument> objects,
    ILogger<MongoIndexInitializer> logger) : BackgroundService
{
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(1);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await polygons.Indexes.CreateOneAsync(
                    new CreateIndexModel<PolygonDocument>(Builders<PolygonDocument>.IndexKeys.Geo2DSphere(p => p.Geometry)),
                    cancellationToken: stoppingToken);

                await objects.Indexes.CreateOneAsync(
                    new CreateIndexModel<MapObjectDocument>(Builders<MapObjectDocument>.IndexKeys.Geo2DSphere(o => o.Location)),
                    cancellationToken: stoppingToken);

                logger.LogInformation("MongoDB 2dsphere indexes are in place");
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not create MongoDB indexes, retrying in {Delay}", delay);
                await Task.Delay(delay, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxDelay.Ticks));
            }
        }
    }
}
