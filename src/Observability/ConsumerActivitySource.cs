using System.Diagnostics;

namespace Messaging.Core.Observability;

/// <summary>
/// Central OpenTelemetry <see cref="ActivitySource"/> for the consumer template.
/// All consumer spans are created here and follow the OpenTelemetry messaging semantic conventions.
/// The source name "Messaging.Core" must be registered in AddOpenTelemetry().WithTracing().
/// </summary>
public static class ConsumerActivitySource
{
    private static ActivitySource _source = new("Messaging.Core", "1.0.0");
    
    /// <summary>
    /// Initializes the ActivitySource with a custom name provided by the implementer.
    /// </summary>
    public static void Initialize(string sourceName, string version = "1.0.0")
    {
        _source = new ActivitySource(sourceName, version);
    }

    /// <summary>
    /// Starts a new tracing span for a single message dispatch.
    /// Populates messaging semantic convention attributes.
    /// </summary>
    public static Activity? StartActivity<TMessage>(
        string queueName,
        string messageId,
        string? correlationId = null)
    {
        var activity = _source.StartActivity(
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
