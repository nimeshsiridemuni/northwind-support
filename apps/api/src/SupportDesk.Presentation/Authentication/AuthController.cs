using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth.Login;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public sealed class AuthController(
    LoginCommandHandler login,
    IAgentRepository agents) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var response = await login.HandleAsync(request, ct);

        if (response is null)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        return Ok(response);
    }

    [HttpGet("me")]
    public async Task<ActionResult<AuthenticatedAgentDto>> Me(
        CancellationToken ct)
    {
        var subject = User.FindFirstValue(
            JwtRegisteredClaimNames.Sub);

        if (!int.TryParse(subject, out var agentId))
        {
            return Unauthorized();
        }

        var agent = await agents.GetByIdAsync(agentId, ct);

        if (agent is null || !agent.IsActive)
        {
            return Unauthorized();
        }

        return Ok(new AuthenticatedAgentDto(
            agent.Id,
            agent.FullName,
            agent.Email));
    }
}