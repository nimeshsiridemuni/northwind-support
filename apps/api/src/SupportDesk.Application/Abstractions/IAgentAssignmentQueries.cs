namespace SupportDesk.Application.Abstractions;

public sealed record AgentAssignmentCandidate(
    int AgentId,
    string FullName,
    int OpenTicketCount);

public interface IAgentAssignmentQueries
{
    Task<IReadOnlyList<AgentAssignmentCandidate>> GetEligibleAsync(
        int categoryId,
        bool requiresSpecialist,
        int? excludingTicketId,
        CancellationToken ct);
}