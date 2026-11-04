using MapApp.Api.Data;
using MapApp.Api.Models;

namespace MapApp.Api.Tests;

/// <summary>Thread-safe in-memory <see cref="IRepository{T}"/> for tests.</summary>
public sealed class InMemoryRepository<T> : IRepository<T> where T : IDocument
{
    private readonly List<T> _items = [];
    private readonly Lock _lock = new();

    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<T>>(_items.ToList());
        }
    }

    public Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return Task.FromResult(_items.FirstOrDefault(i => i.Id == id));
        }
    }

    public Task InsertManyAsync(IReadOnlyCollection<T> documents, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            // Same contract as MongoRepository: a document whose client id is already stored is skipped.
            _items.AddRange(documents.Where(d =>
                d.ClientId is null || !_items.Any(existing => existing.ClientId == d.ClientId)));
        }

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return Task.FromResult(_items.RemoveAll(i => i.Id == id) > 0);
        }
    }

    public Task<long> DeleteAllAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var count = _items.Count;
            _items.Clear();
            return Task.FromResult((long)count);
        }
    }
}
