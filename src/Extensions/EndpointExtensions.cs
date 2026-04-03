using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Messaging.Core.Extensions;

/// <summary>
/// Provides an internal extension method for mapping the liveness/environment endpoint.
/// </summary>
public static class LiveEndpointExtensions
{
    /// <summary>
    /// Maps a liveness endpoint at <c>/health/live</c> that returns the current environment and a readiness endpoint at <c>/health/ready</c> 
    /// that reports the health status of the application and its dependencies.
    /// </summary>
    /// <param name="app">The application instance to map the endpoint on.</param>
    public static void MapHealthEndpoint(this WebApplication app)
    {
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";

                var result = JsonSerializer.Serialize(new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.Select(e => new {
                        name = e.Key,
                        status = e.Value.Status.ToString()
                    })
                });

                await context.Response.WriteAsync(result);
            }
        });

        app.MapGet("/health/live", () =>
        {
            var info = new Dictionary<string, string?>
            {
                // Include any relevant information
                { "environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") },
            };

            return info;
        });
    }
}
