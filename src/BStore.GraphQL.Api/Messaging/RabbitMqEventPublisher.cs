using System.Text.Json;
using System.Threading;
using BStore.GraphQL.Api.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace BStore.GraphQL.Api.Messaging;

/// <summary>
/// Topic-exchange publisher using a shared <see cref="IConnection"/> and per-publish channels (RabbitMQ.Client 6.x).
/// </summary>
public sealed class RabbitMqEventPublisher : IEventPublisher, IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly RabbitMqOptions _opts;
    private readonly ILogger<RabbitMqEventPublisher> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _disposed;
    private IConnection? _connection;

    public RabbitMqEventPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqEventPublisher> log)
    {
        _opts = options.Value;
        _log  = log;
    }

    public async Task PublishAsync(
        string routingKey,
        object payload,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken ct = default)
    {
        if (!_opts.Enabled)
            return;

        var body = JsonSerializer.SerializeToUtf8Bytes(payload, Json);

        await _gate.WaitAsync(ct);
        try
        {
            var connection = EnsureConnection();
            using var channel = connection.CreateModel();
            channel.ExchangeDeclare(_opts.ExchangeName, ExchangeType.Topic, durable: true);

            var props = channel.CreateBasicProperties();
            props.ContentType  = "application/json";
            props.DeliveryMode = 2;
            props.MessageId    = Guid.NewGuid().ToString("N");
            props.Timestamp    = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            if (headers is not null)
            {
                props.Headers ??= new Dictionary<string, object?>();
                foreach (var kv in headers)
                    props.Headers[kv.Key] = kv.Value;
            }

            channel.BasicPublish(_opts.ExchangeName, routingKey, props, body);

            _log.LogDebug(
                "Published event | Exchange={Exchange} | RoutingKey={RoutingKey} | Bytes={Bytes}",
                _opts.ExchangeName,
                routingKey,
                body.Length);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "RabbitMQ publish failed | RoutingKey={RoutingKey}", routingKey);
            DisposeConnection();
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private IConnection EnsureConnection()
    {
        if (_connection?.IsOpen == true)
            return _connection;

        DisposeConnection();

        var factory = new ConnectionFactory
        {
            HostName               = _opts.HostName,
            Port                   = _opts.Port,
            UserName               = _opts.UserName,
            Password               = _opts.Password,
            VirtualHost            = string.IsNullOrEmpty(_opts.VirtualHost) ? "/" : _opts.VirtualHost,
            DispatchConsumersAsync = true,
            ClientProvidedName     = _opts.ClientProvidedName
        };

        _connection = factory.CreateConnection();
        _log.LogInformation("RabbitMQ connection established to {Host}:{Port}", _opts.HostName, _opts.Port);
        return _connection;
    }

    private void DisposeConnection()
    {
        if (_connection is null)
            return;
        try
        {
            if (_connection.IsOpen)
                _connection.Close();
            _connection.Dispose();
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "RabbitMQ connection dispose");
        }
        finally
        {
            _connection = null;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _gate.Wait();
        try
        {
            DisposeConnection();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}
