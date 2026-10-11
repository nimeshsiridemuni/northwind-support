using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Application.Abstractions;

public enum PasswordCheckResult
{
    Failed,
    Success,
    SuccessRehashNeeded
}

public interface IAgentPasswordHasher
{
    string Hash(Agent agent, string password);

    PasswordCheckResult Verify(
        Agent agent,
        string passwordHash,
        string password);
}