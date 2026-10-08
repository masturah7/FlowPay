using FlowPay.Wallet.Data;
using FlowPay.Wallet.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowPay.Wallet.Features.Wallets;

public enum CreateWalletOutcome
{
    Created,
    CurrencyAlreadyExists,
}

public record CreateWalletResult(CreateWalletOutcome Outcome, Domain.Wallet? Wallet);

public enum FundWalletOutcome
{
    Success,
    WalletNotFound,
    CurrencyMismatch,
    AmountTooLarge,
    IdempotencyKeyConflict,
    ConcurrentUpdateDetected,
}

public record FundWalletResult(FundWalletOutcome Outcome, Domain.Wallet? Wallet);

public enum DebitWalletOutcome
{
    Success,
    WalletNotFound,
    CurrencyMismatch,
    InsufficientFunds,
    IdempotencyKeyConflict,
    ConcurrentUpdateDetected,
}

public record DebitWalletResult(DebitWalletOutcome Outcome, Domain.Wallet? Wallet);

public interface IWalletService
{
    Task<CreateWalletResult> CreateAsync(Guid accountId, string currency, CancellationToken cancellationToken);

    Task<List<Domain.Wallet>> GetMineAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>
    /// Internal-only: returns the wallet for (systemAccountId, currency),
    /// creating it if it doesn't exist yet. For platform-owned wallets only
    /// (see FlowPay.BuildingBlocks.SystemAccountIds) — not a general
    /// get-or-create for customer wallets, which always go through the
    /// ownership-checked CreateAsync.
    /// </summary>
    Task<Domain.Wallet> GetOrCreateSystemWalletAsync(
        Guid systemAccountId, string currency, CancellationToken cancellationToken);

    Task<Domain.Wallet?> GetOwnedByIdAsync(Guid accountId, Guid walletId, CancellationToken cancellationToken);

    /// <summary>
    /// Null if the wallet doesn't exist or isn't owned by accountId (same
    /// "don't confirm existence of someone else's wallet" rule as
    /// GetOwnedByIdAsync); otherwise the wallet's ledger entries, newest
    /// first — possibly empty.
    /// </summary>
    Task<List<WalletLedgerEntry>?> GetTransactionHistoryAsync(
        Guid accountId, Guid walletId, CancellationToken cancellationToken);

    /// <summary>
    /// Internal-only: looks up any wallet by id, no ownership check. Used
    /// by FlowPay.Transfers to validate a transfer recipient's wallet
    /// (which the sender doesn't own) exists and get its currency.
    /// </summary>
    Task<Domain.Wallet?> GetByIdAsync(Guid walletId, CancellationToken cancellationToken);

