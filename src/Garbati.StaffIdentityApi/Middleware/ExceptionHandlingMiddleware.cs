using Garbati.StaffIdentityApi.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Garbati.StaffIdentityApi.Middleware;

// Same shape as LedgerApi's/IdentityApi's/OrchestratorApi's middleware of the same name
// (ARCHITECTURE.md Section 5's status-code mapping). 401/403 are handled by
// ApiKeyAuthenticationHandler, not here. DESIGN.md Section 3 calls the two business-rule rejections
// below (Terminated dead end, invalid supervisor) 400, not the platform's usual generic-422
// "business-rule failure" bucket - this service follows its own design doc's literal wording.
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request cancelled by client: {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var (status, title) = Map(ex);
            var isExpected = status != StatusCodes.Status500InternalServerError;

            if (isExpected)
                logger.LogWarning(ex, "Request rejected ({Status}) {Method} {Path}", status, context.Request.Method, context.Request.Path);
            else
                logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);

            context.Response.StatusCode = status;

            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                // Only expected business errors expose their message; unexpected ones must not leak internals.
                Detail = isExpected ? ex.Message : null,
                Extensions = { ["correlationId"] = context.TraceIdentifier }
            };

            await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
        }
    }

    private static (int Status, string Title) Map(Exception ex) => ex switch
    {
        StaffNotFoundException => (StatusCodes.Status404NotFound, "Staff member not found."),
        DuplicateIdempotencyKeyException => (StatusCodes.Status409Conflict, "Duplicate idempotency key."),
        DuplicateUsernameException => (StatusCodes.Status409Conflict, "Username is already in use."),
        InvalidStatusTransitionException => (StatusCodes.Status400BadRequest, "Invalid status transition."),
        InvalidSupervisorAssignmentException => (StatusCodes.Status400BadRequest, "Invalid supervisor assignment."),
        IncorrectCurrentPasswordException => (StatusCodes.Status400BadRequest, "Current password is incorrect."),
        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
    };
}
