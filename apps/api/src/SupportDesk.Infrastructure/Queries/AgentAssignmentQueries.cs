using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

public sealed class AgentAssignmentQueries(SupportDbContext db)
    : IAgentAssignmentQueries
{
    public async Task<IReadOnlyList<AgentAssignmentCandidate>> GetEligibleAsync(
        int categoryId,
        bool requiresSpecialist,
        int? excludingTicketId,
        CancellationToken ct)
    {
        var agents = db.Set<Agent>()
            .AsNoTracking()
            .Where(a => a.IsActive);

        if (requiresSpecialist)
        {
            agents = agents.Where(a =>
                a.Specializations.Any(s => s.CategoryId == categoryId));
        }

        var workloads = agents.Select(a => new
        {
            AgentId = a.Id,
            a.FullName,
            a.MaxOpenTickets,
            OpenTicketCount = db.Set<Ticket>().Count(t =>
                t.AssignedAgentId == a.Id &&
                t.Status != TicketStatus.Resolved &&
                t.Status != TicketStatus.Closed &&
                (!excludingTicketId.HasValue ||
                    t.Id != excludingTicketId.Value))
        });

        return await workloads
            .Where(a => a.OpenTicketCount < a.MaxOpenTickets)
            .OrderBy(a => a.OpenTicketCount)
            .ThenBy(a => a.AgentId)
            .Select(a => new AgentAssignmentCandidate(
                a.AgentId,
                a.FullName,
                a.OpenTicketCount))
            .ToListAsync(ct);
    }
}