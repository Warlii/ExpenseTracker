using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ExpenseTracker.Api.Infrastructure;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var status = exception switch
        {
            ApiException api => api.StatusCode,
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.UniqueViolation } } => 409,
            DbUpdateConcurrencyException => 409,
            _ => 500
        };
        if (status == 500) logger.LogError(exception, "Erro inesperado em {Path}", context.Request.Path);
        else logger.LogWarning("Requisição rejeitada em {Path}: HTTP {Status}", context.Request.Path, status);
        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = status switch { 400 => "Requisição inválida", 404 => "Recurso não encontrado", 409 => "Conflito", _ => "Erro interno" },
                Detail = exception is ApiException known ? known.Message : status == 409
                    ? "A operação conflita com o estado atual dos dados." : "Ocorreu um erro ao processar a requisição.",
                Extensions = { ["traceId"] = context.TraceIdentifier }
            }
        });
    }
}
