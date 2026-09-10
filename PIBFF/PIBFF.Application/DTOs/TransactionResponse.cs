namespace PIBFF.Application.DTOs;

/// <summary>
/// Response returned once a transaction has been accepted for processing.
/// </summary>
public sealed class TransactionResponse
{
    public required Guid CorrelationId { get; init; }
    public required string PartnerId { get; init; }
    public required string TransactionReference { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset AcceptedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
