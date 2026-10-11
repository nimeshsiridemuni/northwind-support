using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.AssignTicket;
using SupportDesk.Application.Features.Tickets.Commands.ChangeTicketStatus;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;
using SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;
using SupportDesk.Application.Features.Tickets.Queries.SearchTickets;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/tickets")]
[Produces("application/json")]
[Authorize]
public sealed class TicketsController : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<TicketListItemDto>> Search(
        [FromQuery] TicketQuery query,
        [FromServices] SearchTicketsQueryHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(query, ct);

    [HttpGet("{id:int}")]
    public Task<TicketDetailDto> Get(
        int id,
        [FromServices] GetTicketQueryHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<TicketDetailDto>> Create(
        CreateTicketRequest request,
        [FromServices] RaiseTicketCommandHandler handler,
        CancellationToken ct)
    {
        var created = await handler.HandleAsync(request, ct);

        return CreatedAtAction(
            nameof(Get),
            new { id = created.Id },
            created);
    }

    [HttpPatch("{id:int}/status")]
    public Task<TicketDetailDto> UpdateStatus(
        int id,
        UpdateTicketStatusRequest request,
        [FromServices] ChangeTicketStatusCommandHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(id, request, ct);

    [HttpPatch("{id:int}/assignment")]
    public Task<TicketDetailDto> Assign(
        int id,
        AssignTicketRequest request,
        [FromServices] AssignTicketCommandHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(id, request, ct);

    [HttpPost("{id:int}/escalate")]
    public async Task<ActionResult<TicketDetailDto>> Escalate(
        int id,
        EscalateTicketBody body,
        [FromServices] EscalateTicketCommandHandler handler,
        CancellationToken ct)
    {
        var subject = User.FindFirstValue(
            JwtRegisteredClaimNames.Sub);

        var agentName = User.Identity?.Name;

        if (!int.TryParse(subject, out var agentId) ||
            agentId <= 0 ||
            string.IsNullOrWhiteSpace(agentName))
        {
            return Unauthorized();
        }

        // Keep the actor within the history column's 100-character limit.
        // Include the agent ID so agents with identical names remain distinct.
        var displayName = agentName.Length > 80
            ? agentName[..80]
            : agentName;

        var actor = $"{displayName} (#{agentId})";

        var request = new EscalateTicketRequest(
            body.Reason,
            actor);

        var updated = await handler.HandleAsync(id, request, ct);

        return Ok(updated);
    }

    [HttpGet("{id:int}/escalations")]
    public Task<IReadOnlyList<TicketEscalationDto>> GetEscalations(
        int id,
        [FromServices] GetTicketEscalationsQueryHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(id, ct);
}

public sealed record EscalateTicketBody(string Reason);