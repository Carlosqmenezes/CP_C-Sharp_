using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();

    public DbSet<ExpenseHistory> ExpenseHistories => Set<ExpenseHistory>();

    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ExpenseCategory>(entity =>
        {
            entity.Property(c => c.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(c => c.Name).IsUnique();
            entity.HasData(
                new ExpenseCategory { Id = 1, Name = "Alimentação" },
                new ExpenseCategory { Id = 2, Name = "Transporte" },
                new ExpenseCategory { Id = 3, Name = "Hospedagem" },
                new ExpenseCategory { Id = 4, Name = "Outros" });
        });

        builder.Entity<Expense>(entity =>
        {
            entity.Property(e => e.OwnerId).HasMaxLength(450).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.RejectionReason).HasMaxLength(500);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsConcurrencyToken();
            entity.HasIndex(e => e.OwnerId);
            entity.HasIndex(e => e.Status);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.OwnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ExpenseCategory>().WithMany().HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ExpenseHistory>(entity =>
        {
            entity.Property(h => h.ActorId).HasMaxLength(450).IsRequired();
            entity.Property(h => h.Action).HasConversion<string>().HasMaxLength(20);
            entity.Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(h => h.Justification).HasMaxLength(500);
            entity.Property(h => h.Changes).HasMaxLength(2000);
            entity.HasIndex(h => h.ExpenseId);
            entity.HasOne<Expense>().WithMany().HasForeignKey(h => h.ExpenseId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PaymentRecord>(entity =>
        {
            entity.Property(p => p.PaidByUserId).HasMaxLength(450).IsRequired();
            entity.Property(p => p.Amount).HasPrecision(18, 2);
            entity.HasIndex(p => p.ExpenseId).IsUnique();
            entity.HasOne<Expense>().WithMany().HasForeignKey(p => p.ExpenseId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
