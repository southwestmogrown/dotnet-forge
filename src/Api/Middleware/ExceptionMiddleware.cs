using Core.Exceptions;
using Microsoft.AspNetCore.Mvc;
using ValidationException = Core.Exceptions.ValidationException;

namespace Api.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (NotFoundException ex)
        {
            await WriteProblem(ctx, 404, "Not Found", ex.Message);
        }
        catch (ValidationException ex)
        {
            ctx.Response.StatusCode = 422;
            ctx.Response.ContentType = "application/problem+json";
            await ctx.Response.WriteAsJsonAsync(new
            {
                title = "Validation Error",
                status = 422,
                errors = ex.Errors,
                instance = ctx.Request.Path.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                ctx.Request.Method, ctx.Request.Path);

            var detail = _env.IsDevelopment() ? ex.ToString() : "An unexpected error occurred.";
            await WriteProblem(ctx, 500, "Server Error", detail);
        }
    }

    private static async Task WriteProblem(HttpContext ctx, int status, string title, string detail)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        await ctx.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = ctx.Request.Path
        });
    }
}
