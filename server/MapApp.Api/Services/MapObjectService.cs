using MapApp.Api.Contracts;
using MapApp.Api.Data;
using MapApp.Api.Models;
using MapApp.Api.Realtime;
using MapApp.Api.Validation;
using MongoDB.Bson;

namespace MapApp.Api.Services;

/// <summary>Application logic for map objects (markers and custom symbols).</summary>
public interface IMapObjectService
{
    Task<IReadOnlyList<MapObjectDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<MapObjectDto?> GetByIdAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Validates and stores all objects, then returns the stored items (only this batch, in request order).
    /// </summary>
    /// <exception cref="RequestValidationException">Any object is invalid; nothing is stored.</exception>
    Task<IReadOnlyList<MapObjectDto>> CreateManyAsync(IReadOnlyList<CreateMapObjectRequest>? requests, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);

    Task<long> DeleteAllAsync(CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class MapObjectService(
    IRepository<MapObjectDocument> repository,
    TimeProvider timeProvider,
    IChangeNotifier notifier) : IMapObjectService
{
    public async Task<IReadOnlyList<MapObjectDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var documents = await repository.GetAllAsync(cancellationToken);
        return documents.Select(GeoJsonMapper.ToDto).ToList();
    }

    public async Task<MapObjectDto?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(id, cancellationToken);
        return document is null ? null : GeoJsonMapper.ToDto(document);
    }

    public async Task<IReadOnlyList<MapObjectDto>> CreateManyAsync(
        IReadOnlyList<CreateMapObjectRequest>? requests,
        CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        RequestRules.ValidateBatch(requests, errors);
        RequestRules.ValidateClientIds(requests!.Select(r => r.ClientId), errors);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var documents = new List<MapObjectDocument>(requests!.Count);

        for (var i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            var symbol = request.Symbol ?? MapObjectSymbol.Marker;
            if (!Enum.IsDefined(symbol))
            {
                errors.Add($"[{i}].symbol", "Unknown symbol.");
            }

            var name = RequestRules.NormalizeName(request.Name, symbol.ToString(), $"[{i}].name", errors);
            var position = GeometryValidator.ValidatePoint(request.Geometry, $"[{i}].geometry", errors);

            if (position is not null)
            {
                documents.Add(new MapObjectDocument
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    Name = name,
                    ClientId = request.ClientId,
                    Symbol = symbol,
                    Location = GeoJsonMapper.ToMongoPoint(position.Value),
                    CreatedAt = now,
                });
            }
        }

        // Validate the whole batch before writing anything.
        errors.ThrowIfAny();

        var stored = await repository.InsertManyAsync(documents, cancellationToken);
        notifier.Publish(ChangeTopics.Objects);
        return stored.Select(GeoJsonMapper.ToDto).ToList();
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var deleted = await repository.DeleteAsync(id, cancellationToken);
        if (deleted)
        {
            notifier.Publish(ChangeTopics.Objects);
        }

        return deleted;
    }

    public async Task<long> DeleteAllAsync(CancellationToken cancellationToken)
    {
        var count = await repository.DeleteAllAsync(cancellationToken);
        if (count > 0)
        {
            notifier.Publish(ChangeTopics.Objects);
        }

        return count;
    }
}
