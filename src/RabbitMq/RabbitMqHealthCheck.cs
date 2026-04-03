using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Messaging.Core.RabbitMq;

/// <summary>
/// Health check for the RabbitMQ connection.
/// Used by the readiness probe — returns Healthy only when the broker connection is open.
/// Used by K8s to decide whether to route traffic (messages) to this pod.
/// </summary>
public sealed class RabbitMqHealthCheck(IRabbitMqConnection connection) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (connection.IsConnected)
        {
            return Task.FromResult(HealthCheckResult.Healthy("RabbitMQ connection is open"));
        }

        return Task.FromResult(
            HealthCheckResult.Unhealthy("RabbitMQ connection is closed or not yet established"));
    }
}