    /// <summary>End-user-facing: only the wallet's owner may fund it.</summary>
    Task<FundWalletResult> FundAsync(
        Guid accountId,
        Guid walletId,
        long amountMinorUnits,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Internal-only: credits any wallet by id, no ownership check. The
    /// caller (e.g. FlowPay.Transfers crediting a transfer recipient) is
    /// trusted to have already made the authorization decision — see
    /// docs/epics/05-transfers.md.
    /// </summary>
    Task<FundWalletResult> CreditAsync(
        Guid walletId,
        long amountMinorUnits,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Internal-only: debits any wallet by id, no ownership check. Same
    /// trust model as CreditAsync — the caller already decided this is
    /// authorized (e.g. Transfers already verified the sender owns the
    /// wallet before calling this).
    /// </summary>
    Task<DebitWalletResult> DebitAsync(
        Guid walletId,
        long amountMinorUnits,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken);
}

/// <summary>
/// Business rules for wallets. Talks to storage only through
/// IWalletRepository. See docs/epics/04-wallets.md for why funding keeps its
/// own append-only ledger table instead of calling FlowPay.Ledger, and
/// docs/epics/05-transfers.md for why Credit/Debit have ownership-free
/// internal variants.
/// </summary>
public class WalletService(IWalletRepository walletRepository) : IWalletService
{
    public async Task<CreateWalletResult> CreateAsync(
        Guid accountId, string currency, CancellationToken cancellationToken)
    {
        var normalizedCurrency = NormalizeCurrency(currency);

        var wallet = new Domain.Wallet
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Currency = normalizedCurrency,
            BalanceMinorUnits = 0,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        walletRepository.Add(wallet);

        try
        {
            await walletRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The (AccountId, Currency) unique index is the only constraint
            // beyond NOT NULL, so any save failure here means this account
            // already has a wallet in this currency.
            return new CreateWalletResult(CreateWalletOutcome.CurrencyAlreadyExists, null);
        }

        return new CreateWalletResult(CreateWalletOutcome.Created, wallet);
    }

    public Task<List<Domain.Wallet>> GetMineAsync(Guid accountId, CancellationToken cancellationToken) =>
        walletRepository.GetByAccountIdAsync(accountId, cancellationToken);

    public async Task<Domain.Wallet> GetOrCreateSystemWalletAsync(
        Guid systemAccountId, string currency, CancellationToken cancellationToken)
    {
        var normalizedCurrency = NormalizeCurrency(currency);

        var existing = await walletRepository.GetByAccountIdAsync(systemAccountId, cancellationToken);
        var existingWallet = existing.Find(w => w.Currency == normalizedCurrency);

        if (existingWallet is not null)
        {
            return existingWallet;
        }

        var createResult = await CreateAsync(systemAccountId, normalizedCurrency, cancellationToken);

        if (createResult.Outcome == CreateWalletOutcome.Created)
        {
            return createResult.Wallet!;
        }

        // Lost a race with a concurrent get-or-create call for this same
        // (systemAccountId, currency) — the wallet now exists, just not the
        // one we tried to create.
        var raceWinner = (await walletRepository.GetByAccountIdAsync(systemAccountId, cancellationToken))
            .Find(w => w.Currency == normalizedCurrency);

        return raceWinner ?? throw new InvalidOperationException(
            "Expected a system wallet to exist after a unique-constraint conflict on (AccountId, Currency).");
    }

    public async Task<Domain.Wallet?> GetOwnedByIdAsync(
        Guid accountId, Guid walletId, CancellationToken cancellationToken)
    {
        var wallet = await walletRepository.GetByIdAsync(walletId, cancellationToken);
        return wallet is not null && wallet.AccountId == accountId ? wallet : null;
    }

    public Task<Domain.Wallet?> GetByIdAsync(Guid walletId, CancellationToken cancellationToken) =>
        walletRepository.GetByIdAsync(walletId, cancellationToken);

    public async Task<List<WalletLedgerEntry>?> GetTransactionHistoryAsync(
        Guid accountId, Guid walletId, CancellationToken cancellationToken)
    {
        var wallet = await walletRepository.GetByIdAsync(walletId, cancellationToken);

        if (wallet is null || wallet.AccountId != accountId)
        {
            return null;
        }

        return await walletRepository.GetLedgerEntriesByWalletIdAsync(walletId, cancellationToken);
    }

    public async Task<FundWalletResult> FundAsync(
        Guid accountId,
        Guid walletId,
        long amountMinorUnits,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var wallet = await walletRepository.GetByIdAsync(walletId, cancellationToken);

        if (wallet is null || wallet.AccountId != accountId)
        {
            return new FundWalletResult(FundWalletOutcome.WalletNotFound, null);
        }

        return await CreditAsync(walletId, amountMinorUnits, currency, idempotencyKey, cancellationToken);
    }

    public async Task<FundWalletResult> CreditAsync(
        Guid walletId,
        long amountMinorUnits,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var wallet = await walletRepository.GetByIdAsync(walletId, cancellationToken);

        if (wallet is null)
        {
            return new FundWalletResult(FundWalletOutcome.WalletNotFound, null);
        }

        var normalizedCurrency = NormalizeCurrency(currency);

        // Check for a prior attempt with this exact idempotency key BEFORE
        // evaluating any balance-dependent rule below. Otherwise a
        // legitimate replay (e.g. FlowPay.Transfers retrying a transfer
        // whose credit step already succeeded once) gets judged against a
        // balance that's since changed — including because of the original
        // credit itself — and a safe replay can be wrongly rejected instead
        // of returning the same success it returned the first time.
        var existingEntry = await walletRepository.GetLedgerEntryByIdempotencyKeyAsync(
            walletId, idempotencyKey, cancellationToken);

        if (existingEntry is not null)
        {
            if (existingEntry.AmountMinorUnits != amountMinorUnits ||
                !string.Equals(existingEntry.Currency, normalizedCurrency, StringComparison.Ordinal))
            {
                return new FundWalletResult(FundWalletOutcome.IdempotencyKeyConflict, null);
            }

            return new FundWalletResult(FundWalletOutcome.Success, wallet);
        }

        if (!string.Equals(wallet.Currency, normalizedCurrency, StringComparison.Ordinal))
        {
            return new FundWalletResult(FundWalletOutcome.CurrencyMismatch, null);
        }

        // long + long overflows silently (wraps, doesn't throw) in C# by
        // default — amountMinorUnits near long.MaxValue would otherwise
        // wrap BalanceMinorUnits to garbage with no error. Reject it
        // explicitly instead of relying on `checked` to turn it into a
        // different unhandled exception.
        if (amountMinorUnits > long.MaxValue - wallet.BalanceMinorUnits)
        {
            return new FundWalletResult(FundWalletOutcome.AmountTooLarge, null);
        }

        var newBalance = wallet.BalanceMinorUnits + amountMinorUnits;

        walletRepository.AddLedgerEntry(new WalletLedgerEntry
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            Direction = LedgerEntryDirection.Credit,
            AmountMinorUnits = amountMinorUnits,
            Currency = normalizedCurrency,
            BalanceAfterMinorUnits = newBalance,
            IdempotencyKey = idempotencyKey,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        wallet.BalanceMinorUnits = newBalance;

        try
        {
            await walletRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request updated this wallet between our read and our
            // write (the xmin concurrency token caught it) — nothing was
            // persisted from this attempt, including the ledger entry. Safe
            // for the client to retry with the same Idempotency-Key, which
            // will re-read the now-current balance and apply cleanly.
            return new FundWalletResult(FundWalletOutcome.ConcurrentUpdateDetected, null);
        }
        catch (DbUpdateException)
        {
            // We already checked for an existing entry above and found
            // none, so reaching here means we lost a genuine race against a
            // concurrent *first* attempt with this same key (both checked
            // "no entry yet" before either had committed). Re-check now
            // that one of them has.
            var raceEntry = await walletRepository.GetLedgerEntryByIdempotencyKeyAsync(
                walletId, idempotencyKey, cancellationToken);

            if (raceEntry is null)
            {
                // Some other constraint failed unexpectedly — don't swallow it.
                throw;
            }

            if (raceEntry.AmountMinorUnits != amountMinorUnits ||
                !string.Equals(raceEntry.Currency, normalizedCurrency, StringComparison.Ordinal))
            {
                return new FundWalletResult(FundWalletOutcome.IdempotencyKeyConflict, null);
            }

            // Safe replay of the same request. `wallet` above still holds the
            // balance mutation from this attempt, which never committed —
            // reload it so the response reflects the wallet's real current
            // state rather than that stale edit.
            await walletRepository.ReloadAsync(wallet, cancellationToken);
        }

        return new FundWalletResult(FundWalletOutcome.Success, wallet);
    }

    public async Task<DebitWalletResult> DebitAsync(
        Guid walletId,
        long amountMinorUnits,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var wallet = await walletRepository.GetByIdAsync(walletId, cancellationToken);

        if (wallet is null)
        {
            return new DebitWalletResult(DebitWalletOutcome.WalletNotFound, null);
        }

        var normalizedCurrency = NormalizeCurrency(currency);

        // Same reasoning as CreditAsync: check for a prior attempt with
        // this exact idempotency key before evaluating InsufficientFunds
        // against the current balance, which already reflects this debit
        // if it's a replay — otherwise a safe retry of an already-applied
        // debit can be wrongly rejected.
        var existingEntry = await walletRepository.GetLedgerEntryByIdempotencyKeyAsync(
            walletId, idempotencyKey, cancellationToken);

        if (existingEntry is not null)
        {
            if (existingEntry.AmountMinorUnits != amountMinorUnits ||
                !string.Equals(existingEntry.Currency, normalizedCurrency, StringComparison.Ordinal))
            {
                return new DebitWalletResult(DebitWalletOutcome.IdempotencyKeyConflict, null);
            }

            return new DebitWalletResult(DebitWalletOutcome.Success, wallet);
        }

        if (!string.Equals(wallet.Currency, normalizedCurrency, StringComparison.Ordinal))
        {
            return new DebitWalletResult(DebitWalletOutcome.CurrencyMismatch, null);
        }

        if (amountMinorUnits > wallet.BalanceMinorUnits)
        {
            return new DebitWalletResult(DebitWalletOutcome.InsufficientFunds, null);
        }

        var newBalance = wallet.BalanceMinorUnits - amountMinorUnits;

        walletRepository.AddLedgerEntry(new WalletLedgerEntry
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            Direction = LedgerEntryDirection.Debit,
            AmountMinorUnits = amountMinorUnits,
            Currency = normalizedCurrency,
            BalanceAfterMinorUnits = newBalance,
            IdempotencyKey = idempotencyKey,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        wallet.BalanceMinorUnits = newBalance;

        try
        {
            await walletRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Same race as CreditAsync — another request changed this
            // wallet between our read and our write. Safe to retry with the
            // same Idempotency-Key: it'll re-read the current balance
            // (including the now-current InsufficientFunds check) cleanly.
            return new DebitWalletResult(DebitWalletOutcome.ConcurrentUpdateDetected, null);
        }
        catch (DbUpdateException)
        {
            // Same reasoning as CreditAsync's catch: we already checked for
            // an existing entry above, so reaching here means a genuine
            // race against a concurrent first attempt with this same key.
            var raceEntry = await walletRepository.GetLedgerEntryByIdempotencyKeyAsync(
                walletId, idempotencyKey, cancellationToken);

            if (raceEntry is null)
            {
                throw;
            }

            if (raceEntry.AmountMinorUnits != amountMinorUnits ||
                !string.Equals(raceEntry.Currency, normalizedCurrency, StringComparison.Ordinal))
            {
                return new DebitWalletResult(DebitWalletOutcome.IdempotencyKeyConflict, null);
            }

            await walletRepository.ReloadAsync(wallet, cancellationToken);
        }

        return new DebitWalletResult(DebitWalletOutcome.Success, wallet);
    }

    private static string NormalizeCurrency(string currency) => currency.Trim().ToUpperInvariant();
}
