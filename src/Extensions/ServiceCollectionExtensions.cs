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
    /// Registers a typed consumer and returns a fluent <see cref="ConsumerBuilder{TMessage}"/>
    /// for chaining gates and per-consumer behaviors.
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
    /// Adds OpenTelemetry tracing with the Consumer.Template ActivitySource.
    /// Exporters: OTLP (→ Elastic APM) + Console (dev).
    /// Configure the OTLP endpoint via <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> env var.
    /// </summary>
    public static OpenTelemetryBuilder AddConsumerTracing(this OpenTelemetryBuilder builder)
    {
        return builder.WithTracing(tracing => tracing
            .AddSource("Consumer.Template")
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
