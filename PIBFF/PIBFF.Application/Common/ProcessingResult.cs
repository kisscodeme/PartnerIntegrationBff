namespace PIBFF.Application.Common;

public enum ProcessingFailureReason
{
    None = 0,
    ValidationFailed,
    PartnerVerificationFailed,
    PartnerVerificationUnavailable,
    PublishFailed
}

/// <summary>
/// Outcome of the transaction processing pipeline
/// </summary>
public sealed class ProcessingResult<TValue>
{
    public bool IsSuccess { get; }
    public TValue? Value { get; }
    public ProcessingFailureReason FailureReason { get; }
    public IReadOnlyCollection<string> Errors { get; }
    private ProcessingResult(bool isSuccess, TValue? value, ProcessingFailureReason reason, IReadOnlyCollection<string> errors)
    {
        IsSuccess = isSuccess;
        Value = value;
        FailureReason = reason;
        Errors = errors;
    }

    public static ProcessingResult<TValue> Success(TValue value) =>
        new(true, value, ProcessingFailureReason.None, Array.Empty<string>());
    public static ProcessingResult<TValue> Failure(ProcessingFailureReason reason, params string[] errors) =>
        new(false, default, reason, errors);
    public static ProcessingResult<TValue> Failure(ProcessingFailureReason reason, IReadOnlyCollection<string> errors) =>
        new(false, default, reason, errors);
}
