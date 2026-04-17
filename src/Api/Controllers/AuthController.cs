using System.ComponentModel.DataAnnotations;
using Api.Controllers;
using Core.Models;
using Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

[EnableRateLimiting("auth")]
public class AuthController : BaseApiController
{
    private readonly TokenService _tokenService;
    private readonly IWebHostEnvironment _env;

    public AuthController(TokenService tokenService, IWebHostEnvironment env)
    {
        _tokenService = tokenService;
        _env = env;
    }

    [HttpPost("token")]
    public ActionResult<ApiResponse<string>> Token([FromBody] TokenRequest request)
    {
        // DEV-ONLY: This endpoint is for local scaffold provisioning only.
        if (!_env.IsDevelopment())
            throw new InvalidOperationException("Token endpoint is only available in Development.");

        var token = _tokenService.GenerateToken(request.UserId, request.Email, request.Roles);
        return OkResult(token);
    }
}

public record TokenRequest(
    [property: Required, MaxLength(128)] string UserId,
    [property: Required, EmailAddress, MaxLength(256)] string Email,
    [property: Required] IEnumerable<string> Roles);

