using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class AgentAssignmentQueriesTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly SupportDbContext db;
    private readonly AgentAssignmentQueries queries;
    private readonly Customer customer;
    private readonly Category category;
    private readonly Agent alex;
    private readonly Agent sam;

    public AgentAssignmentQueriesTests()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        db = new SupportDbContext(options);
        db.Database.EnsureCreated();

        customer = new Customer(
            "Northwind", "customer@example.com", null,
            CustomerTier.Standard, FixedClock.DefaultNow);

        category = new Category("Technical", true, false);

        alex = new Agent(
            "Alex", "alex@example.com", 2, FixedClock.DefaultNow);

        sam = new Agent(
            "Sam", "sam@example.com", 2, FixedClock.DefaultNow);

        db.AddRange(customer, category, alex, sam);
        db.SaveChanges();

        queries = new AgentAssignmentQueries(db);
    }

    [Fact]
    public async Task Lowest_workload_is_selected_first()
    {
        AddTicket(alex, TicketStatus.New);
        await db.SaveChangesAsync();

        var candidates = await queries.GetEligibleAsync(
            category.Id, false, null, CancellationToken.None);

        Assert.Equal(2, candidates.Count);
        Assert.Equal(sam.Id, candidates[0].AgentId);
        Assert.Equal(0, candidates[0].OpenTicketCount);
        Assert.Equal(1, candidates[1].OpenTicketCount);
    }

    [Fact]
    public async Task Equal_workloads_are_ordered_by_agent_id()
    {
        var candidates = await queries.GetEligibleAsync(
            category.Id, false, null, CancellationToken.None);

        Assert.Equal(2, candidates.Count);
        Assert.Equal(
            new[] { Math.Min(alex.Id, sam.Id), Math.Max(alex.Id, sam.Id) },
            candidates.Select(c => c.AgentId).ToArray());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Specialization_is_required_only_when_category_requires_it(
        bool requiresSpecialist)
    {
        alex.AddSpecialization(category.Id);
        await db.SaveChangesAsync();

        var candidates = await queries.GetEligibleAsync(
            category.Id, requiresSpecialist, null, CancellationToken.None);

        if (requiresSpecialist)
        {
            Assert.Equal(alex.Id, Assert.Single(candidates).AgentId);
        }
        else
        {
            Assert.Equal(2, candidates.Count);
        }
    }

    [Fact]
    public async Task Inactive_and_full_agents_are_excluded()
    {
        alex.Deactivate();
        AddTicket(sam, TicketStatus.New);
        AddTicket(sam, TicketStatus.InProgress);
        await db.SaveChangesAsync();

        var candidates = await queries.GetEligibleAsync(
            category.Id, false, null, CancellationToken.None);

        Assert.Empty(candidates);
    }

    [Fact]
    public async Task Resolved_and_closed_tickets_do_not_use_capacity()
    {
        AddTicket(alex, TicketStatus.Resolved);
        AddTicket(alex, TicketStatus.Closed);
        sam.Deactivate();
        await db.SaveChangesAsync();

        var candidates = await queries.GetEligibleAsync(
            category.Id, false, null, CancellationToken.None);

        var candidate = Assert.Single(candidates);
        Assert.Equal(alex.Id, candidate.AgentId);
        Assert.Equal(0, candidate.OpenTicketCount);
    }

    [Fact]
    public async Task Escalated_ticket_can_be_excluded_from_owners_workload()
    {
        var ticket = AddTicket(alex, TicketStatus.InProgress);
        AddTicket(alex, TicketStatus.Open);
        sam.Deactivate();
        await db.SaveChangesAsync();

        var candidates = await queries.GetEligibleAsync(
            category.Id, false, ticket.Id, CancellationToken.None);

        var candidate = Assert.Single(candidates);
        Assert.Equal(alex.Id, candidate.AgentId);
        Assert.Equal(1, candidate.OpenTicketCount);
    }

    private Ticket AddTicket(Agent agent, TicketStatus status)
    {
        var ticket = Ticket.Raise(
            $"TCK-{Guid.NewGuid():N}"[..20],
            "Technical issue",
            "Please investigate this issue.",
            customer.Id,
            category.Id,
            TicketPriority.Medium,
            FixedClock.DefaultNow);

        ticket.AssignTo(agent.Id, FixedClock.DefaultNow);
        ticket.ChangeStatus(status, FixedClock.DefaultNow);
        db.Add(ticket);

        return ticket;
    }

    public void Dispose()
    {
        db.Dispose();
        connection.Dispose();
    }
}