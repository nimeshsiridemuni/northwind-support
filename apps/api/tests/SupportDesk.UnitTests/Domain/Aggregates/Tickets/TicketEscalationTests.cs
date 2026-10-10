using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public sealed class TicketEscalationTests
{
    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public void Escalation_increases_priority_by_one_level(
        TicketPriority original,
        TicketPriority expected)
    {
        var ticket = CreateTicket(original);
        var now = FixedClock.DefaultNow.AddHours(5);
        var newDeadline = now.AddHours(4);

        ticket.EscalatePriority(newDeadline, now);

        Assert.Equal(expected, ticket.Priority);
        Assert.Equal(newDeadline, ticket.DueAtUtc);
        Assert.Equal(now, ticket.UpdatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, ticket.CreatedAtUtc);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void Resolved_and_closed_tickets_reject_escalation(
        TicketStatus status)
    {
        var ticket = CreateTicket(TicketPriority.High);
        ticket.ChangeStatus(status, FixedClock.DefaultNow);
        var previousDeadline = ticket.DueAtUtc;
        var previousUpdatedAt = ticket.UpdatedAtUtc;

        Assert.Throws<BusinessRuleViolationException>(() =>
            ticket.EscalatePriority(
                FixedClock.DefaultNow.AddHours(2),
                FixedClock.DefaultNow.AddHours(1)));

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(previousDeadline, ticket.DueAtUtc);
        Assert.Equal(previousUpdatedAt, ticket.UpdatedAtUtc);
        Assert.Equal(status, ticket.Status);
    }

    [Fact]
    public void Critical_ticket_rejects_escalation_without_changes()
    {
        var ticket = CreateTicket(TicketPriority.Critical);
        var previousDeadline = ticket.DueAtUtc;

        Assert.Throws<BusinessRuleViolationException>(() =>
            ticket.EscalatePriority(
                FixedClock.DefaultNow.AddHours(2),
                FixedClock.DefaultNow.AddHours(1)));

        Assert.Equal(TicketPriority.Critical, ticket.Priority);
        Assert.Equal(previousDeadline, ticket.DueAtUtc);
        Assert.Equal(FixedClock.DefaultNow, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void Invalid_deadline_rejects_escalation_without_changes()
    {
        var ticket = CreateTicket(TicketPriority.Low);
        var previousDeadline = ticket.DueAtUtc;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ticket.EscalatePriority(
                FixedClock.DefaultNow,
                FixedClock.DefaultNow));

        Assert.Equal(TicketPriority.Low, ticket.Priority);
        Assert.Equal(previousDeadline, ticket.DueAtUtc);
    }

    private static Ticket CreateTicket(TicketPriority priority)
    {
        var ticket = Ticket.Raise(
            "TCK-001",
            "Portal issue",
            "The portal is unavailable.",
            1,
            1,
            priority,
            FixedClock.DefaultNow);

        ticket.SetDueDate(
            FixedClock.DefaultNow.AddHours(8),
            FixedClock.DefaultNow);

        return ticket;
    }
}