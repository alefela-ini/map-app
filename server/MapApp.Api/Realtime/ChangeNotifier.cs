using System.Collections.Concurrent;
using System.Threading.Channels;

namespace MapApp.Api.Realtime;

/// <summary>Names of the collections that clients can be told to reload.</summary>
public static class ChangeTopics
{
    public const string Polygons = "polygons";
    public const string Objects = "objects";
}

/// <summary>Tells connected clients that a collection changed, so they reload it.</summary>
public interface IChangeNotifier
{
    /// <summary>Signals that <paramref name="topic"/> (see <see cref="ChangeTopics"/>) was written.</summary>
    void Publish(string topic);
}

/// <summary>
/// In-process publish/subscribe for "a collection changed" events, consumed by the Server-Sent Events endpoint.
/// </summary>
/// <remarks>
/// Events carry no data: a client that receives one reloads the collection. So each subscriber keeps only a set
/// of "dirty" topics, not a queue: repeated changes of one collection merge into one pending reload, memory stays
/// bounded (one entry per collection) for a slow client, and no collection can be dropped while another one keeps
/// changing (a bounded queue that drops the oldest event could lose the last "objects" behind many "polygons").
/// Only clients connected to THIS process are notified; several API instances would need a shared channel
/// (for example Redis pub/sub or MongoDB change streams).
/// </remarks>
public sealed class ChangeNotifier : IChangeNotifier
{
    private readonly ConcurrentDictionary<Guid, Subscription> _subscribers = new();

    public void Publish(string topic)
    {
        foreach (var subscriber in _subscribers.Values)
        {
            subscriber.MarkDirty(topic);
        }
    }

    /// <summary>Registers a subscriber; dispose the result to unregister it.</summary>
    public Subscription Subscribe()
    {
        var id = Guid.NewGuid();
        var subscription = new Subscription(() => _subscribers.TryRemove(id, out _));
        _subscribers[id] = subscription;
        return subscription;
    }

    public int SubscriberCount => _subscribers.Count;

    /// <summary>One subscriber: the topics changed since it last read, and a wake-up signal.</summary>
    public sealed class Subscription(Action unsubscribe) : IDisposable
    {
        private readonly ConcurrentDictionary<string, byte> _dirty = new(StringComparer.Ordinal);

        // Capacity 1: a pending wake-up already covers every later change, so extra signals are dropped.
        private readonly Channel<bool> _wakeUp = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        });

        internal void MarkDirty(string topic)
        {
            _dirty[topic] = 0;
            _wakeUp.Writer.TryWrite(true);
        }

        /// <summary>
        /// Waits until at least one topic changed and returns all changed topics (each once).
        /// </summary>
        public async Task<IReadOnlyList<string>> ReadAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                await _wakeUp.Reader.ReadAsync(cancellationToken);
                var topics = _dirty.Keys.Where(topic => _dirty.TryRemove(topic, out _)).Order(StringComparer.Ordinal).ToList();
                if (topics.Count > 0)
                {
                    return topics;
                }
            }
        }

        public void Dispose() => unsubscribe();
    }
}
