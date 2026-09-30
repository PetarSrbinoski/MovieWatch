using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using MovieWatch.Domain.Common;

namespace MovieWatch.Web.Errors;

public sealed class ApiExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception is OperationException operation ? operation.Kind switch
        {
            FailureKind.Validation => StatusCodes.Status400BadRequest,
            FailureKind.Conflict => StatusCodes.Status409Conflict,
            FailureKind.Unauthenticated => StatusCodes.Status401Unauthorized,
            FailureKind.NotFound => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError
        } : StatusCodes.Status500InternalServerError;

        context.Response.StatusCode = status;
        Console.Error.WriteLine(exception);
        if (status == StatusCodes.Status401Unauthorized)
            context.Response.Headers.WWWAuthenticate = "Bearer";
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new()
            {
                Status = status,
                Title = Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(status),
                Detail = exception is OperationException ? exception.Message : "An unexpected error occurred."
            }
        });
    }
}
