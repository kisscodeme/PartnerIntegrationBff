using Microsoft.AspNetCore.Mvc;
using PIBFF.Application.Common;
using PIBFF.Application.DTOs;
using PIBFF.Application.Interfaces;

namespace PartnerIntegrationBff.Api.Controllers;

[ApiController]
[Route("api/v1/partner/transactions")]
[Produces("application/json")]
public sealed class TransactionsController : ControllerBase
{
    private readonly ITransactionProcessingService _processingService;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(
        ITransactionProcessingService processingService,
        ILogger<TransactionsController> logger)
    {
        _processingService = processingService;
        _logger = logger;
    }

    /// <summary>
    /// Accepts a partner transaction, validates it, verifies the partner
    /// and (once wired in) queues it for legacy processing.
    /// </summary>
    /// <response code="202">Transaction accepted for processing.</response>
    /// <response code="400">Payload failed validation.</response>
    /// <response code="502">Partner verification could not be completed (upstream unavailable).</response>
    [HttpPost]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Post([FromBody] TransactionRequest request, CancellationToken cancellationToken)
    {
        ProcessingResult<TransactionResponse> result = await _processingService.ProcessAsync(request, cancellationToken);

        if (result.IsSuccess)
        {
            return Accepted(result.Value);
        }

        return result.FailureReason switch
        {
            ProcessingFailureReason.ValidationFailed => BadRequest(BuildValidationProblem(result.Errors)),

            ProcessingFailureReason.PartnerVerificationFailed => Problem(
                title: "Partner could not be verified.",
                detail: string.Join(' ', result.Errors),
                statusCode: StatusCodes.Status422UnprocessableEntity),

            ProcessingFailureReason.PartnerVerificationUnavailable => Problem(
                title: "Partner verification service is currently unavailable.",
                detail: string.Join(' ', result.Errors),
                statusCode: StatusCodes.Status502BadGateway),

            ProcessingFailureReason.PublishFailed => Problem(
                title: "Transaction could not be queued for processing.",
                detail: string.Join(' ', result.Errors),
                statusCode: StatusCodes.Status503ServiceUnavailable),

            _ => Problem(
                title: "Unexpected error while processing the transaction.",
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static ValidationProblemDetails BuildValidationProblem(IReadOnlyCollection<string> errors)
    {
        var problem = new ValidationProblemDetails
        {
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest
        };

        problem.Errors.Add("payload", errors.ToArray());
        return problem;
    }
}
