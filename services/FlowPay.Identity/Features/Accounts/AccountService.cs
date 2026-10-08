using FlowPay.Identity.Data;
using FlowPay.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FlowPay.Identity.Features.Accounts;

public enum RegisterAccountOutcome
{
    Created,
    EmailAlreadyRegistered,
}

public record RegisterAccountResult(RegisterAccountOutcome Outcome, Account? Account);

public interface IAccountService
{
    Task<RegisterAccountResult> RegisterAsync(string email, string password, CancellationToken cancellationToken);

    Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Account?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Persists an in-place password hash upgrade on an already-tracked
    /// Account (e.g. after IPasswordHasher reports SuccessRehashNeeded).
    /// Only valid to call with an entity loaded via this same service in the
    /// same request/scope — it relies on EF change tracking, not an explicit
    /// update.
    /// </summary>
    Task PersistPasswordRehashAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Business rules for accounts. Talks to storage only through
/// IAccountRepository — this is where "what does a duplicate email mean"
/// and "what does a successful registration return" get decided; the
/// repository below it just knows how to fetch/add/save Account rows.
/// </summary>
public class AccountService(IAccountRepository accountRepository, IPasswordHasher<Account> passwordHasher)
    : IAccountService
{
    public async Task<RegisterAccountResult> RegisterAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = string.Empty,
            Status = AccountStatus.PendingVerification,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        account.PasswordHash = passwordHasher.HashPassword(account, password);

        accountRepository.Add(account);

        try
        {
            await accountRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The email unique index is the only constraint on this table
            // beyond NOT NULL, so any save failure here is a duplicate email —
            // either a pre-existing one or a concurrent registration that won
            // the race. Deliberately not re-querying to "confirm" this: once
            // this insert is ever part of a multi-statement SaveChanges (e.g.
            // writing an audit row alongside it), the constraint violation
            // aborts the whole transaction and a follow-up query on the same
            // connection would throw instead of answering. Revisit this
            // assumption if a second unique constraint is ever added.
            return new RegisterAccountResult(RegisterAccountOutcome.EmailAlreadyRegistered, null);
        }

        return new RegisterAccountResult(RegisterAccountOutcome.Created, account);
    }

    public Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        accountRepository.GetByIdAsync(id, cancellationToken);

    public Task<Account?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        accountRepository.GetByEmailAsync(NormalizeEmail(email), cancellationToken);

    public Task PersistPasswordRehashAsync(CancellationToken cancellationToken) =>
        accountRepository.SaveChangesAsync(cancellationToken);

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
