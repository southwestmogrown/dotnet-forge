using Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    protected ActionResult<ApiResponse<T>> OkResult<T>(T data, string? message = null) =>
        Ok(new ApiResponse<T>(true, data, message));

    protected ActionResult<ApiResponse<T>> CreatedResult<T>(
        string routeName, object routeValues, T data) =>
        CreatedAtRoute(routeName, routeValues, new ApiResponse<T>(true, data));

    protected ActionResult<ApiResponse<T>> FromResult<T>(Result<T> result) =>
        result.IsSuccess
            ? OkResult(result.Value!)
            : BadRequest(new ProblemDetails
            {
                Detail = result.Error,
                Status = 400,
                Instance = HttpContext.Request.Path
            });
}
