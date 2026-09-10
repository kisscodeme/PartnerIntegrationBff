using PIBFF.Application.Common;
using PIBFF.Application.DTOs;

namespace PIBFF.Application.Interfaces;

/// <summary>
/// Orchestrates the full inbound transaction pipeline
/// </summary>
public interface ITransactionProcessingService
{
    Task<ProcessingResult<TransactionResponse>> ProcessAsync(TransactionRequest request, CancellationToken cancellationToken = default);
}
