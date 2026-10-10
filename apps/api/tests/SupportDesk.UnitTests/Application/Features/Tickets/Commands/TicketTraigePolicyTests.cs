using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public sealed class TicketTriagePolicyTests
{
    private readonly TicketTriagePolicy policy = new(new SlaOptions());

    [Theory]
    [InlineData(TicketPriority.Low, 72)]
    [InlineData(TicketPriority.Medium, 24)]
    [InlineData(TicketPriority.High, 8)]
    [InlineData(TicketPriority.Critical, 4)]
    public void Standard_customers_receive_the_configured_window(
        TicketPriority priority, double expectedHours)
    {
        var window = policy.CalculateWindow(priority, CustomerTier.Standard);

        Assert.Equal(TimeSpan.FromHours(expectedHours), window);
    }

    [Theory]
    [InlineData(TicketPriority.Low, 36)]
    [InlineData(TicketPriority.Medium, 12)]
    [InlineData(TicketPriority.High, 4)]
    [InlineData(TicketPriority.Critical, 2)]
    public void Premium_customers_receive_half_the_standard_window(
        TicketPriority priority, double expectedHours)
    {
        var window = policy.CalculateWindow(priority, CustomerTier.Premium);

        Assert.Equal(TimeSpan.FromHours(expectedHours), window);
    }

    [Fact]
    public void Premium_window_cannot_fall_below_one_hour()
    {
        var options = new SlaOptions
        {
            PremiumCustomerMultiplier = 0.1
        };
        var customPolicy = new TicketTriagePolicy(options);

        var window = customPolicy.CalculateWindow(
            TicketPriority.Critical, CustomerTier.Premium);

        Assert.Equal(TimeSpan.FromHours(1), window);
    }

    [Fact]
    public void Window_uses_configuration_values()
    {
        var options = new SlaOptions();
        options.BaseWindowHours["High"] = 10;
        var customPolicy = new TicketTriagePolicy(options);

        var window = customPolicy.CalculateWindow(
            TicketPriority.High, CustomerTier.Standard);

        Assert.Equal(TimeSpan.FromHours(10), window);
    }

    [Fact]
    public void Missing_requested_priority_defaults_to_medium()
    {
        var category = new Category("Billing", false, false);

        var priority = policy.DeterminePriority(null, category);

        Assert.Equal(TicketPriority.Medium, priority);
    }

    [Theory]
    [InlineData(TicketPriority.Low)]
    [InlineData(TicketPriority.Medium)]
    [InlineData(TicketPriority.High)]
    [InlineData(TicketPriority.Critical)]
    public void Normal_category_preserves_requested_priority(
        TicketPriority requested)
    {
        var category = new Category("Billing", false, false);

        Assert.Equal(requested, policy.DeterminePriority(requested, category));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(TicketPriority.Low)]
    [InlineData(TicketPriority.High)]
    public void Category_flag_forces_critical_regardless_of_name(
        TicketPriority? requested)
    {
        var category = new Category("Any category name", false, true);

        var priority = policy.DeterminePriority(requested, category);

        Assert.Equal(TicketPriority.Critical, priority);
    }
}