namespace SupportDesk.Application.Contracts.Tickets;

public sealed record TriageResultDto(
    string PriorityReason,
    double SlaWindowHours,
    string SlaReason,
    string AssignmentReason);