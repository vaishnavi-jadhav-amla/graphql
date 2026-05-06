using System.Collections.Concurrent;

namespace BStore.GraphQL.Api.Diagnostics;

/// <summary>
/// Lock-free per-provider counters with a small ring buffer for percentile estimation (ADR-025).
/// </summary>
public sealed class ProviderHealthTracker : IProviderHealthTracker
{
    private const int Bucket = 256;
    private readonly ConcurrentDictionary<string, ProviderState> _state = new(StringComparer.Ordinal);

    public void Record(string provider, bool success, long elapsedMs, string? error = null)
    {
        if (string.IsNullOrWhiteSpace(provider)) return;

        var s = _state.GetOrAdd(provider, _ => new ProviderState());
        Interlocked.Increment(ref s.Calls);
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (success)
        {
            Interlocked.Exchange(ref s.LastSuccessUnixMs, nowMs);
        }
        else
        {
            Interlocked.Increment(ref s.Errors);
            Interlocked.Exchange(ref s.LastErrorUnixMs, nowMs);
            s.LastError = error;
        }

        var slot = (int)((uint)Interlocked.Increment(ref s.Cursor) % Bucket);
        s.Latencies[slot] = elapsedMs;
    }

    public IReadOnlyList<ProviderHealth> Snapshot()
    {
        var list = new List<ProviderHealth>(_state.Count);
        foreach (var kv in _state)
        {
            var s = kv.Value;
            var copy = s.Latencies.Where(v => v >= 0).OrderBy(v => v).ToArray();
            long p50 = copy.Length == 0 ? 0 : copy[(int)(copy.Length * 0.50)];
            long p95 = copy.Length == 0 ? 0 : copy[Math.Min(copy.Length - 1, (int)(copy.Length * 0.95))];
            list.Add(new ProviderHealth(
                kv.Key,
                Interlocked.Read(ref s.Calls),
                Interlocked.Read(ref s.Errors),
                Interlocked.Read(ref s.LastSuccessUnixMs),
                Interlocked.Read(ref s.LastErrorUnixMs),
                s.LastError,
                p50,
                p95));
        }
        return list;
    }

    private sealed class ProviderState
    {
        public long Calls;
        public long Errors;
        public long LastSuccessUnixMs;
        public long LastErrorUnixMs;
        public string? LastError;
        public int Cursor = -1;
        public readonly long[] Latencies = Enumerable.Repeat(-1L, Bucket).ToArray();
    }
}
