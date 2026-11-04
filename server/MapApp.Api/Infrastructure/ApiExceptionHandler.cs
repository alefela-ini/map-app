using MapApp.Api.Validation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace MapApp.Api.Infrastructure;

/// <summary>
/// Converts known exceptions into RFC 7807 problem responses so the client always gets a consistent error shape.
/// </summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    // MongoDB "Can't extract geo keys" — the 2dsphere index rejected a geometry.
    private const int InvalidGeoKeysErrorCode = 16755;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails? problem = exception switch
        {
            RequestValidationException validation => new ValidationProblemDetails(validation.Errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
            },
            MongoBulkWriteException bulk when bulk.WriteErrors.Any(e => e.Code == InvalidGeoKeysErrorCode) => InvalidGeometry(),
            MongoWriteException write when write.WriteError?.Code == InvalidGeoKeysErrorCode => InvalidGeometry(),
            TimeoutException or MongoConnectionException => new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "The database is unavailable. Please try again later.",
            },
            _ => null,
        };

        if (problem is null)
        {
            return false; // Let the default handler produce a generic 500.
        }

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Database error while handling {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails InvalidGeometry() => new()
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "The geometry was rejected by the database (for example, a self-intersecting polygon).",
    };
}
