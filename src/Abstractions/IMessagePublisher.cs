namespace Messaging.Core.Abstractions;

/// <summary>
/// Write-side contract for publishing messages.
/// Completely decoupled from the consumer pipeline — inject this into any consumer
/// that needs to forward or fan-out messages.
/// The underlying broker implementation (RabbitMQ, ASB, Kafka, etc.) is swappable.
/// </summary>
public interface IMessagePublisher
{
    /// <summary>
    /// Publish a message to the specified exchange with a routing key.
    /// This is the primary publishing pattern — messages are routed from the exchange
    /// to bound queues based on the routing key.
    /// </summary>
    /// <typeparam name="TMessage">Message type — must implement <see cref="IMessage"/>.</typeparam>
    /// <param name="message">The message to publish.</param>
    /// <param name="exchangeName">Target exchange name.</param>
    /// <param name="routingKey">Routing key for exchange-based routing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<TMessage>(
        TMessage message,
        string exchangeName,
        string routingKey,
        CancellationToken cancellationToken = default)
        where TMessage : IMessage;

    /// <summary>
    /// Publish a message directly to the specified queue (using the default exchange).
    /// Use this only when messages do not need exchange-based routing.
    /// </summary>
    /// <typeparam name="TMessage">Message type — must implement <see cref="IMessage"/>.</typeparam>
    /// <param name="message">The message to publish.</param>
    /// <param name="queueName">Target queue name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishToQueueAsync<TMessage>(
        TMessage message,
        string queueName,
        CancellationToken cancellationToken = default)
        where TMessage : IMessage;
}
