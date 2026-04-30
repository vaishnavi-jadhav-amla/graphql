using System.Text;
using BStore.GraphQL.Api.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BStore.GraphQL.Api.Messaging;

/// <summary>
/// Optional background consumer: declares queue, binds to the topic exchange, acks messages, and logs payloads.
/// Extend <see cref="OnMessageDeliveredAsync"/> via subclass or replace with dispatcher registration later.
/// </summary>
public sealed class RabbitMqEventConsumerHostedService : BackgroundService
{
    private readonly RabbitMqOptions _opts;
    private readonly ILogger<RabbitMqEventConsumerHostedService> _log;

    public RabbitMqEventConsumerHostedService(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqEventConsumerHostedService> log)
    {
        _opts = options.Value;
        _log  = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opts.Enabled || !_opts.ConsumerEnabled)
        {
            _log.LogInformation("RabbitMQ consumer is disabled (RabbitMq:Enabled / ConsumerEnabled).");
            return;
        }

        await Task.Yield();

        var factory = new ConnectionFactory
        {
            HostName               = _opts.HostName,
            Port                   = _opts.Port,
            UserName               = _opts.UserName,
            Password               = _opts.Password,
            VirtualHost            = string.IsNullOrEmpty(_opts.VirtualHost) ? "/" : _opts.VirtualHost,
            DispatchConsumersAsync = true,
            ClientProvidedName     = _opts.ClientProvidedName + ":consumer"
        };

        using var connection = factory.CreateConnection();
        using var channel    = connection.CreateModel();

        channel.ExchangeDeclare(_opts.ExchangeName, ExchangeType.Topic, durable: true);
        channel.QueueDeclare(
            queue: _opts.ConsumerQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        foreach (var routingKey in _opts.ConsumerRoutingKeys)
            channel.QueueBind(_opts.ConsumerQueueName, _opts.ExchangeName, routingKey);

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (_, ea) =>
        {
            try
            {
                var preview = Encoding.UTF8.GetString(ea.Body.Span);
                if (preview.Length > 512)
                    preview = preview[..512] + "…";

                _log.LogInformation(
                    "RabbitMQ message | RoutingKey={RoutingKey} | DeliveryTag={Tag} | Preview={Preview}",
                    ea.RoutingKey,
                    ea.DeliveryTag,
                    preview);

                channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "RabbitMQ consumer handler failed; nack without requeue");
                try
                {
                    channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                }
                catch (Exception nackEx)
                {
                    _log.LogDebug(nackEx, "BasicNack failed");
                }
            }
        };

        channel.BasicConsume(_opts.ConsumerQueueName, autoAck: false, consumer);
        _log.LogInformation(
            "RabbitMQ consumer started | Queue={Queue} | Exchange={Exchange}",
            _opts.ConsumerQueueName,
            _opts.ExchangeName);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }
}
