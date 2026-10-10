namespace SupportDesk.Domain.Aggregates.Tickets;

public static class SlaEvaluator
{
    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc,
        DateTime? startedAtUtc = null,
        double atRiskThresholdPercent = 25)
    {
        if (dueAtUtc is null)
        {
            return SlaStatus.NotApplicable;
        }

        if (resolvedAtUtc is not null)
        {
            return resolvedAtUtc <= dueAtUtc
                ? SlaStatus.Met
                : SlaStatus.Breached;
        }

        if (nowUtc > dueAtUtc.Value)
        {
            return SlaStatus.Breached;
        }

        if (nowUtc == dueAtUtc.Value)
        {
            return SlaStatus.AtRisk;
        }

        if (startedAtUtc is null)
        {
            return SlaStatus.WithinSla;
        }

        var totalWindow = dueAtUtc.Value - startedAtUtc.Value;
        var remaining = dueAtUtc.Value - nowUtc;

        if (totalWindow <= TimeSpan.Zero)
        {
            return SlaStatus.WithinSla;
        }

        var remainingPercent =
            remaining.TotalMilliseconds / totalWindow.TotalMilliseconds * 100;

        return remainingPercent <= atRiskThresholdPercent
            ? SlaStatus.AtRisk
            : SlaStatus.WithinSla;
    }
}