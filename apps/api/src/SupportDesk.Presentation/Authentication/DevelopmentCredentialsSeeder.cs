using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Presentation.Authentication;

public sealed class DevelopmentCredentialsSeeder(
    SupportDbContext db,
    IAgentPasswordHasher passwordHasher,
    IConfiguration configuration,
    IHostEnvironment environment)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (!environment.IsDevelopment())
        {
            return;
        }

        string[] demoEmails =
        [
            "alex.turner@northwind-support.example",
            "ben.osei@northwind-support.example",
            "priya.nair@northwind-support.example",
            "sara.lindqvist@northwind-support.example",
            "marcus.doyle@northwind-support.example",
            "yuki.tanaka@northwind-support.example"
        ];

        var agents = await db.Set<Agent>()
            .Where(agent =>
                demoEmails.Contains(agent.Email) &&
                agent.PasswordHash == null)
            .ToListAsync(ct);

        if (agents.Count == 0)
        {
            return;
        }

        var password = configuration["DevelopmentAuth:Password"];

        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
        {
            throw new InvalidOperationException(
                "Set DevelopmentAuth:Password to a development password " +
                "containing at least 12 characters.");
        }

        foreach (var agent in agents)
        {
            agent.SetPasswordHash(passwordHasher.Hash(agent, password));
        }

        await db.SaveChangesAsync(ct);
    }
}