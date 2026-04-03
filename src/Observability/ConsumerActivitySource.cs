using System.Diagnostics;

namespace Messaging.Core.Observability;

/// <summary>
/// Central OpenTelemetry <see cref="ActivitySource"/> for the consumer template.
/// All consumer spans are created here and follow the OpenTelemetry messaging semantic conventions.
/// The source name "Consumer.Template" must be registered in AddOpenTelemetry().WithTracing().
/// </summary>
public static class ConsumerActivitySource
{
    private static readonly ActivitySource Source = new("Consumer.Template", "1.0.0");

    /// <summary>
    /// Starts a new tracing span for a single message dispatch.
    /// Populates messaging semantic convention attributes.
    /// </summary>
    public static Activity? StartActivity<TMessage>(
        string queueName,
        string messageId,
        string? correlationId = null)
    {
        var activity = Source.StartActivity(
            name: $"{queueName} process",
            kind: ActivityKind.Consumer);

        if (activity is null) return null;

        activity.SetTag("messaging.system", "rabbitmq");
        activity.SetTag("messaging.operation", "process");
        activity.SetTag("messaging.destination", queueName);
        activity.SetTag("messaging.destination.kind", "queue");
        activity.SetTag("messaging.message.id", messageId);
        activity.SetTag("messaging.message.type", typeof(TMessage).Name);

        if (!string.IsNullOrEmpty(correlationId))
        {
            activity.SetTag("messaging.message.correlation_id", correlationId);
        }

        return activity;
    }
}
