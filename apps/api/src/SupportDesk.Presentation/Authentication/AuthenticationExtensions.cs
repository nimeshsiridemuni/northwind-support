using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Features.Auth.Login;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Services;

namespace SupportDesk.Presentation.Authentication;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddDeskAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>() ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(settings.SigningKey) ||
            Encoding.UTF8.GetByteCount(settings.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must contain at least 32 bytes. " +
                "Configure it through user secrets or an environment variable.");
        }

        if (string.IsNullOrWhiteSpace(settings.Issuer) ||
            string.IsNullOrWhiteSpace(settings.Audience) ||
            settings.LifetimeMinutes is < 1 or > 120)
        {
            throw new InvalidOperationException(
                "JWT issuer and audience are required. " +
                "Token lifetime must be between 1 and 120 minutes.");
        }

        services.AddSingleton(settings);
        services.AddSingleton<IAgentPasswordHasher, AgentPasswordHasher>();
        services.AddScoped<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddScoped<LoginCommandHandler>();
        services.AddScoped<DevelopmentCredentialsSeeder>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ValidIssuer = settings.Issuer,
                    ValidAudience = settings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(settings.SigningKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = "name",
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var subject = context.Principal?.FindFirstValue(
                            JwtRegisteredClaimNames.Sub);

                        if (!int.TryParse(subject, out var agentId))
                        {
                            context.Fail("Invalid agent identity.");
                            return;
                        }

                        var repository = context.HttpContext.RequestServices
                            .GetRequiredService<IAgentRepository>();

                        var agent = await repository.GetByIdAsync(
                            agentId,
                            context.HttpContext.RequestAborted);

                        if (agent is null || !agent.IsActive)
                        {
                            context.Fail("Agent is unavailable.");
                        }
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }
}