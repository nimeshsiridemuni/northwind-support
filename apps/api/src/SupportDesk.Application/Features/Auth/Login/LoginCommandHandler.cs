using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Login;

public sealed class LoginCommandHandler(
    IAgentRepository agents,
    IAgentPasswordHasher passwordHasher,
    IAccessTokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork)
{
    public async Task<LoginResponse?> HandleAsync(
        LoginRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            request.Email.Length > 256 ||
            request.Password.Length > 1024)
        {
            return null;
        }

        var agent = await agents.GetByEmailAsync(request.Email, ct);

        if (agent is null ||
            !agent.IsActive ||
            string.IsNullOrWhiteSpace(agent.PasswordHash))
        {
            return null;
        }

        var result = passwordHasher.Verify(
            agent,
            agent.PasswordHash,
            request.Password);

        if (result == PasswordCheckResult.Failed)
        {
            return null;
        }

        if (result == PasswordCheckResult.SuccessRehashNeeded)
        {
            agent.SetPasswordHash(
                passwordHasher.Hash(agent, request.Password));

            await unitOfWork.SaveChangesAsync(ct);
        }

        return tokenIssuer.Issue(agent);
    }
}