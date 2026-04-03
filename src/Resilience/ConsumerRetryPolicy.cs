using Messaging.Core.Configuration;
using Polly;
using Polly.Retry;

namespace Messaging.Core.Resilience;

/// <summary>
/// Factory for building the Polly v8 resilience pipeline used by the consumer.
/// Strategy: exponential back-off with full jitter to prevent retry storms in HA deployments.
/// After <see cref="ConsumerOptions.MaxRetryAttempts"/> failures the exception propagates
/// and the message is routed to the DLQ by the consumer service.
/// </summary>
public static class ConsumerRetryPolicy
{
    /// <summary>
    /// Builds a <see cref="ResiliencePipeline"/> configured from <see cref="ConsumerOptions"/>.
    /// </summary>
    public static ResiliencePipeline Build(ConsumerOptions options, Action<string>? onRetry = null)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = options.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(options.RetryBaseDelayMs),
                OnRetry = args =>
                {
                    onRetry?.Invoke(
                        $"Retry {args.AttemptNumber}/{options.MaxRetryAttempts} " +
                        $"after {args.RetryDelay.TotalMilliseconds:F0}ms. " +
                        $"Error: {args.Outcome.Exception?.Message}");

                    return ValueTask.CompletedTask;
                },
            })
            .Build();
    }
}
