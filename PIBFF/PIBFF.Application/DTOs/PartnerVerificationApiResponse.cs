namespace PIBFF.Application.DTOs;

/// <summary>
/// Wire-format response contract for the Partner Verification API.
/// Shared between the mock endpoint (Api layer) and the client that
/// consumes it (Infrastructure layer) so both sides serialize/deserialize
/// against the same shape.
/// </summary>
public sealed class PartnerVerificationApiResponse
{
    public required string PartnerId { get; init; }

    public required bool IsValid { get; init; }

    public DateTimeOffset VerifiedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
