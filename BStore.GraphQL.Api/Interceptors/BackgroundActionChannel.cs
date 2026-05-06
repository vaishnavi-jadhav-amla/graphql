using System.Threading.Channels;

namespace BStore.GraphQL.Api.Interceptors;

/// <summary>
/// Async channel for true fire-and-forget after-actions. After-actions are enqueued
/// and processed by a background hosted service, ensuring they never block the GraphQL response.
/// </summary>
public sealed class BackgroundActionChannel
{
    private readonly Channel<BackgroundActionItem> _channel =
        Channel.CreateBounded<BackgroundActionItem>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public ChannelReader<BackgroundActionItem> Reader => _channel.Reader;

    public bool TryEnqueue(BackgroundActionItem item) =>
        _channel.Writer.TryWrite(item);
}

/// <summary>
/// Represents a deferred after-action to be executed in the background.
/// </summary>
public sealed class BackgroundActionItem
{
    public required IAfterAction Action { get; init; }
    public required InterceptorContext Context { get; init; }
    public required object? Result { get; init; }
}
