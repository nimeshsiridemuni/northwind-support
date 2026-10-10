using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public sealed class RaiseTicketAssignmentTests
{
    private static CreateTicketRequest Request() =>
        new(
            "Cannot access the portal",
            "The portal is unavailable for our team.",
            1,
            3,
            TicketPriority.Low);

    [Fact]
    public async Task Creation_assigns_the_first_eligible_agent()
    {
        var context = new TicketCommandTestContext();

        context.AssignmentQueries
            .Setup(q => q.GetEligibleAsync(
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new AgentAssignmentCandidate(7, "Alex", 1),
                new AgentAssignmentCandidate(9, "Sam", 2)
            });

        await context.RaiseTicket.HandleAsync(
            Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(7, context.AddedTicket.AssignedAgentId);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Creation_saves_an_unassigned_ticket_when_no_agent_is_available()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(
            Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Null(context.AddedTicket.AssignedAgentId);
        Assert.NotNull(context.AddedTicket.DueAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Creation_applies_the_category_priority_and_specialist_rules()
    {
        var context = new TicketCommandTestContext();

        context.Categories
            .Setup(c => c.GetByIdAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category("Any category", true, true));

        await context.RaiseTicket.HandleAsync(
            Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Critical, context.AddedTicket.Priority);
        Assert.Equal(
            context.Clock.UtcNow.AddHours(2),
            context.AddedTicket.DueAtUtc);

        context.AssignmentQueries.Verify(
            q => q.GetEligibleAsync(
                It.IsAny<int>(),
                true,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once());
    }
}