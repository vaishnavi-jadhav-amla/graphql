using Microsoft.Extensions.Hosting;

namespace BStore.GraphQL.Api.Messaging;

/// <summary>Closes the shared RabbitMQ connection on host shutdown.</summary>
public sealed class RabbitMqPublisherShutdownHostedService(RabbitMqEventPublisher publisher) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        publisher.Dispose();
        return Task.CompletedTask;
    }
}
