using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public sealed class LoginCommandHandlerTests
{
    [Fact]
    public async Task ValidCredentials_ReturnTokenWithoutSaving()
    {
        var context = new TestContext();

        var result = await context.Handler.HandleAsync(
            context.Request,
            CancellationToken.None);

        Assert.Same(context.Response, result);

        context.TokenIssuer.Verify(
            issuer => issuer.Issue(context.Agent),
            Times.Once);

        context.VerifyNotSaved();
    }

    [Fact]
    public async Task WrongPassword_DoesNotIssueTokenOrSave()
    {
        var context = new TestContext();

        context.PasswordHasher
            .Setup(hasher => hasher.Verify(
                context.Agent,
                "stored-hash",
                context.Request.Password))
            .Returns(PasswordCheckResult.Failed);

        var result = await context.Handler.HandleAsync(
            context.Request,
            CancellationToken.None);

        Assert.Null(result);
        context.VerifyNoToken();
        context.VerifyNotSaved();
    }

    [Fact]
    public async Task InactiveAgent_CannotLogIn()
    {
        var context = new TestContext();
        context.Agent.Deactivate();

        var result = await context.Handler.HandleAsync(
            context.Request,
            CancellationToken.None);

        Assert.Null(result);

        context.PasswordHasher.Verify(
            hasher => hasher.Verify(
                It.IsAny<Agent>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);

        context.VerifyNoToken();
        context.VerifyNotSaved();
    }

    [Fact]
    public async Task UnknownEmail_DoesNotIssueToken()
    {
        var context = new TestContext();

        context.Agents
            .Setup(repository => repository.GetByEmailAsync(
                context.Request.Email,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        var result = await context.Handler.HandleAsync(
            context.Request,
            CancellationToken.None);

        Assert.Null(result);
        context.VerifyNoToken();
        context.VerifyNotSaved();
    }

    [Fact]
    public async Task AgentWithoutPasswordHash_CannotLogIn()
    {
        var context = new TestContext();

        var agent = new Agent(
            "New Agent",
            context.Request.Email,
            10,
            DateTime.UtcNow);

        context.Agents
            .Setup(repository => repository.GetByEmailAsync(
                context.Request.Email,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var result = await context.Handler.HandleAsync(
            context.Request,
            CancellationToken.None);

        Assert.Null(result);
        context.VerifyNoToken();
        context.VerifyNotSaved();
    }

    [Fact]
    public async Task OlderHash_IsReplacedBeforeIssuingToken()
    {
        var context = new TestContext();

        context.PasswordHasher
            .Setup(hasher => hasher.Verify(
                context.Agent,
                "stored-hash",
                context.Request.Password))
            .Returns(PasswordCheckResult.SuccessRehashNeeded);

        context.PasswordHasher
            .Setup(hasher => hasher.Hash(
                context.Agent,
                context.Request.Password))
            .Returns("upgraded-hash");

        var result = await context.Handler.HandleAsync(
            context.Request,
            CancellationToken.None);

        Assert.Same(context.Response, result);
        Assert.Equal("upgraded-hash", context.Agent.PasswordHash);

        context.UnitOfWork.Verify(
            unit => unit.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);

        context.TokenIssuer.Verify(
            issuer => issuer.Issue(context.Agent),
            Times.Once);
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData("   ", "password")]
    [InlineData("agent@example.com", "")]
    [InlineData("agent@example.com", "   ")]
    public async Task EmptyCredentials_AreRejectedBeforeRepositoryAccess(
        string email,
        string password)
    {
        var context = new TestContext();

        var result = await context.Handler.HandleAsync(
            new LoginRequest(email, password),
            CancellationToken.None);

        Assert.Null(result);

        context.Agents.Verify(
            repository => repository.GetByEmailAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        context.VerifyNoToken();
        context.VerifyNotSaved();
    }

    private sealed class TestContext
    {
        public TestContext()
        {
            Agent.SetPasswordHash("stored-hash");

            Agents
                .Setup(repository => repository.GetByEmailAsync(
                    Request.Email,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Agent);

            PasswordHasher
                .Setup(hasher => hasher.Verify(
                    Agent,
                    "stored-hash",
                    Request.Password))
                .Returns(PasswordCheckResult.Success);

            TokenIssuer
                .Setup(issuer => issuer.Issue(Agent))
                .Returns(Response);

            UnitOfWork
                .Setup(unit => unit.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            Handler = new LoginCommandHandler(
                Agents.Object,
                PasswordHasher.Object,
                TokenIssuer.Object,
                UnitOfWork.Object);
        }

        public Agent Agent { get; } = new(
            "Alex Turner",
            "alex.turner@northwind-support.example",
            10,
            DateTime.UtcNow);

        public LoginRequest Request { get; } = new(
            "alex.turner@northwind-support.example",
            "Local_Agent_Password1");

        public LoginResponse Response { get; } = new(
            "issued-token",
            DateTime.UtcNow.AddMinutes(30),
            new AuthenticatedAgentDto(
                1,
                "Alex Turner",
                "alex.turner@northwind-support.example"));

        public Mock<IAgentRepository> Agents { get; } = new();

        public Mock<IAgentPasswordHasher> PasswordHasher { get; } = new();

        public Mock<IAccessTokenIssuer> TokenIssuer { get; } = new();

        public Mock<IUnitOfWork> UnitOfWork { get; } = new();

        public LoginCommandHandler Handler { get; }

        public void VerifyNoToken() =>
            TokenIssuer.Verify(
                issuer => issuer.Issue(It.IsAny<Agent>()),
                Times.Never);

        public void VerifyNotSaved() =>
            UnitOfWork.Verify(
                unit => unit.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
                Times.Never);
    }
}