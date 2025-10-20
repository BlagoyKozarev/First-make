namespace Api.Middleware;

/// <summary>
/// Global exception handling middleware
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Request was cancelled");
            context.Response.StatusCode = 499; // Client Closed Request
            await context.Response.WriteAsJsonAsync(new { error = "Request was cancelled" });
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Operation timed out");
            context.Response.StatusCode = StatusCodes.Status408RequestTimeout;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Operation timed out",
                message = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Invalid input",
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Invalid operation",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "An unexpected error occurred",
                message = context.Request.Host.Host.Contains("localhost") ? ex.Message : "Please try again later"
            });
        }
    }
}

/// <summary>
/// Request size limiting middleware
/// </summary>
public class RequestSizeLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestSizeLimitMiddleware> _logger;
    private readonly long _maxRequestBodySize;

    public RequestSizeLimitMiddleware(
        RequestDelegate next,
        ILogger<RequestSizeLimitMiddleware> logger,
        long maxRequestBodySize = 100 * 1024 * 1024) // 100MB default
    {
        _next = next;
        _logger = logger;
        _maxRequestBodySize = maxRequestBodySize;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.ContentLength.HasValue &&
            context.Request.ContentLength.Value > _maxRequestBodySize)
        {
            _logger.LogWarning(
                "Request body too large: {Size} bytes (max: {Max})",
                context.Request.ContentLength.Value,
                _maxRequestBodySize);

            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Request body too large",
                maxSize = _maxRequestBodySize,
                actualSize = context.Request.ContentLength.Value
            });
            return;
        }

        await _next(context);
    }
}

/// <summary>
/// Request timeout middleware
/// </summary>
public class RequestTimeoutMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTimeoutMiddleware> _logger;
    private readonly TimeSpan _timeout;

    public RequestTimeoutMiddleware(
        RequestDelegate next,
        ILogger<RequestTimeoutMiddleware> logger,
        TimeSpan? timeout = null)
    {
        _next = next;
        _logger = logger;
        _timeout = timeout ?? TimeSpan.FromMinutes(5); // 5 minutes default
    }

    public async Task InvokeAsync(HttpContext context)
    {
        using var cts = new CancellationTokenSource(_timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cts.Token,
            context.RequestAborted);

        try
        {
            var originalToken = context.RequestAborted;
            context.RequestAborted = linkedCts.Token;

            await _next(context);

            context.RequestAborted = originalToken;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            _logger.LogWarning("Request timed out after {Timeout}", _timeout);
            context.Response.StatusCode = StatusCodes.Status408RequestTimeout;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Request timed out",
                timeout = _timeout.TotalSeconds
            });
        }
    }
}
