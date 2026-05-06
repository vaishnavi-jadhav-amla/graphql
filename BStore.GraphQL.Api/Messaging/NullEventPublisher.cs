namespace BStore.GraphQL.Api.Messaging;

/// <summary>No-op publisher when RabbitMQ is disabled.</summary>
public sealed class NullEventPublisher : IEventPublisher
{
    public Task PublishAsync(
        string routingKey,
        object payload,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken ct = default) =>
        Task.CompletedTask;
}
