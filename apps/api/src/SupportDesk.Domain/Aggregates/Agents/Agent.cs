using SupportDesk.Domain.Common;

namespace SupportDesk.Domain.Aggregates.Agents;

/// <summary>
/// A support agent and the categories they are qualified to handle.
/// </summary>
public sealed class Agent : AggregateRoot
{
    private readonly List<AgentSpecialization> _specializations = [];

    private Agent()
    {
        // For EF Core.
    }

    public Agent(
        string fullName,
        string email,
        int maxOpenTickets,
        DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxOpenTickets);

        FullName = fullName;
        Email = email;
        IsActive = true;
        MaxOpenTickets = maxOpenTickets;
        CreatedAtUtc = createdAtUtc;
    }

    public string FullName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string? PasswordHash { get; private set; }

    public bool IsActive { get; private set; }

    public int MaxOpenTickets { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<AgentSpecialization> Specializations =>
        _specializations;

    public void SetPasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }

    public void AddSpecialization(int categoryId)
    {
        if (_specializations.Any(s => s.CategoryId == categoryId))
        {
            return;
        }

        _specializations.Add(new AgentSpecialization(categoryId));
    }

    public void Deactivate() => IsActive = false;
}