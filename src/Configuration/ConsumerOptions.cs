using System.ComponentModel.DataAnnotations;

namespace Messaging.Core.Configuration;

/// <summary>
/// Base consumer options shared across all broker implementations.
/// Bind from the "Consumer" section in appsettings.json.
/// All values can be overridden via environment variables using double-underscore notation:
///   Consumer__ExchangeName, Consumer__RoutingKey, Consumer__QueueName, etc.
/// </summary>
public sealed class ConsumerOptions : IValidatableObject
{
    public const string SectionName = "Consumer";

    /// <summary>
    /// Exchange name to bind the queue to.
    /// This is the primary way to configure consumption — messages flow from exchange → routing key → queue.
    /// </summary>
    public string? ExchangeName { get; set; }

    /// <summary>
    /// Routing key for the exchange → queue binding.
    /// Required when <see cref="ExchangeName"/> is set.
    /// When <see cref="QueueName"/> is not explicitly set, the routing key is also used as the queue name.
    /// </summary>
    public string? RoutingKey { get; set; }

    /// <summary>
    /// Exchange type when auto-declaring: direct, topic, fanout, headers.
    /// Only applies when <see cref="ExchangeName"/> is set. Default: "direct".
    /// </summary>
    public string ExchangeType { get; set; } = "direct";

    /// <summary>
    /// Name of the queue to consume from.
    /// When using exchange-based binding, this defaults to <see cref="RoutingKey"/> if not explicitly set.
    /// For direct queue consumption (no exchange), this is required.
    /// </summary>
    public string QueueName { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of messages processed concurrently by this consumer instance.
    /// Maps to RabbitMQ prefetch count (QoS). Default: 1 (safest for ordered processing).
    /// </summary>
    [Range(1, 1000)]
    public ushort ConcurrencyLimit { get; set; } = 1;

    /// <summary>
    /// Maximum retry attempts before routing a message to the Dead-Letter Queue.
    /// Default: 3.
    /// </summary>
    [Range(0, 100)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay in milliseconds for exponential back-off between retries.
    /// Actual delay = RetryBaseDelayMs * 2^attempt + jitter. Default: 500ms.
    /// </summary>
    [Range(100, 60_000)]
    public int RetryBaseDelayMs { get; set; } = 500;

    /// <summary>
    /// Route messages that exceed <see cref="MaxRetryAttempts"/> to a Dead-Letter Queue.
    /// Default: true.
    /// </summary>
    public bool EnableDeadLetterQueue { get; set; } = true;

    /// <summary>
    /// How often (in seconds) to re-evaluate consumer gates when they are closed.
    /// Default: 10 seconds.
    /// </summary>
    [Range(1, 300)]
    public int GatePollingIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Seconds to wait for in-flight messages to complete on graceful shutdown (SIGTERM).
    /// Should be less than the K8s terminationGracePeriodSeconds. Default: 30.
    /// </summary>
    [Range(1, 300)]
    public int ShutdownTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Resolved queue name — returns <see cref="QueueName"/> if explicitly set,
    /// otherwise falls back to <see cref="RoutingKey"/>.
    /// </summary>
    public string ResolvedQueueName =>
        !string.IsNullOrEmpty(QueueName) ? QueueName : RoutingKey ?? string.Empty;

    /// <summary>
    /// Whether this consumer uses exchange-based binding (exchange → routing key → queue).
    /// </summary>
    public bool HasExchangeBinding => !string.IsNullOrEmpty(ExchangeName);

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!HasExchangeBinding && string.IsNullOrEmpty(QueueName))
        {
            yield return new ValidationResult(
                "Either (ExchangeName + RoutingKey) or QueueName must be provided.",
                [nameof(ExchangeName), nameof(QueueName)]);
        }

        if (HasExchangeBinding && string.IsNullOrEmpty(RoutingKey))
        {
            yield return new ValidationResult(
                "RoutingKey is required when ExchangeName is set.",
                [nameof(RoutingKey)]);
        }

        if (string.IsNullOrEmpty(ResolvedQueueName))
        {
            yield return new ValidationResult(
                "A queue name must be resolvable — either set QueueName explicitly or provide a RoutingKey.",
                [nameof(QueueName), nameof(RoutingKey)]);
        }
    }
}
