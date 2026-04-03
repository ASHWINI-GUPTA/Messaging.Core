using Messaging.Core.Abstractions;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Messaging.Core.Pipeline;

/// <summary>
/// Pipeline behavior that logs message processing start, completion, and duration.
/// Registered globally — every consumer gets structured log entries automatically
/// without any logging code in the consumer itself.
/// Logged fields: MessageId, MessageType, Queue, ElapsedMs (on completion).
/// </summary>
public sealed class LoggingBehavior<TMessage>(
    ILogger<LoggingBehavior<TMessage>> logger) : IConsumerPipelineBehavior<TMessage>
    where TMessage : IMessage
{
    public async Task HandleAsync(TMessage message, Func<Task> next, CancellationToken cancellationToken)
    {
        LoggingBehaviorLog.Processing(logger, typeof(TMessage).Name, message.MessageId);

        var sw = Stopwatch.StartNew();
        try
        {
            await next();
            sw.Stop();

            LoggingBehaviorLog.Processed(logger, typeof(TMessage).Name, message.MessageId, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            LoggingBehaviorLog.Failed(logger, ex, typeof(TMessage).Name, message.MessageId, sw.ElapsedMilliseconds);
            throw;
        }
    }
}

/// <summary>High-performance logger messages for <see cref="LoggingBehavior{TMessage}"/>.</summary>
internal static partial class LoggingBehaviorLog
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Processing {MessageType} {MessageId}")]
    internal static partial void Processing(ILogger logger, string messageType, Guid messageId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Processed {MessageType} {MessageId} in {ElapsedMs}ms")]
    internal static partial void Processed(ILogger logger, string messageType, Guid messageId, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed {MessageType} {MessageId} after {ElapsedMs}ms")]
    internal static partial void Failed(ILogger logger, Exception ex, string messageType, Guid messageId, long elapsedMs);
}
