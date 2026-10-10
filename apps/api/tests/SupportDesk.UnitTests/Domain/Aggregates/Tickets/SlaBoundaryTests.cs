using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public sealed class SlaBoundaryTests
{
    private static readonly DateTime Start = FixedClock.DefaultNow;
    private static readonly DateTime Due = Start.AddHours(4);

    [Theory]
    [InlineData(179, SlaStatus.WithinSla)]
    [InlineData(180, SlaStatus.AtRisk)]
    [InlineData(181, SlaStatus.AtRisk)]
    [InlineData(240, SlaStatus.AtRisk)]
    [InlineData(241, SlaStatus.Breached)]
    public void Open_ticket_status_matches_window_boundaries(
        int elapsedMinutes,
        SlaStatus expected)
    {
        var actual = SlaEvaluator.Evaluate(
            Due, null, Start.AddMinutes(elapsedMinutes), Start);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void One_millisecond_before_risk_boundary_is_within_sla()
    {
        var actual = SlaEvaluator.Evaluate(
            Due, null, Start.AddHours(3).AddMilliseconds(-1), Start);

        Assert.Equal(SlaStatus.WithinSla, actual);
    }

    [Fact]
    public void One_millisecond_after_deadline_is_breached()
    {
        var actual = SlaEvaluator.Evaluate(
            Due, null, Due.AddMilliseconds(1), Start);

        Assert.Equal(SlaStatus.Breached, actual);
    }

    [Fact]
    public void Resolution_exactly_at_deadline_is_met()
    {
        var actual = SlaEvaluator.Evaluate(
            Due, Due, Due.AddDays(1), Start);

        Assert.Equal(SlaStatus.Met, actual);
    }

    [Fact]
    public void Resolution_after_deadline_is_breached()
    {
        var actual = SlaEvaluator.Evaluate(
            Due, Due.AddMilliseconds(1), Due.AddDays(1), Start);

        Assert.Equal(SlaStatus.Breached, actual);
    }

    [Fact]
    public void Risk_threshold_can_be_configured()
    {
        var actual = SlaEvaluator.Evaluate(
            Due, null, Start.AddHours(2), Start,
            atRiskThresholdPercent: 50);

        Assert.Equal(SlaStatus.AtRisk, actual);
    }

    [Fact]
    public void Reset_window_uses_its_new_start_time()
    {
        var newStart = Start.AddDays(1);
        var newDue = newStart.AddHours(4);

        var actual = SlaEvaluator.Evaluate(
            newDue, null, newStart.AddHours(1), newStart);

        Assert.Equal(SlaStatus.WithinSla, actual);
    }
}