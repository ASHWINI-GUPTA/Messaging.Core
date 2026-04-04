using Messaging.Core.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Messaging.Core.Hosting;

/// <summary>
/// Hosted service that starts and stops all registered <see cref="IConsumerService"/> instances.
/// Runs as a long-lived background service for the lifetime of the application.
/// On SIGTERM: StopAsync is called by the host, which gives each consumer up to
/// <c>ShutdownTimeoutSeconds</c> to drain in-flight messages before the channel is closed.
/// </summary>
internal sealed class ConsumerBackgroundService(
    IEnumerable<IConsumerService> consumers,
    ILogger<ConsumerBackgroundService> logger) : BackgroundService
{
    private readonly IReadOnlyList<IConsumerService> _consumers = consumers.ToList();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_consumers.Count == 0)
        {
            ConsumerBackgroundServiceLog.NoConsumers(logger);
            return;
        }

        ConsumerBackgroundServiceLog.Starting(logger, _consumers.Count);

        // Start all consumers concurrently
        var startTasks = _consumers.Select(c => StartConsumerAsync(c, stoppingToken));
        await Task.WhenAll(startTasks);

        ConsumerBackgroundServiceLog.AllStarted(logger);

        // Hold until the host requests cancellation (SIGTERM / Ctrl+C)
        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        ConsumerBackgroundServiceLog.ShutdownSignal(logger);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        var stopTasks = _consumers.Select(c => StopConsumerAsync(c, cancellationToken));
        await Task.WhenAll(stopTasks);

        await base.StopAsync(cancellationToken);
        ConsumerBackgroundServiceLog.AllStopped(logger);
    }

    private async Task StartConsumerAsync(IConsumerService consumer, CancellationToken cancellationToken)
    {
        try
        {
            ConsumerBackgroundServiceLog.StartingQueue(logger, consumer.QueueName);
            await consumer.StartAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Normal on host shutdown during startup
        }
        catch (Exception ex)
        {
            ConsumerBackgroundServiceLog.StartFailed(logger, ex, consumer.QueueName);
            throw;
        }
    }

    private async Task StopConsumerAsync(IConsumerService consumer, CancellationToken cancellationToken)
    {
        try
        {
            await consumer.StopAsync(cancellationToken);
            ConsumerBackgroundServiceLog.Stopped(logger, consumer.QueueName);
        }
        catch (Exception ex)
        {
            ConsumerBackgroundServiceLog.StopFailed(logger, ex, consumer.QueueName);
        }
    }
}

/// <summary>High-performance logger messages for <see cref="ConsumerBackgroundService"/>.</summary>
internal static partial class ConsumerBackgroundServiceLog
{
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "No consumers are registered. ConsumerBackgroundService has nothing to do.")]
    internal static partial void NoConsumers(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Starting {Count} consumer(s)...")]
    internal static partial void Starting(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "All consumers started. Waiting for shutdown signal.")]
    internal static partial void AllStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Shutdown signal received. Draining consumers...")]
    internal static partial void ShutdownSignal(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Starting consumer for queue '{Queue}'")]
    internal static partial void StartingQueue(ILogger logger, string queue);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Consumer for queue '{Queue}' failed to start")]
    internal static partial void StartFailed(ILogger logger, Exception ex, string queue);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Consumer for queue '{Queue}' stopped")]
    internal static partial void Stopped(ILogger logger, string queue);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Error stopping consumer for queue '{Queue}'")]
    internal static partial void StopFailed(ILogger logger, Exception ex, string queue);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "All consumers stopped cleanly.")]
    internal static partial void AllStopped(ILogger logger);
}
