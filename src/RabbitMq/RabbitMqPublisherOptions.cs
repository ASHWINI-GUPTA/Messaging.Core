namespace Messaging.Core.RabbitMq;

/// <summary>
/// Configuration options for RabbitMQ message publishing channels.
/// </summary>
public sealed class RabbitMqPublisherOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether publisher confirmations are enabled on the channel.
    /// When enabled, published messages must be acknowledged by the broker before the publish operation completes.
    /// Default: <see langword="true"/>.
    /// </summary>
    public bool PublisherConfirmationsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether publisher confirmation tracking is enabled on the channel.
    /// Default: <see langword="true"/>.
    /// </summary>
    public bool PublisherConfirmationTrackingEnabled { get; set; } = true;
}
