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
public sealed class HardConsumer(
    ILogger<HardConsumer> logger) : IMessageConsumer<HardMessage>
{
    public async Task ConsumeAsync(HardMessage message, CancellationToken cancellationToken)
    {
        // Simulate validation — bad payloads will be retried then DLQ'd
        if (string.IsNullOrWhiteSpace(message.Payload))
        {
            throw new InvalidOperationException($"Message {message.MessageId} has an empty payload.");
        }

        HardConsumerLog.Handling(logger, message.Payload);

        // Simulate domain work
        await Task.Delay(1000, cancellationToken);
    }
}

/// <summary>High-performance logger messages for <see cref="SampleConsumer"/>.</summary>
internal static partial class HardConsumerLog
{
    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Handling HardMessage with payload: {Payload}")]
    internal static partial void Handling(ILogger logger, string payload);
}
