using System.ComponentModel.DataAnnotations;

namespace Messaging.Core.Configuration;

/// <summary>
/// RabbitMQ connection options.
/// Bind from the "RabbitMq" section in appsettings.json.
/// Override individual values via environment variables:
///   RabbitMq__Host, RabbitMq__Password, etc.
/// Sensitive values (Password) should be overridden via K8s Secrets mounted as env vars,
/// not stored in appsettings.json checked into source control.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    /// <summary>RabbitMQ server hostname or IP address.</summary>
    [Required]
    public string Host { get; set; } = "localhost";

    /// <summary>AMQP port. Default: 5672 (5671 for TLS).</summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 5672;

    /// <summary>Virtual host to connect to. Default: "/".</summary>
    [Required]
    public string VirtualHost { get; set; } = "/";

    /// <summary>AMQP username.</summary>
    [Required]
    public string Username { get; set; } = "guest";

    /// <summary>AMQP password. Override via environment variable / K8s Secret.</summary>
    [Required]
    public string Password { get; set; } = "guest";

    /// <summary>Enable TLS for the AMQP connection.</summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>Expected server name in the TLS certificate (required when UseSsl = true).</summary>
    public string? SslServerName { get; set; }

    /// <summary>
    /// Heartbeat interval in seconds. Keeps the connection alive across idle periods.
    /// Default: 60.
    /// </summary>
    [Range(10, 600)]
    public ushort HeartbeatSeconds { get; set; } = 60;

    /// <summary>
    /// Allow the RabbitMQ client to automatically recover from network failures.
    /// Default: true.
    /// </summary>
    public bool AutomaticRecoveryEnabled { get; set; } = true;

    /// <summary>
    /// Network recovery interval in seconds when AutomaticRecoveryEnabled = true.
    /// Default: 5.
    /// </summary>
    [Range(1, 60)]
    public int NetworkRecoveryIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Exchange used for dead-letter routing. Created automatically if EnableDeadLetterQueue is true.
    /// Default: "dlx".
    /// </summary>
    public string DeadLetterExchangeName { get; set; } = "dlx";

    /// <summary>Convenience property to build the AMQP URI.</summary>
    public Uri AmqpUri => new($"amqp://{Username}:{Password}@{Host}:{Port}/{Uri.EscapeDataString(VirtualHost)}");
}
