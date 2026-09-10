using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using PIBFF.Application.Common;
using PIBFF.Application.DTOs;
using PIBFF.Application.Interfaces;
using PIBFF.Domain.Entities;

namespace PIBFF.Application.Services;

/// <summary>
/// Default implementation of the transaction processing pipeline.
/// </summary>
public sealed class TransactionProcessingService : ITransactionProcessingService
{
    private readonly IValidator<TransactionRequest> _validator;
    private readonly ILogger<TransactionProcessingService> _logger;

    public TransactionProcessingService(
        IValidator<TransactionRequest> validator,
        ILogger<TransactionProcessingService> logger)
    {
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Processing Result
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<ProcessingResult<TransactionResponse>> ProcessAsync(
        TransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidationResult validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToArray();
            _logger.LogWarning(
                "Rejected transaction {TransactionReference} from partner {PartnerId}: {Errors}",
                request.TransactionReference, request.PartnerId, string.Join("; ", errors));
            return ProcessingResult<TransactionResponse>.Failure(ProcessingFailureReason.ValidationFailed, errors);
        }

        // Create partner transaction
        var transaction = new PartnerTransaction
        {
            PartnerId = request.PartnerId!,
            TransactionReference = request.TransactionReference!,
            Amount = request.Amount,
            Currency = request.Currency!.ToUpperInvariant(),
            Timestamp = request.Timestamp!.Value
        };

        _logger.LogInformation(
            "Validated transaction {TransactionReference} from partner {PartnerId} (correlationId: {CorrelationId})",
            transaction.TransactionReference, transaction.PartnerId, transaction.CorrelationId);

        // Response transaction
        var response = new TransactionResponse
        {
            CorrelationId = transaction.CorrelationId,
            PartnerId = transaction.PartnerId,
            TransactionReference = transaction.TransactionReference,
            Status = "Validated"
        };

        return ProcessingResult<TransactionResponse>.Success(response);
    }
}
