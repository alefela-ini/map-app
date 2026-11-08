using MapApp.Api.Realtime;

namespace MapApp.Api.Tests;

/// <summary>The in-process "collection changed" notifier behind the Server-Sent Events stream.</summary>
public sealed class ChangeNotifierTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task A_slow_subscriber_never_loses_a_collection_and_gets_each_one_once()
    {
        var notifier = new ChangeNotifier();
        using var subscription = notifier.Subscribe();

        // The subscriber does not read while many changes happen: the last "objects" is followed by
        // a burst of "polygons" (a bounded drop-oldest queue would lose it).
        notifier.Publish(ChangeTopics.Objects);
        for (var i = 0; i < 100; i++)
        {
            notifier.Publish(ChangeTopics.Polygons);
        }

        using var cts = new CancellationTokenSource(Timeout);
        var topics = await subscription.ReadAsync(cts.Token);

        Assert.Equal([ChangeTopics.Objects, ChangeTopics.Polygons], topics);
    }

    [Fact]
    public async Task Every_subscriber_is_notified_and_a_disposed_one_is_removed()
    {
        var notifier = new ChangeNotifier();
        using var first = notifier.Subscribe();
        var second = notifier.Subscribe();
        second.Dispose();

        notifier.Publish(ChangeTopics.Objects);

        using var cts = new CancellationTokenSource(Timeout);
        Assert.Equal([ChangeTopics.Objects], await first.ReadAsync(cts.Token));
        Assert.Equal(1, notifier.SubscriberCount);
    }

    [Fact]
    public async Task Reading_waits_for_the_next_change()
    {
        var notifier = new ChangeNotifier();
        using var subscription = notifier.Subscribe();
        using var cts = new CancellationTokenSource(Timeout);

        var read = subscription.ReadAsync(cts.Token);
        Assert.False(read.IsCompleted);

        notifier.Publish(ChangeTopics.Polygons);
        Assert.Equal([ChangeTopics.Polygons], await read);
    }
}
