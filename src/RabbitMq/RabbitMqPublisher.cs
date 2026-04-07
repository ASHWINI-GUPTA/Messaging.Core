using Messaging.Core.Abstractions;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Messaging.Core.RabbitMq;

/// <summary>
/// RabbitMQ implementation of <see cref="IMessagePublisher"/>.
/// Serializes messages to JSON and publishes them to an exchange with a routing key,
/// or directly to a queue via the default exchange.
/// Reuses the shared <see cref="IRabbitMqConnection"/> — a new channel is created per publish call
/// to remain thread-safe (channels are not thread-safe in RabbitMQ.Client).
/// </summary>
public sealed class RabbitMqPublisher(
    IRabbitMqConnection connection,
    ILogger<RabbitMqPublisher> logger) : IMessagePublisher
{
    /// <inheritdoc />
    public async Task PublishAsync<TMessage>(
        TMessage message,
        string exchangeName,
        string routingKey,
        CancellationToken cancellationToken = default)
        where TMessage : IMessage
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);
        ArgumentNullException.ThrowIfNull(message);

        var body = SerializeMessage(message);
        var props = CreateBasicProperties<TMessage>(message);

        await using var channel = await connection.CreateChannelAsync(cancellationToken);

        await channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: cancellationToken);

        RabbitMqPublisherLog.PublishedToExchange(logger, typeof(TMessage).Name, message.MessageId, exchangeName, routingKey);
    }

    /// <inheritdoc />
    public async Task PublishToQueueAsync<TMessage>(
        TMessage message,
        string queueName,
        CancellationToken cancellationToken = default)
        where TMessage : IMessage
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(message);

        var body = SerializeMessage(message);
        var props = CreateBasicProperties<TMessage>(message);

        await using var channel = await connection.CreateChannelAsync(cancellationToken);

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: cancellationToken);

        RabbitMqPublisherLog.PublishedToQueue(logger, typeof(TMessage).Name, message.MessageId, queueName);
    }

    private static BasicProperties CreateBasicProperties<TMessage>(TMessage message)
        where TMessage : IMessage
    {
        return new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            MessageId = message.MessageId.ToString(),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?>
            {
                ["x-message-type"] = typeof(TMessage).Name,
                ["x-published-at"] = DateTimeOffset.UtcNow.ToString("O"),
            }
        };
    }

    private static ReadOnlyMemory<byte> SerializeMessage<TMessage>(TMessage message)
        where TMessage : IMessage
    {
        var json = JsonSerializer.Serialize(message);
        return Encoding.UTF8.GetBytes(json);
    }
}

/// <summary>High-performance logger messages for <see cref="RabbitMqPublisher"/>.</summary>
internal static partial class RabbitMqPublisherLog
{
    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Published {MessageType} {MessageId} to exchange {ExchangeName} with routing key {RoutingKey}")]
    internal static partial void PublishedToExchange(
        ILogger logger, string messageType, Guid messageId, string exchangeName, string routingKey);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Published {MessageType} {MessageId} to queue {QueueName}")]
    internal static partial void PublishedToQueue(
        ILogger logger, string messageType, Guid messageId, string queueName);
}
