using Messaging.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Messaging.Core.Gates;

/// <summary>
/// A gate that polls an external service's HTTP health endpoint.
/// The gate is OPEN when the endpoint returns a 2xx response.
/// Use this to pause message consumption when a downstream HTTP dependency is unavailable.
///
/// Example: pause an order consumer if the Payment Service is down.
/// <code>
/// // In your concrete gate:
/// public class PaymentServiceGate(IHttpClientFactory factory, ILogger logger)
///     : HttpDependencyGate("https://payment-service/health", factory, logger) { }
/// </code>
/// </summary>
public abstract class HttpDependencyGate(
    string healthEndpoint,
    IHttpClientFactory httpClientFactory,
    ILogger logger) : IConsumerGate
{
    private readonly Uri _healthEndpointUri = new Uri(healthEndpoint);

    /// <summary>Override to provide a custom HTTP client name registered with IHttpClientFactory.</summary>
    protected virtual string HttpClientName => "ConsumerGate";

    public virtual string Name => GetType().Name;

    public async Task<bool> IsOpenAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(_healthEndpointUri, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            HttpDependencyGateLog.StatusNotSuccess(logger, Name, healthEndpoint, (int)response.StatusCode);
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            HttpDependencyGateLog.Unreachable(logger, ex, Name, healthEndpoint);
            return false;
        }
    }
}

/// <summary>High-performance logger messages for <see cref="HttpDependencyGate"/>.</summary>
internal static partial class HttpDependencyGateLog
{
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Gate {GateName}: {Endpoint} returned {StatusCode}. Gate is CLOSED.")]
    internal static partial void StatusNotSuccess(ILogger logger, string gateName, string endpoint, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Gate {GateName}: Could not reach {Endpoint}. Gate is CLOSED.")]
    internal static partial void Unreachable(ILogger logger, Exception ex, string gateName, string endpoint);
}
