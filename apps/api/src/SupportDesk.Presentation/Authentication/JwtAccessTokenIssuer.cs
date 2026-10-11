using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Presentation.Authentication;

public sealed class JwtAccessTokenIssuer(
    JwtOptions options,
    IClock clock) : IAccessTokenIssuer
{
    public LoginResponse Issue(Agent agent)
    {
        var now = clock.UtcNow;
        var expiresAtUtc = now.AddMinutes(options.LifetimeMinutes);

        var claims = new[]
        {
            new Claim(
                JwtRegisteredClaimNames.Sub,
                agent.Id.ToString(CultureInfo.InvariantCulture)),

            new Claim("name", agent.FullName),

            new Claim(JwtRegisteredClaimNames.Email, agent.Email),

            new Claim(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString("N"))
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(options.SigningKey));

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAtUtc,
            signingCredentials: new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256));

        return new LoginResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAtUtc,
            new AuthenticatedAgentDto(
                agent.Id,
                agent.FullName,
                agent.Email));
    }
}