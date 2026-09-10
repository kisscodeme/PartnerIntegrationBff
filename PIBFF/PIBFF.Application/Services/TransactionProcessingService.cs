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
    private readonly IPartnerVerificationService _partnerVerificationService;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<TransactionProcessingService> _logger;

    public TransactionProcessingService(
        IValidator<TransactionRequest> validator,
        IPartnerVerificationService partnerVerificationService,
         IMessagePublisher messagePublisher,
        ILogger<TransactionProcessingService> logger)
    {
        _validator = validator;
        _partnerVerificationService = partnerVerificationService;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

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

        // TODO (requirement 2): verify transaction.PartnerId against the
        // Partner Verification API with a resilience strategy before proceeding.

        PartnerVerificationOutcome verification = await _partnerVerificationService.VerifyAsync(
            transaction.PartnerId, cancellationToken);

        switch (verification.Status)
        {
            case PartnerVerificationStatus.Unavailable:
                // Upstream kept failing even after retries/circuit-breaker — this is
                // an infrastructure problem, not the partner's fault. Surface it as a
                // distinct failure reason (-> HTTP 502 at the controller) so the
                // request fails gracefully instead of crashing or returning a false 4xx.
                _logger.LogError(
                    "Partner verification unavailable for transaction {TransactionReference} from partner {PartnerId}: {Reason}",
                    transaction.TransactionReference, transaction.PartnerId, verification.Reason);

                return ProcessingResult<TransactionResponse>.Failure(
                    ProcessingFailureReason.PartnerVerificationUnavailable, verification.Reason!);

            case PartnerVerificationStatus.Rejected:
                _logger.LogWarning(
                    "Rejected transaction {TransactionReference}: {Reason}",
                    transaction.TransactionReference, verification.Reason);

                return ProcessingResult<TransactionResponse>.Failure(
                    ProcessingFailureReason.PartnerVerificationFailed, verification.Reason!);
        }


        // TODO (requirement 3): publish `transaction` to the message queue
        // once verification succeeds, and reflect publish failures in the result.
        try
        {
            await _messagePublisher.PublishAsync(transaction, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish transaction {TransactionReference} from partner {PartnerId} (correlationId: {CorrelationId})",
                transaction.TransactionReference, transaction.PartnerId, transaction.CorrelationId);

            return ProcessingResult<TransactionResponse>.Failure(
                ProcessingFailureReason.PublishFailed,
                "Message broker is unavailable or rejected the transaction.");
        }

        _logger.LogInformation(
            "Queued verified transaction {TransactionReference} from partner {PartnerId} (correlationId: {CorrelationId})",
            transaction.TransactionReference, transaction.PartnerId, transaction.CorrelationId);

        var response = new TransactionResponse
        {
            CorrelationId = transaction.CorrelationId,
            PartnerId = transaction.PartnerId,
            TransactionReference = transaction.TransactionReference,
            Status = "Verified"
        };

        return ProcessingResult<TransactionResponse>.Success(response);
    }
}
