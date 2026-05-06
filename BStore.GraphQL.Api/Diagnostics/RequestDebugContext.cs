using System.Diagnostics;

namespace BStore.GraphQL.Api.Diagnostics;

/// <summary>
/// Default scoped <see cref="IRequestDebugContext"/> implementation backed by a stopwatch and
/// thread-safe collections. Designed to be allocated once per HTTP request.
/// </summary>
public sealed class RequestDebugContext : IRequestDebugContext
{
    private readonly object _gate = new();
    private readonly HashSet<string> _dataSources = new(StringComparer.Ordinal);
    private readonly List<StageTiming> _timings = new(8);
    private readonly List<PipelineNote> _pipeline = new(8);
    private readonly List<EmptyResultReason> _empty = new(0);

    public RequestDebugContext(string correlationId)
    {
        CorrelationId = correlationId;
        OperationName = "anonymous";
    }

    public string CorrelationId { get; }
    public string OperationName { get; set; }
    public int UserId { get; set; }
    public string? ClientId { get; set; }
    public DebugLevel Level { get; set; } = DebugLevel.Basic;
    public bool IsAdmin { get; set; }

    public void RecordDataSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return;
        lock (_gate) _dataSources.Add(source);
    }

    public IDisposable Stage(string name)
    {
        var sw = Stopwatch.StartNew();
        return new StageScope(this, name, sw);
    }

    public void Note(string step, string? details = null)
    {
        if (string.IsNullOrWhiteSpace(step)) return;
        lock (_gate) _pipeline.Add(new PipelineNote(step, details, DateTimeOffset.UtcNow));
    }

    public void RecordEmptyResultReason(string code, string message)
    {
        if (string.IsNullOrWhiteSpace(code)) return;
        lock (_gate) _empty.Add(new EmptyResultReason(code, message));
    }

    public RequestDiagnosticSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new RequestDiagnosticSnapshot(
                CorrelationId,
                OperationName,
                UserId,
                ClientId,
                Level,
                IsAdmin,
                _dataSources.ToArray(),
                _timings.ToArray(),
                _pipeline.ToArray(),
                _empty.ToArray());
        }
    }

    private void Add(StageTiming timing)
    {
        lock (_gate) _timings.Add(timing);
    }

    private sealed class StageScope(RequestDebugContext owner, string stage, Stopwatch sw) : IDisposable
    {
        public void Dispose()
        {
            sw.Stop();
            owner.Add(new StageTiming(stage, sw.ElapsedMilliseconds));
        }
    }
}
