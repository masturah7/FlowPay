using FlowPay.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowPay.Identity.Data;

public class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(account =>
        {
            account.ToTable("accounts");
            account.HasKey(a => a.Id);
            account.Property(a => a.Email).IsRequired().HasMaxLength(320);
            account.HasIndex(a => a.Email).IsUnique();
            account.Property(a => a.PasswordHash).IsRequired();
            account.Property(a => a.Status).HasConversion<string>().HasMaxLength(32);
        });
    }
}
