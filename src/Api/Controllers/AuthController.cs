using Api.Controllers;
using Core.Models;
using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

public class AuthController : BaseApiController
{
    private readonly TokenService _tokenService;

    public AuthController(TokenService tokenService)
    {
        _tokenService = tokenService;
    }

    [HttpPost("token")]
    public ActionResult<ApiResponse<string>> Token([FromBody] TokenRequest request)
    {
        var token = _tokenService.GenerateToken(request.UserId, request.Email, request.Roles);
        return OkResult(token);
    }
}

public record TokenRequest(string UserId, string Email, IEnumerable<string> Roles);
