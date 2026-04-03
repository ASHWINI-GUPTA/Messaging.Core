using System.ComponentModel.DataAnnotations;

namespace Messaging.Core.Configuration;

/// <summary>
/// Base consumer options shared across all broker implementations.
/// Bind from the "Consumer" section in appsettings.json.
/// All values can be overridden via environment variables using double-underscore notation:
///   Consumer__QueueName, Consumer__MaxRetryAttempts, etc.
/// </summary>
public sealed class ConsumerOptions
{
    public const string SectionName = "Consumer";

    /// <summary>Name of the queue/topic to consume from.</summary>
    [Required]
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
}
