using MapApp.Api.Contracts;
using MapApp.Api.Data;
using MapApp.Api.Models;
using MapApp.Api.Realtime;
using MapApp.Api.Validation;
using MongoDB.Bson;

namespace MapApp.Api.Services;

/// <summary>Application logic for polygons.</summary>
public interface IPolygonService
{
    Task<IReadOnlyList<PolygonDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<PolygonDto?> GetByIdAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Validates and stores all polygons, then returns the stored items (only this batch, in request order).
    /// </summary>
    /// <exception cref="RequestValidationException">Any polygon is invalid; nothing is stored.</exception>
    Task<IReadOnlyList<PolygonDto>> CreateManyAsync(IReadOnlyList<CreatePolygonRequest>? requests, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);

    Task<long> DeleteAllAsync(CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class PolygonService(
    IRepository<PolygonDocument> repository,
    TimeProvider timeProvider,
    IChangeNotifier notifier) : IPolygonService
{
    public async Task<IReadOnlyList<PolygonDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var documents = await repository.GetAllAsync(cancellationToken);
        return documents.Select(GeoJsonMapper.ToDto).ToList();
    }

    public async Task<PolygonDto?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(id, cancellationToken);
        return document is null ? null : GeoJsonMapper.ToDto(document);
    }

    public async Task<IReadOnlyList<PolygonDto>> CreateManyAsync(
        IReadOnlyList<CreatePolygonRequest>? requests,
        CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        RequestRules.ValidateBatch(requests, errors);
        RequestRules.ValidateTotalVertices(
            requests!.Select(r => r.Geometry?.Coordinates?.Sum(ring => ring?.Length ?? 0) ?? 0), errors);
        RequestRules.ValidateClientIds(requests!.Select(r => r.ClientId), errors);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var documents = new List<PolygonDocument>(requests!.Count);

        for (var i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            var name = RequestRules.NormalizeName(request.Name, "Polygon", $"[{i}].name", errors);
            var ring = GeometryValidator.ValidatePolygon(request.Geometry, $"[{i}].geometry", errors);

            if (ring is not null)
            {
                documents.Add(new PolygonDocument
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    Name = name,
                    ClientId = request.ClientId,
                    Geometry = GeoJsonMapper.ToMongoPolygon(ring),
                    CreatedAt = now,
                });
            }
        }

        // Validate the whole batch before writing anything.
        errors.ThrowIfAny();

        var stored = await repository.InsertManyAsync(documents, cancellationToken);
        notifier.Publish(ChangeTopics.Polygons);
        return stored.Select(GeoJsonMapper.ToDto).ToList();
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var deleted = await repository.DeleteAsync(id, cancellationToken);
        if (deleted)
        {
            notifier.Publish(ChangeTopics.Polygons);
        }

        return deleted;
    }

    public async Task<long> DeleteAllAsync(CancellationToken cancellationToken)
    {
        var count = await repository.DeleteAllAsync(cancellationToken);
        if (count > 0)
        {
            notifier.Publish(ChangeTopics.Polygons);
        }

        return count;
    }
}
