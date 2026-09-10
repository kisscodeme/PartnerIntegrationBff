namespace PIBFF.Api.Middleware;

/// <summary>
/// Maps an unhandled <see cref="TimeoutException"/> (thrown deliberately by
/// <see cref="Controllers.MockPartnerVerificationController"/> to simulate upstream
/// flakiness) to HTTP 504 Gateway Timeout, so that any HttpClient calling this
/// endpoint — and its attached Polly retry/circuit-breaker policies — can classify
/// the response as a transient failure using standard HTTP status semantics,
/// rather than the request blowing up as an unhandled HTTP 500.
/// </summary>
public sealed class TimeoutExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TimeoutExceptionMiddleware> _logger;

    public TimeoutExceptionMiddleware(RequestDelegate next, ILogger<TimeoutExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Simulated upstream timeout for {Path}", context.Request.Path);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "UpstreamTimeout",
                message = ex.Message
            });
        }
    }
}
