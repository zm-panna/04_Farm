using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;
/// <summary>Consistent safe HTTP errors for known business conflicts; no database exception details are sent to clients.</summary>
public sealed class PermissionExceptionMiddleware(RequestDelegate next, ILogger<PermissionExceptionMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        try { await next(context); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or ArgumentException or KeyNotFoundException or DbUpdateException or InvalidOperationException or HttpRequestException)
        {
            if (context.Response.HasStarted) throw;
            var (status, title, detail) = ex switch
            {
                UnauthorizedAccessException => (context.User.Identity?.IsAuthenticated == true ? 403 : 401, "Access denied", ex.Message),
                ArgumentException => (400, "Invalid input", ex.Message),
                KeyNotFoundException => (404, "Record unavailable", "The requested record is not available in your selected scope."),
                ScanWorkflowException => (409, "Scan review required", ex.Message),
                DbUpdateConcurrencyException => (409, "Concurrent update", "The record changed. Reload the current values and retry."),
                DbUpdateException => (409, "Posting conflict", "The operation could not be saved. Check for duplicate references and related records."),
                HttpRequestException => (503, "Provider unavailable", "The configured provider did not respond successfully. Please retry later."),
                _ => (409, "Operation unavailable", "The operation cannot be completed in its current state. Check the records or contact your administrator.")
            };
            logger.LogWarning("Handled request failure {ExceptionType} on {Path}; status {Status}", ex.GetType().Name, context.Request.Path, status);
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new { title, status, detail, requestId = context.TraceIdentifier }, context.RequestAborted);
        }
    }
}
