using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Infrastructure.Data.Configurations;

public sealed class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable("Agents", table =>
            table.HasCheckConstraint(
                "CK_Agents_MaxOpenTickets",
                "[MaxOpenTickets] > 0"));

        builder.HasKey(agent => agent.Id);

        builder.Property(agent => agent.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(agent => agent.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(agent => agent.PasswordHash)
            .HasMaxLength(512);

        builder.Property(agent => agent.CreatedAtUtc)
            .HasColumnType("datetime2(3)");

        builder.HasIndex(agent => agent.Email)
            .IsUnique()
            .HasDatabaseName("UQ_Agents_Email");

        builder.HasMany(agent => agent.Specializations)
            .WithOne()
            .HasForeignKey(specialization => specialization.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(agent => agent.Specializations)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}