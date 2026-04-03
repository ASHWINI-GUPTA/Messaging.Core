using Messaging.Core.Abstractions;
using System.Diagnostics;

namespace Messaging.Core.Pipeline;

/// <summary>
/// Pipeline behavior that creates an OpenTelemetry span around every message dispatch.
/// The span covers the entire processing duration including all inner behaviors and the consumer.
/// Span status is set to Ok on success and Error (with exception message) on failure.
/// Registered globally — every consumer is automatically traced without any OTel code in the consumer.
/// </summary>
public sealed class TracingBehavior<TMessage> : IConsumerPipelineBehavior<TMessage>
    where TMessage : IMessage
{
    public async Task HandleAsync(TMessage message, Func<Task> next, CancellationToken cancellationToken)
    {
        // Activity is already started by RabbitMqConsumerService — this behavior enriches it
        var activity = Activity.Current;

        try
        {
            await next();
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.type", ex.GetType().FullName);
            activity?.SetTag("exception.message", ex.Message);
            throw;
        }
    }
}
