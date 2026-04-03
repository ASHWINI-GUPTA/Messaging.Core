namespace Messaging.Core.Abstractions;

/// <summary>
/// Write-side contract for publishing messages to a queue.
/// Completely decoupled from the consumer pipeline — inject this into any consumer
/// that needs to forward or fan-out messages to the same or a different queue.
/// The underlying broker implementation (RabbitMQ, ASB, Kafka, etc.) is swappable.
/// </summary>
public interface IMessagePublisher
{
    /// <summary>
    /// Publish a message to the specified queue.
    /// </summary>
    /// <typeparam name="TMessage">Message type — must implement <see cref="IMessage"/>.</typeparam>
    /// <param name="message">The message to publish.</param>
    /// <param name="queueName">Target queue or topic name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<TMessage>(
        TMessage message,
        string queueName,
        CancellationToken cancellationToken = default)
        where TMessage : IMessage;
}
