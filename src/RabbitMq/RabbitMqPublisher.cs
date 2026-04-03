using Messaging.Core.Abstractions;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Messaging.Core.RabbitMq;

/// <summary>
/// RabbitMQ implementation of <see cref="IMessagePublisher"/>.
/// Serializes messages to JSON and publishes them to the specified queue.
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
        string queueName,
        CancellationToken cancellationToken = default)
        where TMessage : IMessage
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(message);

        var body = SerializeMessage(message);

        await using var channel = await connection.CreateChannelAsync(cancellationToken);

        var props = new BasicProperties
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

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: cancellationToken);

        RabbitMqPublisherLog.Published(logger, typeof(TMessage).Name, message.MessageId, queueName);
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
        Message = "Published {MessageType} {MessageId} to queue {QueueName}")]
    internal static partial void Published(
        ILogger logger, string messageType, Guid messageId, string queueName);
}
