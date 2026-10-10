using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;
using SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public sealed class EscalateTicketCommandHandlerTests
{
    private static EscalateTicketRequest ValidRequest() =>
        new("Customer impact has increased.", "Development Agent");

    [Fact]
    public async Task Escalation_recalculates_deadline_from_now_and_records_history()
    {
        var fixture = new Fixture();
        var oldDeadline = fixture.Ticket.DueAtUtc;
        fixture.Context.Clock.Advance(TimeSpan.FromHours(6));

        await fixture.Handler.HandleAsync(
            fixture.Ticket.Id, ValidRequest(), CancellationToken.None);

        Assert.Equal(TicketPriority.High, fixture.Ticket.Priority);
        Assert.Equal(
            fixture.Context.Clock.UtcNow.AddHours(4),
            fixture.Ticket.DueAtUtc);

        var history = Assert.Single(fixture.History);
        Assert.Equal(TicketPriority.Medium, history.FromPriority);
        Assert.Equal(TicketPriority.High, history.ToPriority);
        Assert.Equal(7, history.FromAgentId);
        Assert.Null(history.ToAgentId);
        Assert.Equal(oldDeadline, history.FromDueAtUtc);
        Assert.Equal(fixture.Ticket.DueAtUtc, history.ToDueAtUtc);
        Assert.Equal(ValidRequest().Reason, history.Reason);
        Assert.Equal(ValidRequest().EscalatedBy, history.EscalatedBy);
        Assert.Equal(
            fixture.Context.Clock.UtcNow,
            history.EscalatedAtUtc);

        fixture.Context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Eligible_current_owner_is_kept_even_if_another_agent_has_less_work()
    {
        var fixture = new Fixture();
        fixture.SetCandidates(
            new AgentAssignmentCandidate(9, "Sam", 0),
            new AgentAssignmentCandidate(7, "Alex", 1));

        await fixture.Handler.HandleAsync(
            fixture.Ticket.Id, ValidRequest(), CancellationToken.None);

        Assert.Equal(7, fixture.Ticket.AssignedAgentId);
        Assert.Equal(7, Assert.Single(fixture.History).ToAgentId);

        fixture.Context.AssignmentQueries.Verify(
            q => q.GetEligibleAsync(
                It.IsAny<int>(),
                It.IsAny<bool>(),
                fixture.Ticket.Id,
                It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task Ineligible_owner_is_replaced_with_first_eligible_agent()
    {
        var fixture = new Fixture();
        fixture.SetCandidates(
            new AgentAssignmentCandidate(9, "Sam", 0));

        await fixture.Handler.HandleAsync(
            fixture.Ticket.Id, ValidRequest(), CancellationToken.None);

        Assert.Equal(9, fixture.Ticket.AssignedAgentId);
        var history = Assert.Single(fixture.History);
        Assert.Equal(7, history.FromAgentId);
        Assert.Equal(9, history.ToAgentId);
    }

    [Fact]
    public async Task No_eligible_agent_leaves_ticket_unassigned()
    {
        var fixture = new Fixture();

        await fixture.Handler.HandleAsync(
            fixture.Ticket.Id, ValidRequest(), CancellationToken.None);

        Assert.Null(fixture.Ticket.AssignedAgentId);
        Assert.Null(Assert.Single(fixture.History).ToAgentId);
        fixture.Context.VerifySaved(Times.Once());
    }

    [Theory]
    [InlineData(TicketPriority.Critical, TicketStatus.New)]
    [InlineData(TicketPriority.High, TicketStatus.Resolved)]
    [InlineData(TicketPriority.High, TicketStatus.Closed)]
    public async Task Rejected_escalation_changes_nothing(
        TicketPriority priority,
        TicketStatus status)
    {
        var fixture = new Fixture(priority, status);
        var oldDeadline = fixture.Ticket.DueAtUtc;
        var oldUpdatedAt = fixture.Ticket.UpdatedAtUtc;

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            fixture.Handler.HandleAsync(
                fixture.Ticket.Id, ValidRequest(), CancellationToken.None));

        Assert.Equal(priority, fixture.Ticket.Priority);
        Assert.Equal(status, fixture.Ticket.Status);
        Assert.Equal(7, fixture.Ticket.AssignedAgentId);
        Assert.Equal(oldDeadline, fixture.Ticket.DueAtUtc);
        Assert.Equal(oldUpdatedAt, fixture.Ticket.UpdatedAtUtc);
        Assert.Empty(fixture.History);
        fixture.Context.VerifySaved(Times.Never());
    }

    [Theory]
    [InlineData("")]
    [InlineData("four")]
    [InlineData("     ")]
    public async Task Invalid_reason_is_rejected_before_changes(string reason)
    {
        var fixture = new Fixture();

        await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Handler.HandleAsync(
                fixture.Ticket.Id,
                new EscalateTicketRequest(reason, "Development Agent"),
                CancellationToken.None));

        Assert.Equal(TicketPriority.Medium, fixture.Ticket.Priority);
        Assert.Empty(fixture.History);
        fixture.Context.VerifySaved(Times.Never());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_actor_is_rejected(string actor)
    {
        var fixture = new Fixture();

        await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Handler.HandleAsync(
                fixture.Ticket.Id,
                new EscalateTicketRequest("Customer impact increased.", actor),
                CancellationToken.None));

        Assert.Empty(fixture.History);
        fixture.Context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Unknown_ticket_is_rejected()
    {
        var fixture = new Fixture();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            fixture.Handler.HandleAsync(
                999, ValidRequest(), CancellationToken.None));

        Assert.Empty(fixture.History);
        fixture.Context.VerifySaved(Times.Never());
    }

    private sealed class Fixture
    {
        public TicketCommandTestContext Context { get; } = new();

        public List<TicketEscalation> History { get; } = [];

        public Ticket Ticket { get; }

        public EscalateTicketCommandHandler Handler { get; }

        public Fixture(
            TicketPriority priority = TicketPriority.Medium,
            TicketStatus status = TicketStatus.New)
        {
            Ticket = Ticket.Raise(
                "TCK-001",
                "Portal issue",
                "The portal is unavailable.",
                1,
                3,
                priority,
                FixedClock.DefaultNow);

            Ticket.AssignTo(7, Context.Clock.UtcNow);
            Ticket.SetDueDate(
                Context.Clock.UtcNow.AddHours(24),
                Context.Clock.UtcNow);
            Ticket.ChangeStatus(status, Context.Clock.UtcNow);
            Context.ExistingTicket(Ticket);

            var escalations = new Mock<ITicketEscalationRepository>();

            escalations.Setup(e => e.AddAsync(
                    It.IsAny<TicketEscalation>(),
                    It.IsAny<CancellationToken>()))
                .Callback<TicketEscalation, CancellationToken>(
                    (history, _) => History.Add(history))
                .Returns(Task.CompletedTask);

            Handler = new EscalateTicketCommandHandler(
                Context.Tickets.Object,
                Context.Customers.Object,
                Context.Categories.Object,
                escalations.Object,
                Context.AssignmentQueries.Object,
                Context.UnitOfWork.Object,
                Context.Clock,
                new TicketTriagePolicy(new SlaOptions()),
                new GetTicketQueryHandler(Context.TicketQueries.Object));
        }

        public void SetCandidates(params AgentAssignmentCandidate[] candidates)
        {
            Context.AssignmentQueries.Setup(q => q.GetEligibleAsync(
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(candidates);
        }
    }
}