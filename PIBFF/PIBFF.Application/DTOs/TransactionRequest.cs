namespace PIBFF.Application.DTOs;

/// <summary>
/// Wire-format payload accepted at POST /api/v1/partner/transactions.
/// Intentionally a plain DTO (no behavior) — validation lives in
/// </summary>
public sealed record TransactionRequest
{
    public string? PartnerId { get; init; }
    public string? TransactionReference { get; init; }
    public decimal Amount { get; init; }
    public string? Currency { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
}