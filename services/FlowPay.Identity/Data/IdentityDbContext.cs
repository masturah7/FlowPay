using FlowPay.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowPay.Identity.Data;

public class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<KycSubmission> KycSubmissions => Set<KycSubmission>();

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

        modelBuilder.Entity<KycSubmission>(submission =>
        {
            submission.ToTable("kyc_submissions");
            submission.HasKey(s => s.Id);
            submission.Property(s => s.FullName).IsRequired().HasMaxLength(200);
            submission.Property(s => s.DocumentType).HasConversion<string>().HasMaxLength(32);
            submission.Property(s => s.DocumentNumber).IsRequired().HasMaxLength(100);
            submission.Property(s => s.Status).HasConversion<string>().HasMaxLength(32);
            // Two distinct indexes on the same column: EF Core treats
            // repeated HasIndex(sameProperty) calls as configuring ONE
            // index (the second silently overwrites the first) unless each
            // is given an explicit name up front via this overload.
            submission.HasIndex([nameof(KycSubmission.AccountId)], "IX_kyc_submissions_AccountId");

            // At most one Pending submission per account at the database
            // level — without this, two concurrent submissions both pass
            // the service-layer eligibility check and both insert, leaving
            // an orphaned Pending row no API call can ever resolve.
            submission.HasIndex([nameof(KycSubmission.AccountId)], "IX_kyc_submissions_AccountId_PendingOnly")
                .IsUnique()
                .HasFilter("\"Status\" = 'Pending'");
        });
    }
}
