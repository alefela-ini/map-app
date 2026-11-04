using MapApp.Api.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MapApp.Api.Data;

/// <summary>
/// Generic MongoDB implementation of <see cref="IRepository{T}"/>.
/// </summary>
public sealed class MongoRepository<T>(IMongoCollection<T> collection, ILogger<MongoRepository<T>> logger)
    : IRepository<T> where T : IDocument
{
    // "_id" is used directly because the documents are generic; ObjectIds also sort by creation time.
    private static readonly SortDefinition<T> DefaultSort = Builders<T>.Sort.Ascending("_id");

    /// <summary>BSON name of <see cref="IDocument.ClientId"/> (camelCase convention).</summary>
    public const string ClientIdField = "clientId";

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken) =>
        await collection.Find(FilterDefinition<T>.Empty).Sort(DefaultSort).ToListAsync(cancellationToken);

    public async Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var objectId))
        {
            return default;
        }

        return await collection.Find(ById(objectId)).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task InsertManyAsync(IReadOnlyCollection<T> documents, CancellationToken cancellationToken)
    {
        if (documents.Count == 0)
        {
            return;
        }

        try
        {
            await collection.BulkWriteAsync(
                documents.Select(ToWriteModel), new BulkWriteOptions { IsOrdered = true }, cancellationToken);
        }
        catch (MongoBulkWriteException)
        {
            // A standalone MongoDB server has no multi-document transactions, so delete the part of the batch
            // that was already written (best-effort atomicity, not a transaction). Deleting by the ids of this
            // batch is safe for idempotent items too: if an item matched a document stored by an earlier attempt,
            // that document has a different _id and is not touched.
            var ids = documents.Select(d => ObjectId.Parse(d.Id)).ToArray();
            try
            {
                await collection.DeleteManyAsync(Builders<T>.Filter.In("_id", ids), CancellationToken.None);
            }
            catch (Exception rollbackError)
            {
                logger.LogError(rollbackError, "Failed to roll back a partially inserted batch of {Count} documents", ids.Length);
            }

            throw;
        }
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var objectId))
        {
            return false;
        }

        var result = await collection.DeleteOneAsync(ById(objectId), cancellationToken);
        return result.DeletedCount > 0;
    }

    public async Task<long> DeleteAllAsync(CancellationToken cancellationToken)
    {
        var result = await collection.DeleteManyAsync(FilterDefinition<T>.Empty, cancellationToken);
        return result.DeletedCount;
    }

    /// <summary>
    /// A document with a client id is upserted: <c>$setOnInsert</c> writes it only if no document with that
    /// client id exists, so a retried Save stores nothing new. The unique index on <c>clientId</c> enforces it.
    /// </summary>
    private static WriteModel<T> ToWriteModel(T document)
    {
        if (document.ClientId is null)
        {
            return new InsertOneModel<T>(document);
        }

        return new UpdateOneModel<T>(
            Builders<T>.Filter.Eq(ClientIdField, document.ClientId),
            new BsonDocument("$setOnInsert", document.ToBsonDocument()))
        {
            IsUpsert = true,
        };
    }

    private static FilterDefinition<T> ById(ObjectId id) => Builders<T>.Filter.Eq("_id", id);
}
