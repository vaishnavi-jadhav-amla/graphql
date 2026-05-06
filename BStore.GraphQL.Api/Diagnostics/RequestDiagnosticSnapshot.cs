namespace BStore.GraphQL.Api.Diagnostics;

/// <summary>Immutable view of <see cref="IRequestDebugContext"/> at request completion.</summary>
public sealed record RequestDiagnosticSnapshot(
    string CorrelationId,
    string OperationName,
    int UserId,
    string? ClientId,
    DebugLevel Level,
    bool IsAdmin,
    IReadOnlyList<string> DataSources,
    IReadOnlyList<StageTiming> Timings,
    IReadOnlyList<PipelineNote> PipelineNotes,
    IReadOnlyList<EmptyResultReason> EmptyReasons);

public sealed record StageTiming(string Stage, long ElapsedMs);

public sealed record PipelineNote(string Step, string? Details, DateTimeOffset At);

public sealed record EmptyResultReason(string Code, string Message);
