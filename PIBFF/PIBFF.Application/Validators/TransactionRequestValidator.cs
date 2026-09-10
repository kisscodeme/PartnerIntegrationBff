using FluentValidation;
using PIBFF.Application.DTOs;
using PIBFF.Domain.Constants;

namespace PIBFF.Application.Validators;

/// <summary>
/// Validation rules for the incoming partner transaction payload:
///   - all fields are required
/// </summary>
public sealed class TransactionRequestValidator : AbstractValidator<TransactionRequest>
{
    public TransactionRequestValidator()
    {
        RuleFor(x => x.PartnerId)
            .NotEmpty().WithMessage("partnerId is required.")
            .MaximumLength(64).WithMessage("partnerId must not exceed 64 characters.");

        RuleFor(x => x.TransactionReference)
            .NotEmpty().WithMessage("transactionReference is required.")
            .MaximumLength(128).WithMessage("transactionReference must not exceed 128 characters.");

        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithMessage("amount must be greater than 0.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("currency is required.")
            .Must(SupportedCurrencies.IsSupported)
            .WithMessage(x => $"currency '{x.Currency}' is not a supported currency code.");

        RuleFor(x => x.Timestamp)
            .NotNull().WithMessage("timestamp is required.")
            .Must(BeAReasonableTimestamp)
            .When(x => x.Timestamp.HasValue, ApplyConditionTo.CurrentValidator)
            .WithMessage("timestamp must not be in the future.");
    }

    /// <summary>
    /// Guards against obviously malformed clock values from partners.
    /// A small clock-skew allowance avoids false negatives from minor drift.
    /// </summary>
    private static bool BeAReasonableTimestamp(DateTimeOffset? timestamp) =>
        timestamp!.Value <= DateTimeOffset.UtcNow.AddMinutes(5);
}
