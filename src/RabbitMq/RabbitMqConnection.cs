using Messaging.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Messaging.Core.RabbitMq;

/// <summary>
/// Manages a single, long-lived RabbitMQ connection with automatic reconnection.
/// Registered as a singleton — all consumers and publishers share the same TCP connection
/// and use separate channels per operation.
/// </summary>
public interface IRabbitMqConnection : IAsyncDisposable
{
    /// <summary>True when the RabbitMQ connection is established and open.</summary>
    bool IsConnected { get; }

    /// <summary>Creates a new channel on the shared connection. Caller owns the channel lifetime.</summary>
    Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class RabbitMqConnection(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConnection> logger) : IRabbitMqConnection
{
    private readonly RabbitMqOptions _options = options.Value;
    private IConnection? _connection;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <inheritdoc />
    public bool IsConnected => _connection?.IsOpen == true;

    /// <inheritdoc />
    public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);
        return await _connection!.CreateChannelAsync(cancellationToken: cancellationToken);
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (IsConnected) return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected) return;

            RabbitMqConnectionLog.Connecting(logger, _options.Host, _options.Port, _options.VirtualHost);

            var factory = BuildConnectionFactory();
            _connection = await factory.CreateConnectionAsync(cancellationToken);

            _connection.ConnectionShutdownAsync += OnConnectionShutdownAsync;
            _connection.ConnectionBlockedAsync += OnConnectionBlockedAsync;
            _connection.ConnectionUnblockedAsync += OnConnectionUnblockedAsync;

            RabbitMqConnectionLog.Connected(logger);
        }
        catch (Exception ex)
        {
            RabbitMqConnectionLog.ConnectFailed(logger, ex, _options.Host, _options.Port);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    private ConnectionFactory BuildConnectionFactory() => new()
    {
        HostName = _options.Host,
        Port = _options.Port,
        VirtualHost = _options.VirtualHost,
        UserName = _options.Username,
        Password = _options.Password,
        Ssl = _options.UseSsl
            ? new SslOption { Enabled = true, ServerName = _options.SslServerName ?? _options.Host }
            : new SslOption { Enabled = false },
        RequestedHeartbeat = TimeSpan.FromSeconds(_options.HeartbeatSeconds),
        AutomaticRecoveryEnabled = _options.AutomaticRecoveryEnabled,
        NetworkRecoveryInterval = TimeSpan.FromSeconds(_options.NetworkRecoveryIntervalSeconds),
    };

    private Task OnConnectionShutdownAsync(object? sender, ShutdownEventArgs e)
    {
        RabbitMqConnectionLog.ConnectionShutdown(logger, e.ReplyText, _options.AutomaticRecoveryEnabled);
        return Task.CompletedTask;
    }

    private Task OnConnectionBlockedAsync(object? sender, ConnectionBlockedEventArgs e)
    {
        RabbitMqConnectionLog.ConnectionBlocked(logger, e.Reason);
        return Task.CompletedTask;
    }

    private Task OnConnectionUnblockedAsync(object? sender, AsyncEventArgs e)
    {
        RabbitMqConnectionLog.ConnectionUnblocked(logger);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            RabbitMqConnectionLog.Closing(logger);
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }

        _lock.Dispose();
    }
}

/// <summary>High-performance logger messages for <see cref="RabbitMqConnection"/>.</summary>
internal static partial class RabbitMqConnectionLog
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Establishing RabbitMQ connection to {Host}:{Port}{VirtualHost}")]
    internal static partial void Connecting(
        ILogger logger, string host, int port, string virtualHost);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "RabbitMQ connection established successfully")]
    internal static partial void Connected(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to connect to RabbitMQ at {Host}:{Port}")]
    internal static partial void ConnectFailed(
        ILogger logger, Exception ex, string host, int port);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "RabbitMQ connection shut down. Cause: {Cause}. AutoRecovery: {AutoRecovery}")]
    internal static partial void ConnectionShutdown(
        ILogger logger, string? cause, bool autoRecovery);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "RabbitMQ connection blocked. Reason: {Reason}")]
    internal static partial void ConnectionBlocked(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "RabbitMQ connection unblocked")]
    internal static partial void ConnectionUnblocked(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Closing RabbitMQ connection")]
    internal static partial void Closing(ILogger logger);
}
