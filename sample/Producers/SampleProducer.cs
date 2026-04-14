using Messaging.Core.Abstractions;
using Messaging.Sample.Models;

namespace Messaging.Sample.Producers;

#pragma warning disable CA1848 // Use the LoggerMessage delegates
public sealed class SampleProducer(
    IMessagePublisher publisher,
    ILogger<SampleProducer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SampleProducer starting...");

        // Give the consumer a moment to start up and connect to the broker
        await Task.Delay(15000, stoppingToken);

        int count = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            count++;

            var message = new SampleMessage(
                MessageId: Guid.NewGuid(),
                Timestamp: DateTimeOffset.UtcNow,
                Payload: $"Periodic message #{count} from SampleProducer for SampleConsumer",
                Priority: (count % 10 == 0) ? 9 : 1
            );

            var hardMessage = new HardMessage(
                MessageId: Guid.NewGuid(),
                Timestamp: DateTimeOffset.UtcNow,
                Payload: $"Periodic message #{count} from SampleProducer for HardConsumer"
            );

            try
            {
#pragma warning disable CA1873 // Avoid potentially expensive logging
                logger.LogInformation(
                    "Publishing SampleMessage {MessageId} (Priority: {Priority})",
                    message.MessageId,
                    message.Priority);

                await publisher.PublishAsync(message, "sample-exchange", "sample-queue", stoppingToken);

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

                logger.LogInformation(
                    "Publishing HardMessage {MessageId}",
                    hardMessage.MessageId);

                await publisher.PublishAsync(hardMessage, "sample-exchange", "ashwini-queue", stoppingToken);
#pragma warning restore CA1873 // Avoid potentially expensive logging
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to publish message {MessageId}",
                    message.MessageId);
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
#pragma warning restore CA1848 // Use the LoggerMessage delegates
