using SupportDesk.Domain.Common;
using SupportDesk.Domain.Exceptions;

namespace SupportDesk.Domain.Aggregates.Tickets;

public sealed class Ticket : AggregateRoot
{
    private Ticket()
    {
        // For EF Core.
    }

    public string Reference { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int CustomerId { get; private set; }

    public int CategoryId { get; private set; }

    public int? AssignedAgentId { get; private set; }

    public TicketPriority Priority { get; private set; }

    public TicketStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime? DueAtUtc { get; private set; }

    public DateTime? SlaStartedAtUtc { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    public bool IsOpen =>
        Status is not (TicketStatus.Resolved or TicketStatus.Closed);

    public static Ticket Raise(
        string reference,
        string title,
        string description,
        int customerId,
        int categoryId,
        TicketPriority priority,
        DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new Ticket
        {
            Reference = reference,
            Title = title.Trim(),
            Description = description.Trim(),
            CustomerId = customerId,
            CategoryId = categoryId,
            Priority = priority,
            Status = TicketStatus.New,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public void ChangeStatus(TicketStatus status, DateTime nowUtc)
    {
        if (Status == TicketStatus.Closed && status != TicketStatus.Closed)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is closed and cannot be moved to {status}.");
        }

        ResolvedAtUtc = status switch
        {
            TicketStatus.Resolved => nowUtc,
            TicketStatus.Closed => ResolvedAtUtc ?? nowUtc,
            _ => null
        };

        Status = status;
        UpdatedAtUtc = nowUtc;
    }

    public void AssignTo(int? agentId, DateTime nowUtc)
    {
        AssignedAgentId = agentId;
        UpdatedAtUtc = nowUtc;
    }

    public void SetDueDate(DateTime dueAtUtc, DateTime nowUtc)
    {
        if (dueAtUtc <= nowUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dueAtUtc),
                "The SLA deadline must be later than the current time.");
        }

        DueAtUtc = dueAtUtc;
        SlaStartedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void EscalatePriority(DateTime newDueAtUtc, DateTime nowUtc)
    {
        if (!IsOpen)
        {
            throw new BusinessRuleViolationException(
                "Resolved or closed tickets cannot be escalated.");
        }

        if (Priority == TicketPriority.Critical)
        {
            throw new BusinessRuleViolationException(
                "The ticket is already Critical and cannot be escalated further.");
        }

        SetDueDate(newDueAtUtc, nowUtc);
        Priority = (TicketPriority)((int)Priority + 1);
    }
}