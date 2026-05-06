using System.Collections.Concurrent;

namespace BStore.GraphQL.Api.Interceptors.Samples;

/// <summary>
/// Operation-level rate limiting interceptor. Limits specific mutations
/// to a configurable number of calls per time window, per user.
/// </summary>
public sealed class RateLimitBeforeInterceptor : IBeforeAction
{
    private static readonly ConcurrentDictionary<string, OperationTracker> Trackers = new();
    private readonly ILogger<RateLimitBeforeInterceptor> _logger;

    public RateLimitBeforeInterceptor(ILogger<RateLimitBeforeInterceptor> logger) => _logger = logger;

    public IReadOnlySet<string> Operations { get; } = new HashSet<string>
    {
        "bStoreCreate", "bStoreCopy", "bStoreUploadFile"
    };

    public int Order => 50;

    public Task ExecuteAsync(InterceptorContext context, CancellationToken ct)
    {
        var userId = context.DebugContext.UserId;
        var key = $"{context.OperationName}:{userId}";

        var tracker = Trackers.GetOrAdd(key, _ => new OperationTracker());
        if (!tracker.TryAcquire(maxPerMinute: 5))
        {
            _logger.LogWarning(
                "Operation rate limit exceeded: {Operation} for user {UserId}",
                context.OperationName, userId);
            throw new InvalidOperationException(
                $"Rate limit exceeded for operation '{context.OperationName}'. Please wait before retrying.");
        }

        return Task.CompletedTask;
    }

    private sealed class OperationTracker
    {
        private readonly object _lock = new();
        private readonly Queue<DateTime> _timestamps = new();

        public bool TryAcquire(int maxPerMinute)
        {
            lock (_lock)
            {
                var cutoff = DateTime.UtcNow.AddMinutes(-1);
                while (_timestamps.Count > 0 && _timestamps.Peek() < cutoff)
                    _timestamps.Dequeue();

                if (_timestamps.Count >= maxPerMinute)
                    return false;

                _timestamps.Enqueue(DateTime.UtcNow);
                return true;
            }
        }
    }
}
