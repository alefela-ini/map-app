using System.Diagnostics;
using MapApp.Api.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MapApp.Api.Data;

/// <summary>
/// Creates the <c>2dsphere</c> indexes during startup. The host awaits <see cref="StartAsync"/> before the web
/// server starts listening, so no request can store a geometry before MongoDB is able to validate it.
/// </summary>
/// <remarks>
/// Transient errors (MongoDB not reachable yet) are retried with exponential back-off for up to a minute.
/// Errors that retrying cannot fix stop the startup with a clear message; the container is then restarted
/// by Docker (<c>restart: unless-stopped</c>) and the failure is visible instead of hidden.
/// <c>createIndex</c> is idempotent, so this runs safely on every start and on every instance.
/// </remarks>
public sealed class MongoIndexInitializer(
    IMongoCollection<PolygonDocument> polygons,
    IMongoCollection<MapObjectDocument> objects,
    ILogger<MongoIndexInitializer> logger) : IHostedService
{
    private static readonly TimeSpan GiveUpAfter = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(16);

    // Errors that retrying cannot fix: the stored data or an existing index definition must be changed first.
    private static readonly HashSet<int> PermanentErrorCodes =
    [
        16755, // Can't extract geo keys: a stored document has invalid GeoJSON
        85,    // IndexOptionsConflict: same keys already indexed with different options
        86,    // IndexKeySpecsConflict: an index with this name already exists with different keys
        67,    // CannotCreateIndex
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(1);
        var elapsed = Stopwatch.StartNew();

        while (true)
        {
            try
            {
                await polygons.Indexes.CreateOneAsync(
                    new CreateIndexModel<PolygonDocument>(Builders<PolygonDocument>.IndexKeys.Geo2DSphere(p => p.Geometry)),
                    cancellationToken: cancellationToken);

                await objects.Indexes.CreateOneAsync(
                    new CreateIndexModel<MapObjectDocument>(Builders<MapObjectDocument>.IndexKeys.Geo2DSphere(o => o.Location)),
                    cancellationToken: cancellationToken);

                await polygons.Indexes.CreateOneAsync(UniqueClientId<PolygonDocument>(), cancellationToken: cancellationToken);
                await objects.Indexes.CreateOneAsync(UniqueClientId<MapObjectDocument>(), cancellationToken: cancellationToken);

                logger.LogInformation("MongoDB indexes are in place (2dsphere, unique clientId)");
                return;
            }
            catch (MongoCommandException ex) when (PermanentErrorCodes.Contains(ex.Code))
            {
                throw new InvalidOperationException(
                    $"MongoDB 2dsphere indexes cannot be created ({ex.CodeName}). " +
                    "Fix the stored data or the conflicting index, then restart the API.", ex);
            }
            catch (MongoAuthenticationException ex)
            {
                // Wrong credentials do not fix themselves: fail now instead of retrying for a minute.
                throw new InvalidOperationException(
                    "Cannot authenticate to MongoDB. Check the credentials in MongoDb:ConnectionString.", ex);
            }
            catch (Exception ex) when (ex is not OperationCanceledException && elapsed.Elapsed + delay < GiveUpAfter)
            {
                // Transient: MongoDB not reachable yet, timeout, network. Once GiveUpAfter is reached the
                // filter no longer matches, the exception propagates and the startup fails.
                logger.LogWarning(ex, "Could not create MongoDB indexes, retrying in {Delay}", delay);
                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxDelay.Ticks));
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Unique idempotency key. Partial (only string values), so documents saved without a client id —
    /// including data stored before this index existed — do not collide on a missing value.
    /// </summary>
    private static CreateIndexModel<T> UniqueClientId<T>() where T : IDocument => new(
        Builders<T>.IndexKeys.Ascending(MongoRepository<T>.ClientIdField),
        new CreateIndexOptions<T>
        {
            Unique = true,
            PartialFilterExpression = Builders<T>.Filter.Type(MongoRepository<T>.ClientIdField, BsonType.String),
        });
}
