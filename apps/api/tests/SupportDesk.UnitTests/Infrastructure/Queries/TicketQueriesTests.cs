using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class TicketQueriesTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly SupportDbContext db;
    private readonly TicketQueries queries;
    private readonly Customer northwind;
    private readonly Customer contoso;
    private readonly Category category;

    public TicketQueriesTests()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        db = new SupportDbContext(options);
        db.Database.EnsureCreated();

        northwind = new Customer(
            "Northwind Traders", "northwind@example.com", null,
            CustomerTier.Standard, FixedClock.DefaultNow);

        contoso = new Customer(
            "Contoso Services", "contoso@example.com", null,
            CustomerTier.Standard, FixedClock.DefaultNow);

        category = new Category(
            "Billing", requiresSpecialist: false,
            forcesCriticalPriority: false);

        db.AddRange(northwind, contoso, category);
        db.SaveChanges();

        queries = new TicketQueries(db, new FixedClock());
    }

    [Fact]
    public async Task Priority_filter_returns_exact_matches_and_counts_before_paging()
    {
        AddTicket("TCK-001", northwind, TicketPriority.High);
        AddTicket("TCK-002", northwind, TicketPriority.High);
        AddTicket("TCK-003", northwind, TicketPriority.Critical);
        AddTicket("TCK-004", northwind, TicketPriority.Low);
        await db.SaveChangesAsync();

        var result = await queries.GetPagedAsync(
            new TicketQuery
            {
                Priority = TicketPriority.High,
                Page = 1,
                PageSize = 1
            },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(TicketPriority.High, result.Items[0].Priority);
    }

    [Fact]
    public async Task Supplied_filters_are_combined_with_and()
    {
        var expected = AddTicket(
            "TCK-001", northwind,
            TicketPriority.High, TicketStatus.InProgress);

        AddTicket("TCK-002", northwind,
            TicketPriority.High, TicketStatus.New);

        AddTicket("TCK-003", northwind,
            TicketPriority.Low, TicketStatus.InProgress);

        AddTicket("TCK-004", contoso,
            TicketPriority.High, TicketStatus.InProgress);

        await db.SaveChangesAsync();

        var result = await queries.GetPagedAsync(
            new TicketQuery
            {
                Priority = TicketPriority.High,
                Status = TicketStatus.InProgress,
                CustomerId = northwind.Id
            },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Items);
        Assert.Equal(expected.Id, item.Id);
    }

    [Fact]
    public async Task Search_matches_customer_name_and_trims_whitespace()
    {
        var expected = AddTicket(
            "TCK-001", northwind, TicketPriority.Medium);

        AddTicket("TCK-002", contoso, TicketPriority.Medium);
        await db.SaveChangesAsync();

        var result = await queries.GetPagedAsync(
            new TicketQuery { Search = "  Northwind  " },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Items);
        Assert.Equal(expected.Id, item.Id);
    }

    private Ticket AddTicket(
        string reference,
        Customer customer,
        TicketPriority priority,
        TicketStatus status = TicketStatus.New)
    {
        var ticket = Ticket.Raise(
            reference,
            "Invoice question",
            "Please help with this invoice.",
            customer.Id,
            category.Id,
            priority,
            FixedClock.DefaultNow);

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