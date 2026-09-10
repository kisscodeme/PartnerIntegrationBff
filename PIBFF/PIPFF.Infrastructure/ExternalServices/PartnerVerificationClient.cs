using Microsoft.Extensions.Logging;
using PIBFF.Application.DTOs;
using PIBFF.Application.Interfaces;
using Polly.CircuitBreaker;
using System.Net.Http.Json;

namespace PIBFF.Infrastructure.ExternalServices;

/// <summary>
/// Calls the (mock) Partner Verification API over HTTP.
/// </summary>
public sealed class PartnerVerificationClient : IPartnerVerificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PartnerVerificationClient> _logger;

    /// <summary>
    /// Partner Verification Client 
    /// </summary>
    /// <param name="httpClient"></param>
    /// <param name="logger"></param>
    public PartnerVerificationClient(HttpClient httpClient, ILogger<PartnerVerificationClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Verify Async
    /// </summary>
    /// <param name="partnerId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<PartnerVerificationOutcome> VerifyAsync(string partnerId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                $"api/mock/partner-verification/{Uri.EscapeDataString(partnerId)}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Partner verification returned {StatusCode} for partnerId {PartnerId} after retries.",
                    response.StatusCode, partnerId);

                return PartnerVerificationOutcome.Unavailable(
                    $"Verification API returned {(int)response.StatusCode} {response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<PartnerVerificationApiResponse>(cancellationToken);

            if (payload is null)
            {
                return PartnerVerificationOutcome.Unavailable("Verification API returned an empty response.");
            }

            return payload.IsValid
                ? PartnerVerificationOutcome.Verified()
                : PartnerVerificationOutcome.Rejected($"partnerId '{partnerId}' is not a recognized partner.");
        }
        catch (BrokenCircuitException ex)
        {
            // Circuit is open: upstream was deemed unhealthy, so Polly failed fast
            _logger.LogError(ex, "Partner verification circuit is OPEN for partnerId {PartnerId}.", partnerId);
            return PartnerVerificationOutcome.Unavailable("Verification service temporarily unavailable (circuit open).");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // The per-attempt HttpClient timeout tripped (not caller cancellation).
            _logger.LogError(ex, "Partner verification timed out after retries for partnerId {PartnerId}.", partnerId);
            return PartnerVerificationOutcome.Unavailable("Verification service timed out after retries.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Partner verification request failed for partnerId {PartnerId}.", partnerId);
            return PartnerVerificationOutcome.Unavailable("Verification service unreachable.");
        }
    }
}
