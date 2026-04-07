using Messaging.Core.Abstractions;
using Messaging.Sample.Models;

namespace Messaging.Sample.Samples;

/// <summary>
/// Example consumer demonstrating:
///   1. Injecting <see cref="IMessagePublisher"/> to forward messages to another queue.
///   2. Clean consumer body — no logging/tracing boilerplate (handled by pipeline behaviors).
///   3. Throwing exceptions to exercise the retry/DLQ policy.
///
/// Replace this with your domain consumer logic.
/// </summary>
public sealed class SampleConsumer(
    IMessagePublisher publisher,
    ILogger<SampleConsumer> logger) : IMessageConsumer<SampleMessage>
{
    public async Task ConsumeAsync(SampleMessage message, CancellationToken cancellationToken)
    {
        // Simulate validation — bad payloads will be retried then DLQ'd
        if (string.IsNullOrWhiteSpace(message.Payload))
        {
            throw new InvalidOperationException($"Message {message.MessageId} has an empty payload.");
        }

        SampleConsumerLog.Handling(logger, message.Payload);

        // Simulate domain work
        await Task.Delay(50, cancellationToken);

        // Optional: publish a follow-up message to another queue
        if (message.Priority > 5)
        {
            var followUp = new SampleMessage(
                MessageId: Guid.NewGuid(),
                Timestamp: DateTimeOffset.UtcNow,
                Payload: $"Follow-up from {message.MessageId}",
                Priority: 0);

            await publisher.PublishToQueueAsync(followUp, "sample-followup-queue", cancellationToken);
        }
    }
}

/// <summary>High-performance logger messages for <see cref="SampleConsumer"/>.</summary>
internal static partial class SampleConsumerLog
{
    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Handling SampleMessage with payload: {Payload}")]
    internal static partial void Handling(ILogger logger, string payload);
}
