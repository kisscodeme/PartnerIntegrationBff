using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PIBFF.Application.Interfaces;
using PIBFF.Infrastructure.ExternalServices;
using Polly;
using Polly.Extensions.Http;

namespace PIBFF.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers all Infrastructure-layer services: currently the Partner
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<IPartnerVerificationService, PartnerVerificationClient>(client =>
            {
                var baseUrl = configuration["PartnerVerificationApi:BaseUrl"]
                    ?? throw new InvalidOperationException(
                        "Configuration 'PartnerVerificationApi:BaseUrl' is missing.");

                client.BaseAddress = new Uri(baseUrl);

                // Per-attempt timeout: bounds a single HTTP call so a hung request
                // doesn't block the pipeline — the retry policy below reacts to it.
                client.Timeout = TimeSpan.FromSeconds(3);
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        return services;
    }

    /// <summary>
    /// Retries transient failures (5xx, 408, and network/timeout exceptions) up to
    /// 3 times with exponential backoff + jitter. Given the mock API's 30% failure rate
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        var jitterer = new Random();

        return HttpPolicyExtensions
            .HandleTransientHttpError() // 5xx and 408 — our simulated timeout maps to 504
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt))
                    + TimeSpan.FromMilliseconds(jitterer.Next(0, 100)));
    }

    /// <summary>
    /// Opens the circuit after 5 consecutive failures and stops calling the
    /// upstream for 15 seconds, so a genuinely down dependency fails fast
    /// instead of every request paying the full retry cost.
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(15));
    }
}
