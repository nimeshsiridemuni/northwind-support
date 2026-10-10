using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketRequestValidator
    : AbstractValidator<EscalateTicketRequest>
{
    public EscalateTicketRequestValidator()
    {
        RuleFor(x => x.Reason)
            .Must(reason =>
                !string.IsNullOrWhiteSpace(reason) &&
                reason.Trim().Length is >= 5 and <= 500)
            .WithMessage(
                "The reason must contain between 5 and 500 characters.");

        RuleFor(x => x.EscalatedBy)
            .Must(actor =>
                !string.IsNullOrWhiteSpace(actor) &&
                actor.Trim().Length <= 100)
            .WithMessage(
                "An actor is required and must not exceed 100 characters.");
    }
}