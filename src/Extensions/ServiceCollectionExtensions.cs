using Messaging.Core.Abstractions;
using Messaging.Core.Configuration;
using Messaging.Core.Hosting;
using Messaging.Core.RabbitMq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Messaging.Core.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the RabbitMQ connection and all required broker services.
    /// Binds <see cref="RabbitMqOptions"/> from the "RabbitMq" configuration section.
    /// </summary>
    public static IServiceCollection AddRabbitMqBroker(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IRabbitMqConnection, RabbitMqConnection>();
        services.AddHostedService<ConsumerBackgroundService>();

        return services;
    }

    /// <summary>
    /// Registers the RabbitMQ <see cref="IMessagePublisher"/> implementation.
    /// </summary>
    public static IServiceCollection AddRabbitMqPublisher(this IServiceCollection services)
    {
        services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
        return services;
    }

    /// <summary>
    /// Registers a typed consumer bound to an exchange via a routing key.
    /// This is the primary registration pattern — the exchange and queue binding
    /// are declared automatically during topology setup.
    /// </summary>
    /// <typeparam name="TMessage">The message type to consume.</typeparam>
    /// <typeparam name="TConsumer">The consumer implementation.</typeparam>
    /// <param name="exchangeName">The exchange to bind to.</param>
    /// <param name="routingKey">The routing key for the exchange → queue binding.</param>
    /// <param name="queueName">Optional queue name. Defaults to <paramref name="routingKey"/> if not specified.</param>
    /// <param name="exchangeType">Exchange type: direct, topic, fanout, headers. Default: "direct".</param>
    public static ConsumerBuilder<TMessage> AddConsumer<TMessage, TConsumer>(
        this IServiceCollection services,
        string exchangeName,
        string routingKey,
        string? queueName = null,
        string exchangeType = "direct")
        where TMessage : IMessage
        where TConsumer : class, IMessageConsumer<TMessage>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);

        services
            .AddOptions<ConsumerOptions>()
            .Configure(o =>
            {
                o.ExchangeName = exchangeName;
                o.RoutingKey = routingKey;
                o.ExchangeType = exchangeType;

                if (!string.IsNullOrEmpty(queueName))
                    o.QueueName = queueName;
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IMessageConsumer<TMessage>, TConsumer>();

        return new ConsumerBuilder<TMessage>(services);
    }

    /// <summary>
    /// Registers a typed consumer for direct queue consumption (no exchange binding).
    /// Use this for the rare case where messages are published directly to a queue
    /// via the default exchange.
    /// </summary>
    /// <typeparam name="TMessage">The message type to consume.</typeparam>
    /// <typeparam name="TConsumer">The consumer implementation.</typeparam>
    /// <param name="queueName">The queue to consume from.</param>
    public static ConsumerBuilder<TMessage> AddConsumer<TMessage, TConsumer>(
        this IServiceCollection services,
        string queueName)
        where TMessage : IMessage
        where TConsumer : class, IMessageConsumer<TMessage>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        services
            .AddOptions<ConsumerOptions>()
            .Configure(o => o.QueueName = queueName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IMessageConsumer<TMessage>, TConsumer>();

        return new ConsumerBuilder<TMessage>(services);
    }

    /// <summary>
    /// Registers a pipeline behavior globally for ALL consumers.
    /// Pass the open generic type: <c>typeof(LoggingBehavior&lt;&gt;)</c>.
    /// </summary>
    public static IServiceCollection AddGlobalConsumerBehavior(
        this IServiceCollection services,
        Type openGenericBehaviorType)
    {
        // Store the open generic type in a registry resolved at consumer-build time
        services.AddSingleton(new GlobalBehaviorRegistration(openGenericBehaviorType));

        // Register the generic behavior itself so it can be resolved by Type
        services.AddScoped(openGenericBehaviorType);

        return services;
    }

    /// <summary>
    /// Adds RabbitMQ health check.
    /// </summary>
    public static IServiceCollection AddConsumerHealthChecks(this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddCheck<RabbitMqHealthCheck>(
                name: "RabbitMQ",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"]);

        return services;
    }

    /// <summary>
    /// Adds OpenTelemetry tracing with a custom ActivitySource provided by the implementer.
    /// Exporters: OTLP (→ Elastic APM) + Console (dev).
    /// Configure the OTLP endpoint via <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> env var.
    /// </summary>
    /// <param name="builder">The OpenTelemetry builder.</param>
    /// <param name="activitySourceName">The name of the ActivitySource.</param>
    /// <param name="activitySourceVersion">The version of the ActivitySource.</param>
    public static OpenTelemetryBuilder AddConsumerTracing(
        this OpenTelemetryBuilder builder, 
        string activitySourceName, 
        string activitySourceVersion = "1.0.0")
    {
        Observability.ConsumerActivitySource.Initialize(activitySourceName, activitySourceVersion);

        return builder.WithTracing(tracing => tracing
            .AddSource(activitySourceName)
            .AddOtlpExporter()
            .AddConsoleExporter());
    }
}

/// <summary>Marker to hold globally registered open-generic behavior types.</summary>
public sealed record GlobalBehaviorRegistration(Type OpenGenericType);

/// <summary>High-performance logger messages for <see cref="ServiceCollectionExtensions"/>.</summary>
internal static partial class ServiceCollectionLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Retry: {Message}")]
    internal static partial void Retry(ILogger logger, string message);
}
