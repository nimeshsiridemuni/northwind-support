using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    ITicketEscalationRepository escalations,
    IAgentAssignmentQueries assignmentQueries,
    IUnitOfWork unitOfWork,
    IClock clock,
    TicketTriagePolicy triagePolicy,
    GetTicketQueryHandler getTicket)
{
    public async Task<TicketDetailDto> HandleAsync(
        int ticketId,
        EscalateTicketRequest request,
        CancellationToken ct)
    {
        var validation = await new EscalateTicketRequestValidator()
            .ValidateAsync(request, ct);

        if (!validation.IsValid)
        {
            throw new ValidationException(
                validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(e => e.ErrorMessage).ToArray()));
        }

        var ticket = await tickets.GetByIdAsync(ticketId, ct)
            ?? throw new NotFoundException("Ticket", ticketId);

        if (!ticket.IsOpen)
        {
            throw new BusinessRuleViolationException(
                "Resolved or closed tickets cannot be escalated.");
        }

        if (ticket.Priority == TicketPriority.Critical)
        {
            throw new BusinessRuleViolationException(
                "The ticket is already Critical and cannot be escalated further.");
        }

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);

        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var now = clock.UtcNow;
        var nextPriority = (TicketPriority)((int)ticket.Priority + 1);
        var window = triagePolicy.CalculateWindow(nextPriority, customer.Tier);
        var newDeadline = now.Add(window);

        var candidates = await assignmentQueries.GetEligibleAsync(
            category.Id,
            category.RequiresSpecialist,
            ticket.Id,
            ct);

        var selectedAgent = candidates.FirstOrDefault(
                candidate => candidate.AgentId == ticket.AssignedAgentId)
            ?? candidates.FirstOrDefault();

        var history = new TicketEscalation(
            ticket.Id,
            ticket.Priority,
            nextPriority,
            ticket.AssignedAgentId,
            selectedAgent?.AgentId,
            ticket.DueAtUtc,
            newDeadline,
            request.Reason,
            request.EscalatedBy,
            now);

        ticket.EscalatePriority(newDeadline, now);
        ticket.AssignTo(selectedAgent?.AgentId, now);

        await escalations.AddAsync(history, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return await getTicket.HandleAsync(ticket.Id, ct);
    }
}