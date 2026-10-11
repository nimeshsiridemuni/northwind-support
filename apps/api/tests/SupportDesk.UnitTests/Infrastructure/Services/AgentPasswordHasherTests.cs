using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Services;

namespace SupportDesk.UnitTests.Infrastructure.Services;

public sealed class AgentPasswordHasherTests
{
    [Fact]
    public void SamePassword_ProducesDifferentSaltedHashes()
    {
        var hasher = new AgentPasswordHasher();
        var agent = MakeAgent();

        var first = hasher.Hash(agent, "Local_Agent_Password1");
        var second = hasher.Hash(agent, "Local_Agent_Password1");

        Assert.NotEqual(first, second);
        Assert.NotEqual("Local_Agent_Password1", first);

        Assert.Equal(
            PasswordCheckResult.Success,
            hasher.Verify(agent, first, "Local_Agent_Password1"));

        Assert.Equal(
            PasswordCheckResult.Success,
            hasher.Verify(agent, second, "Local_Agent_Password1"));
    }

    [Fact]
    public void WrongPassword_FailsVerification()
    {
        var hasher = new AgentPasswordHasher();
        var agent = MakeAgent();

        var hash = hasher.Hash(agent, "Local_Agent_Password1");

        Assert.Equal(
            PasswordCheckResult.Failed,
            hasher.Verify(agent, hash, "incorrect-password"));
    }

    [Fact]
    public void MalformedHash_FailsVerification()
    {
        var hasher = new AgentPasswordHasher();

        Assert.Equal(
            PasswordCheckResult.Failed,
            hasher.Verify(
                MakeAgent(),
                "not-a-valid-base64-hash",
                "Local_Agent_Password1"));
    }

    private static Agent MakeAgent() => new(
        "Alex Turner",
        "alex.turner@northwind-support.example",
        10,
        DateTime.UtcNow);
}