namespace Messaging.Core.Abstractions;

/// <summary>
/// Internal marker interface so the DI container can resolve all registered consumer services
/// without knowing the generic type argument.
/// </summary>
public interface IConsumerService
{
    /// <summary>
    /// Gets the name of the queue that this consumer service listens to.
    /// </summary>
    string QueueName { get; }

    /// <summary>
    /// Gets the exchange name this consumer is bound to, or null for direct queue consumption.
    /// </summary>
    string? ExchangeName { get; }

    /// <summary>
    /// Starts the consumer service asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the start operation.</param>
    /// <returns>A task that represents the asynchronous start operation.</returns>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the consumer service asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the stop operation.</param>
    /// <returns>A task that represents the asynchronous stop operation.</returns>
    Task StopAsync(CancellationToken cancellationToken);
}
