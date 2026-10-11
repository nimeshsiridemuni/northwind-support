using Microsoft.AspNetCore.Identity;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Infrastructure.Services;

public sealed class AgentPasswordHasher : IAgentPasswordHasher
{
    private readonly PasswordHasher<Agent> _hasher = new();

    public string Hash(Agent agent, string password) =>
        _hasher.HashPassword(agent, password);

    public PasswordCheckResult Verify(
        Agent agent,
        string passwordHash,
        string password)
    {
        try
        {
            return _hasher.VerifyHashedPassword(
                agent,
                passwordHash,
                password) switch
            {
                PasswordVerificationResult.Success =>
                    PasswordCheckResult.Success,

                PasswordVerificationResult.SuccessRehashNeeded =>
                    PasswordCheckResult.SuccessRehashNeeded,

                _ => PasswordCheckResult.Failed
            };
        }
        catch (FormatException)
        {
            return PasswordCheckResult.Failed;
        }
    }
}