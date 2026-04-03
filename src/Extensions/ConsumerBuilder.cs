using Messaging.Core.Abstractions;
using Messaging.Core.Configuration;
using Messaging.Core.RabbitMq;
using Messaging.Core.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Messaging.Core.Extensions;

/// <summary>
/// Fluent builder for configuring per-consumer gates and behaviors.
/// Returned by <see cref="ServiceCollectionExtensions.AddConsumer{TMessage,TConsumer}"/>.
/// </summary>
public sealed class ConsumerBuilder<TMessage> where TMessage : IMessage
{
    private readonly IServiceCollection _services;
    private readonly List<Type> _behaviorTypes = [];
    private readonly List<Type> _gateTypes = [];

    internal ConsumerBuilder(IServiceCollection services)
    {
        _services = services;
        RegisterConsumerService();
    }

    /// <summary>Adds a per-consumer pipeline behavior (in addition to global behaviors).</summary>
    public ConsumerBuilder<TMessage> WithBehavior<TBehavior>()
        where TBehavior : class, IConsumerPipelineBehavior<TMessage>
    {
        _services.AddScoped<TBehavior>();
        _behaviorTypes.Add(typeof(TBehavior));
        return this;
    }

    /// <summary>Adds a gate that must be open before this consumer pulls any messages.</summary>
    public ConsumerBuilder<TMessage> WithGate<TGate>()
        where TGate : class, IConsumerGate
    {
        _services.AddSingleton<TGate>();
        _gateTypes.Add(typeof(TGate));
        return this;
    }

    /// <summary>Return to IServiceCollection for further registrations.</summary>
    public IServiceCollection Services => _services;

    private void RegisterConsumerService()
    {
        _services.AddSingleton<IConsumerService>(sp =>
        {
            var connection = sp.GetRequiredService<IRabbitMqConnection>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            var options = sp.GetRequiredService<IOptions<ConsumerOptions>>();
            var rabbitOptions = sp.GetRequiredService<IOptions<RabbitMqOptions>>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RabbitMqConsumerService<TMessage>>>();

            // Resolve per-consumer gates
            var gates = _gateTypes.Count > 0
                ? _gateTypes.Select(t => (IConsumerGate)sp.GetRequiredService(t)).ToList()
                : [];

            // Resolve per-consumer behaviors + global behaviors
            var globalBehaviors = sp.GetServices<GlobalBehaviorRegistration>()
                .Select(r => r.OpenGenericType.MakeGenericType(typeof(TMessage)))
                .ToList();

            var allBehaviorTypes = globalBehaviors.Concat(_behaviorTypes).ToList();

            var retryPipeline = ConsumerRetryPolicy.Build(options.Value,
                msg => ServiceCollectionLog.Retry(logger, msg));

            return new RabbitMqConsumerService<TMessage>(
                connection, scopeFactory, options, rabbitOptions,
                gates, allBehaviorTypes, retryPipeline, logger);
        });
    }
}
