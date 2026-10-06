using ExpenseTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.Property(c => c.Name).HasMaxLength(100).IsRequired();
        });
        modelBuilder.Entity<Expense>(entity =>
        {
            entity.Property(e => e.Description).HasMaxLength(200).IsRequired();
            entity.Property(e => e.PaymentMethod).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.HasOne(e => e.Category).WithMany().HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.Date);
            entity.ToTable(t => t.HasCheckConstraint("CK_Expenses_Amount", "\"Amount\" > 0"));
        });
    }
}
