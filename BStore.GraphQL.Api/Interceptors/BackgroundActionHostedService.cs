namespace BStore.GraphQL.Api.Interceptors;

/// <summary>
/// Background hosted service that processes deferred after-actions from the
/// <see cref="BackgroundActionChannel"/>. Runs continuously, draining the channel.
/// Failures are logged but never crash the host.
/// </summary>
public sealed class BackgroundActionHostedService : BackgroundService
{
    private readonly BackgroundActionChannel _channel;
    private readonly ILogger<BackgroundActionHostedService> _logger;

    public BackgroundActionHostedService(
        BackgroundActionChannel channel,
        ILogger<BackgroundActionHostedService> logger)
    {
        _channel = channel;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackgroundActionHostedService started");

        await foreach (var item in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await item.Action.ExecuteAsync(item.Context, item.Result, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Background after-action {Action} failed for {Operation}",
                    item.Action.GetType().Name, item.Context.OperationName);
            }
        }

        _logger.LogInformation("BackgroundActionHostedService stopped");
    }
}
