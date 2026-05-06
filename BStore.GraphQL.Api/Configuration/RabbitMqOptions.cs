namespace BStore.GraphQL.Api.Configuration;

/// <summary>RabbitMQ publish / optional subscribe settings. Bind from <c>"RabbitMq"</c>.</summary>
public sealed class RabbitMqOptions
{
    public const string Section = "RabbitMq";

    /// <summary>When false, <see cref="Messaging.IEventPublisher"/> is a no-op.</summary>
    public bool Enabled { get; init; }

    public string HostName { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string UserName { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public string VirtualHost { get; init; } = "/";

    /// <summary>Durable topic exchange for integration events.</summary>
    public string ExchangeName { get; init; } = "bstore.graphql.events";

    /// <summary>Application id appended to message headers for tracing.</summary>
    public string ClientProvidedName { get; init; } = "BStore.GraphQL.Api";

    /// <summary>When true, hosts <see cref="Messaging.RabbitMqEventConsumerHostedService"/> to receive events.</summary>
    public bool ConsumerEnabled { get; init; }

    /// <summary>Queue bound to <see cref="ExchangeName"/> for this service instance.</summary>
    public string ConsumerQueueName { get; init; } = "bstore.graphql.api";

    /// <summary>Routing keys to bind (topic). Default binds all under <c>bstore.graphql.#</c>.</summary>
    public string[] ConsumerRoutingKeys { get; init; } = ["bstore.graphql.#"];
}
