using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class AgentRepository(SupportDbContext db) : IAgentRepository
{
    public Task<Agent?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Agent>()
            .Include(agent => agent.Specializations)
            .FirstOrDefaultAsync(agent => agent.Id == id, ct);

    public Task<Agent?> GetByEmailAsync(string email, CancellationToken ct)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        return db.Set<Agent>()
            .SingleOrDefaultAsync(
                agent => agent.Email.ToLower() == normalizedEmail,
                ct);
    }
}