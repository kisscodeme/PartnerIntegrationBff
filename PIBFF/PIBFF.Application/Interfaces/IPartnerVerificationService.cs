namespace PIBFF.Application.Interfaces;

/// <summary>
/// Verifies a partnerId against the external Partner Verification API.
///
/// Contract: this method never throws for transient upstream failures
/// (timeouts, retries exhausted, circuit open). Every failure mode is
/// surfaced as a <see cref="PartnerVerificationOutcome"/> so that
/// <c>TransactionProcessingService</c> can map it to a well-defined
/// <c>ProcessingFailureReason</c> instead of letting an exception
/// propagate and crash the incoming request.
/// </summary>
public interface IPartnerVerificationService
{
    Task<PartnerVerificationOutcome> VerifyAsync(string partnerId, CancellationToken cancellationToken = default);
}

public enum PartnerVerificationStatus
{
    /// <summary>Upstream responded and confirmed the partner is valid.</summary>
    Verified,

    /// <summary>Upstream responded and confirmed the partner is NOT valid (business rejection).</summary>
    Rejected,

    /// <summary>Upstream could not be reached / kept failing even after retries, or the circuit is open.</summary>
    Unavailable
}

/// <summary>
/// Outcome of a partner verification attempt. Deliberately a closed result
/// type (not a bool, not an exception) so every caller must explicitly
/// handle all three states: verified, rejected, unavailable.
/// </summary>
public sealed record PartnerVerificationOutcome
{
    public required PartnerVerificationStatus Status { get; init; }

    public string? Reason { get; init; }

    public static PartnerVerificationOutcome Verified() =>
        new() { Status = PartnerVerificationStatus.Verified };

    public static PartnerVerificationOutcome Rejected(string reason) =>
        new() { Status = PartnerVerificationStatus.Rejected, Reason = reason };

    public static PartnerVerificationOutcome Unavailable(string reason) =>
        new() { Status = PartnerVerificationStatus.Unavailable, Reason = reason };
}
