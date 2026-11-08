namespace MapApp.Api.Realtime;

/// <summary>
/// <c>GET /api/events</c>: a Server-Sent Events stream that says which collection changed
/// (<c>event: changed</c>, <c>data: polygons|objects</c>). The browser's <c>EventSource</c> reconnects by itself.
/// </summary>
public static class ChangeEventsEndpoint
{
    /// <summary>
    /// A comment line is sent when nothing happened for this long, so proxies (nginx closes idle upstream reads
    /// after 60 s) keep the connection open and a dead client is noticed on the next write.
    /// </summary>
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    public static IEndpointRouteBuilder MapChangeEvents(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/events", StreamAsync).ExcludeFromDescription();
        return endpoints;
    }

    private static async Task StreamAsync(HttpContext context, ChangeNotifier notifier, IHostApplicationLifetime lifetime)
    {
        var response = context.Response;
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no"; // nginx: pass every event on at once instead of buffering

        // End the stream when the client leaves OR the app shuts down. Graceful shutdown waits for running
        // requests, and this one never ends by itself: without ApplicationStopping, "docker stop" waited its full
        // 10 s and then killed the process. The browser reconnects to the next instance on its own.
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, lifetime.ApplicationStopping);
        var aborted = stop.Token;
        using var subscription = notifier.Subscribe();

        try
        {
            // How long the browser waits before reconnecting after the stream breaks.
            await WriteAsync(response, "retry: 3000\n\n", aborted);

            while (!aborted.IsCancellationRequested)
            {
                using var heartbeat = CancellationTokenSource.CreateLinkedTokenSource(aborted);
                heartbeat.CancelAfter(HeartbeatInterval);
                try
                {
                    foreach (var topic in await subscription.ReadAsync(heartbeat.Token))
                    {
                        await WriteAsync(response, $"event: changed\ndata: {topic}\n\n", aborted);
                    }
                }
                catch (OperationCanceledException) when (!aborted.IsCancellationRequested)
                {
                    await WriteAsync(response, ": heartbeat\n\n", aborted);
                }
            }
        }
        catch (OperationCanceledException) when (aborted.IsCancellationRequested)
        {
            // The client went away: normal end of the stream.
        }
    }

    private static async Task WriteAsync(HttpResponse response, string text, CancellationToken cancellationToken)
    {
        await response.WriteAsync(text, cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}
