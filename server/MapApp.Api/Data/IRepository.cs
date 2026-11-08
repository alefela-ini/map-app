using MapApp.Api.Models;

namespace MapApp.Api.Data;

/// <summary>
/// Minimal persistence abstraction used by the services (Create, Read, Delete).
/// </summary>
public interface IRepository<T> where T : IDocument
{
    /// <summary>Returns all documents ordered by creation time.</summary>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Returns a document by id, or <c>null</c> if it does not exist or the id is malformed.</summary>
    Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts all documents. Either all of them are stored or none (best effort without transactions).
    /// A document whose <see cref="IDocument.ClientId"/> is already stored is skipped, so retries are idempotent.
    /// Returns the stored documents in the same order; for a skipped one, the document stored earlier.
    /// </summary>
    Task<IReadOnlyList<T>> InsertManyAsync(IReadOnlyCollection<T> documents, CancellationToken cancellationToken);

    /// <summary>Deletes a document by id. Returns <c>false</c> if nothing was deleted.</summary>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);

    /// <summary>Deletes all documents and returns how many were removed.</summary>
    Task<long> DeleteAllAsync(CancellationToken cancellationToken);
}
