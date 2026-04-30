namespace BStore.GraphQL.Api.Messaging;

/// <summary>
/// Publishes integration events to RabbitMQ (or no-ops when messaging is disabled).
/// Routing keys should follow a stable convention, e.g. <c>bstore.graphql.mutation.bstoreUpdate</c>.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes JSON payload to the configured topic exchange with <paramref name="routingKey"/>.
    /// </summary>
    Task PublishAsync(
        string routingKey,
        object payload,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken ct = default);
}
