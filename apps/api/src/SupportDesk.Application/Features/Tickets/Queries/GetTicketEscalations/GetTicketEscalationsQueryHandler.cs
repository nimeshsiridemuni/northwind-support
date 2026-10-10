using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;

public sealed class GetTicketEscalationsQueryHandler(
    GetTicketQueryHandler getTicket,
    ITicketEscalationRepository escalations)
{
    public async Task<IReadOnlyList<TicketEscalationDto>> HandleAsync(
        int ticketId,
        CancellationToken ct)
    {
        // An unknown ticket should return 404, rather than an empty history.
        await getTicket.HandleAsync(ticketId, ct);

        var history = await escalations.GetForTicketAsync(ticketId, ct);

        return history.Select(e => new TicketEscalationDto(
            e.Id,
            e.TicketId,
            e.FromPriority,
            e.ToPriority,
            e.FromAgentId,
            e.ToAgentId,
            e.FromDueAtUtc,
            e.ToDueAtUtc,
            e.Reason,
            e.EscalatedBy,
            e.EscalatedAtUtc)).ToArray();
    }
}