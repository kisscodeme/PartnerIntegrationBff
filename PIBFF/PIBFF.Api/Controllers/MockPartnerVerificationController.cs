using Microsoft.AspNetCore.Mvc;
using PIBFF.Application.DTOs;

namespace PIBFF.Api.Controllers;

/// <summary>
/// DUMMY endpoint that simulates an unreliable upstream "Partner Verification API":
///   - ~30% of calls throw TimeoutException (simulating a slow/dead upstream)
///   - ~70% of calls return a valid verification response
///
/// This exists purely to give the Infrastructure-layer PartnerVerificationClient
/// (which calls this endpoint over HTTP) something realistically flaky, so the retry /
/// circuit-breaker pipeline has real failures to exercise. It is not part of the
/// BFF's real business logic and would be replaced by an actual partner's endpoint
/// in production.
/// </summary>
[ApiController]
[Route("api/mock/partner-verification")]
public sealed class MockPartnerVerificationController : ControllerBase
{
    private const double SimulatedFailureRate = 0.30;
    private static readonly Random Random = new();
    private readonly ILogger<MockPartnerVerificationController> _logger;

    public MockPartnerVerificationController(ILogger<MockPartnerVerificationController> logger)
    {
        _logger = logger;
    }

    /// <response code="200">Partner verification result.</response>
    /// <response code="504">Simulated upstream timeout (~30% of calls).</response>
    [HttpGet("{partnerId}")]
    [ProducesResponseType(typeof(PartnerVerificationApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> VerifyPartner(string partnerId, CancellationToken cancellationToken)
    {
        // Simulate realistic upstream latency before deciding the outcome.
        await Task.Delay(Random.Next(50, 250), cancellationToken);
        if (Random.NextDouble() < SimulatedFailureRate)
        {
            _logger.LogWarning("[Mock] Simulated timeout verifying partnerId {PartnerId}", partnerId);
            // Thrown deliberately, per requirement, rather than returned as a status code.
            // TimeoutExceptionMiddleware translates this into an HTTP 504 so the calling
            // HttpClient/Polly pipeline can classify it as a transient, retryable failure.
            throw new TimeoutException($"Simulated timeout verifying partnerId '{partnerId}'.");
        }

        var isKnownPartner = !string.IsNullOrWhiteSpace(partnerId) && partnerId.Length >= 3;
        _logger.LogInformation(
            "[Mock] Verified partnerId {PartnerId} -> IsValid={IsValid}", partnerId, isKnownPartner);

        return Ok(new PartnerVerificationApiResponse
        {
            PartnerId = partnerId,
            IsValid = isKnownPartner
        });
    }
}
