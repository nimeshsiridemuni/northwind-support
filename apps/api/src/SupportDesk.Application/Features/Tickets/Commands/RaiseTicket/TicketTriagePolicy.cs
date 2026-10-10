using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

public sealed class TicketTriagePolicy(SlaOptions options)
{
    public TicketPriority DeterminePriority(
        TicketPriority? requestedPriority,
        Category category)
    {
        return category.ForcesCriticalPriority
            ? TicketPriority.Critical
            : requestedPriority ?? TicketPriority.Medium;
    }

    public TimeSpan CalculateWindow(
        TicketPriority priority,
        CustomerTier customerTier)
    {
        if (!options.BaseWindowHours.TryGetValue(
                priority.ToString(), out var hours))
        {
            throw new InvalidOperationException(
                $"No SLA window is configured for priority {priority}.");
        }

        if (customerTier == CustomerTier.Premium)
        {
            hours *= options.PremiumCustomerMultiplier;
        }

        hours = Math.Max(hours, options.MinimumWindowHours);

        return TimeSpan.FromHours(hours);
    }
}