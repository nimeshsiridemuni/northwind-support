using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IUnitOfWork unitOfWork,
    IClock clock,
    TicketTriagePolicy triagePolicy,
    IAgentAssignmentQueries assignmentQueries,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger)
{
    public async Task<TicketDetailDto> HandleAsync(
        CreateTicketRequest request,
        CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(request.CustomerId, ct)
            ?? throw new NotFoundException("Customer", request.CustomerId);

        var category = await categories.GetByIdAsync(request.CategoryId, ct)
            ?? throw new NotFoundException("Category", request.CategoryId);

        var now = clock.UtcNow;

        var priority = triagePolicy.DeterminePriority(
            request.RequestedPriority, category);

        var window = triagePolicy.CalculateWindow(priority, customer.Tier);

        var candidates = await assignmentQueries.GetEligibleAsync(
            category.Id,
            category.RequiresSpecialist,
            excludingTicketId: null,
            ct);

        var selectedAgent = candidates.FirstOrDefault();

        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            priority,
            now);

        ticket.SetDueDate(now.Add(window), now);
        ticket.AssignTo(selectedAgent?.AgentId, now);

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId} in category {CategoryId}, assigned to {AgentId}.",
            ticket.Reference,
            ticket.CustomerId,
            ticket.CategoryId,
            ticket.AssignedAgentId);

        var priorityReason = category.ForcesCriticalPriority
            ? "The category requires Critical priority."
            : request.RequestedPriority.HasValue
                ? $"The requested priority, {priority}, was applied."
                : "No priority was requested; Medium was applied.";

        var slaReason = customer.Tier == CustomerTier.Premium
            ? "The configured priority window was adjusted for a Premium customer, with the minimum window enforced."
            : "The configured priority window was applied, with the minimum window enforced.";

        var assignmentReason = selectedAgent is null
            ? category.RequiresSpecialist
                ? "No active specialist has capacity; the ticket remains unassigned."
                : "No active agent has capacity; the ticket remains unassigned."
            : $"Assigned to {selectedAgent.FullName}, who has the lowest eligible workload ({selectedAgent.OpenTicketCount} open tickets). Ties use the lowest agent ID.";

        var result = await getTicket.HandleAsync(ticket.Id, ct);

        result.Triage = new TriageResultDto(
            priorityReason,
            window.TotalHours,
            slaReason,
            assignmentReason);

        return result;
    }
}