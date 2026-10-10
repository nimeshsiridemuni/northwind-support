using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.AssignTicket;
using SupportDesk.Application.Features.Tickets.Commands.ChangeTicketStatus;
using SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

internal sealed class TicketCommandTestContext
{
    public TicketCommandTestContext()
    {
        Customers.Setup(c => c.ExistsAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Categories.Setup(c => c.ExistsAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Customers.Setup(c => c.GetByIdAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer(
                "Contoso Ltd",
                "support@contoso.example",
                null,
                CustomerTier.Premium,
                FixedClock.DefaultNow));

        Categories.Setup(c => c.GetByIdAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category("General", false, false));

        AssignmentQueries.Setup(q => q.GetEligibleAsync(
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AgentAssignmentCandidate>());

        Tickets.Setup(t => t.NextReferenceAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("TCK-0041");

        Tickets.Setup(t => t.AddAsync(
                It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .Callback<Ticket, CancellationToken>(
                (ticket, _) => AddedTicket = ticket)
            .Returns(Task.CompletedTask);

        TicketQueries.Setup(q => q.GetDetailAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDetailDto
            {
                Id = 41,
                Reference = "TCK-0041",
                Customer = new CustomerContactDto(
                    1,
                    "Contoso Ltd",
                    "support@contoso.example",
                    null,
                    CustomerTier.Premium),
                Category = new CategoryDto(1, "General", false)
            });

        var getTicket = new GetTicketQueryHandler(TicketQueries.Object);

        RaiseTicket = new RaiseTicketCommandHandler(
            Tickets.Object,
            Customers.Object,
            Categories.Object,
            UnitOfWork.Object,
            Clock,
            new TicketTriagePolicy(new SlaOptions()),
            AssignmentQueries.Object,
            getTicket,
            NullLogger<RaiseTicketCommandHandler>.Instance);

        ChangeTicketStatus = new ChangeTicketStatusCommandHandler(
            Tickets.Object,
            UnitOfWork.Object,
            Clock,
            getTicket);

        AssignTicket = new AssignTicketCommandHandler(
            Tickets.Object,
            Agents.Object,
            UnitOfWork.Object,
            Clock,
            getTicket,
            NullLogger<AssignTicketCommandHandler>.Instance);
    }

    public Mock<ITicketRepository> Tickets { get; } = new();

    public Mock<ICustomerRepository> Customers { get; } = new();

    public Mock<ICategoryRepository> Categories { get; } = new();

    public Mock<IAgentRepository> Agents { get; } = new();

    public Mock<IAgentAssignmentQueries> AssignmentQueries { get; } = new();

    public Mock<IUnitOfWork> UnitOfWork { get; } = new();

    public Mock<ITicketQueries> TicketQueries { get; } = new();

    public FixedClock Clock { get; } = new();

    public RaiseTicketCommandHandler RaiseTicket { get; }

    public ChangeTicketStatusCommandHandler ChangeTicketStatus { get; }

    public AssignTicketCommandHandler AssignTicket { get; }

    public Ticket? AddedTicket { get; private set; }

    public Ticket ExistingTicket(Ticket ticket)
    {
        Tickets.Setup(t => t.GetByIdAsync(
                ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        return ticket;
    }

    public void ExistingAgent(
        int id,
        string fullName = "Alex Turner",
        bool isActive = true)
    {
        var agent = new Agent(
            fullName,
            $"agent{id}@example.com",
            maxOpenTickets: 10,
            Clock.UtcNow);

        if (!isActive)
        {
            agent.Deactivate();
        }

        Agents.Setup(a => a.GetByIdAsync(
                id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
    }

    public void VerifySaved(Times times) =>
        UnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            times);
}