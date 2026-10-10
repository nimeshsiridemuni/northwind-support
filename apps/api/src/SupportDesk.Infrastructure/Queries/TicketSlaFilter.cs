using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Queries;

public static class TicketSlaFilter
{
    public static IQueryable<Ticket> Apply(
        IQueryable<Ticket> source,
        SlaStatus? status,
        DateTime now,
        double riskThresholdPercent)
    {
        if (status is null)
        {
            return source;
        }

        switch (status.Value)
        {
            case SlaStatus.NotApplicable:
                return source.Where(t => t.DueAtUtc == null);

            case SlaStatus.Met:
                return source.Where(t =>
                    t.DueAtUtc != null &&
                    t.ResolvedAtUtc != null &&
                    t.ResolvedAtUtc <= t.DueAtUtc);

            case SlaStatus.Breached:
                return source.Where(t =>
                    t.DueAtUtc != null &&
                    (
                        (t.ResolvedAtUtc != null &&
                            t.ResolvedAtUtc > t.DueAtUtc) ||
                        (t.ResolvedAtUtc == null &&
                            t.DueAtUtc < now)
                    ));

            case SlaStatus.AtRisk:
            case SlaStatus.WithinSla:
                break;

            default:
                throw new ValidationException(
                    new Dictionary<string, string[]>
                    {
                        ["SlaStatus"] = ["Unknown SLA status."]
                    });
        }

        var open = source.Where(t =>
            t.DueAtUtc != null &&
            t.ResolvedAtUtc == null &&
            t.DueAtUtc >= now);

        var windows = open.Select(t => new
        {
            Ticket = t,
            Start = t.SlaStartedAtUtc ?? t.CreatedAtUtc,
            Due = t.DueAtUtc!.Value
        });

        // DATEDIFF counts second boundaries. Fractional parts correct
        // the result so exact SLA boundaries match the domain evaluator.
        var durations = windows.Select(x => new
        {
            x.Ticket,
            x.Due,
            TotalSeconds =
                (double)EF.Functions.DateDiffSecond(x.Start, x.Due) +
                (x.Due.Millisecond - x.Start.Millisecond) / 1000.0 +
                (x.Due.Microsecond - x.Start.Microsecond) / 1000000.0 +
                (x.Due.Nanosecond - x.Start.Nanosecond) / 1000000000.0,
            RemainingSeconds =
                (double)EF.Functions.DateDiffSecond(now, x.Due) +
                (x.Due.Millisecond - now.Millisecond) / 1000.0 +
                (x.Due.Microsecond - now.Microsecond) / 1000000.0 +
                (x.Due.Nanosecond - now.Nanosecond) / 1000000000.0
        });

        var atRisk = durations
            .Where(x =>
                x.Due == now ||
                (x.TotalSeconds > 0 &&
                    x.RemainingSeconds * 100 <=
                    x.TotalSeconds * riskThresholdPercent))
            .Select(x => x.Ticket);

        if (status == SlaStatus.AtRisk)
        {
            return atRisk;
        }

        var atRiskIds = atRisk.Select(t => t.Id);

        return open.Where(t => !atRiskIds.Contains(t.Id));
    }
}