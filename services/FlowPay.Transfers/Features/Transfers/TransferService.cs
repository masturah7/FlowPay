using FlowPay.Transfers.Clients;
using FlowPay.Transfers.Data;
using FlowPay.Transfers.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowPay.Transfers.Features.Transfers;

public enum CreateTransferOutcome
{
    Completed,
    PendingReconciliation,
    SourceWalletNotFound,
    RecipientWalletNotFound,
    CurrencyMismatch,
    SameWallet,
    InsufficientFunds,
    IdempotencyKeyConflict,

    /// <summary>
    /// The debit step itself didn't complete cleanly (concurrency conflict,
    /// transient error, etc.) — nothing was committed, safe to retry with
    /// the same Idempotency-Key.
    /// </summary>
    DebitFailed,
}

public record CreateTransferResult(CreateTransferOutcome Outcome, Transfer? Transfer);

public interface ITransferService
{
    Task<CreateTransferResult> CreateAsync(
        Guid accountId,
        Guid fromWalletId,
        Guid toWalletId,
        long amountMinorUnits,
        string currency,
        string authorizationHeaderValue,
        string idempotencyKey,
        CancellationToken cancellationToken);
}

/// <summary>
/// Orchestrates one transfer across FlowPay.Wallet and FlowPay.Ledger. See
/// docs/epics/05-transfers.md for the step order and what each failure mode
/// means. This is the only place in the codebase that calls another
/// service's API as part of a single business operation — keep the
/// controller a thin wrapper around this.
/// </summary>
public class TransferService(
    ITransferRepository transferRepository,
    IWalletApiClient walletApiClient,
    ILedgerApiClient ledgerApiClient) : ITransferService
{
    public async Task<CreateTransferResult> CreateAsync(
        Guid accountId,
        Guid fromWalletId,
        Guid toWalletId,
        long amountMinorUnits,
        string currency,
        string authorizationHeaderValue,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var normalizedCurrency = currency.Trim().ToUpperInvariant();

        var existing = await transferRepository.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);

        if (existing is not null)
        {
            var isSameRequest =
                existing.AccountId == accountId &&
                existing.FromWalletId == fromWalletId &&
                existing.ToWalletId == toWalletId &&
                existing.AmountMinorUnits == amountMinorUnits &&
                existing.Currency == normalizedCurrency;

            if (!isSameRequest)
            {
                return new CreateTransferResult(CreateTransferOutcome.IdempotencyKeyConflict, null);
            }

            // Only Completed is truly terminal — replay it without
            // touching anything else. Pending, Failed, and
            // PendingReconciliation all re-run ExecuteAsync: every
            // downstream call (debit/ledger/credit) is idempotent on the
            // same key, so steps already done just no-op/replay, and a
            // Failed transfer gets an honest fresh attempt rather than a
            // cached rejection that might no longer be accurate (e.g. the
            // sender now has sufficient funds).
            return existing.Status == TransferStatus.Completed
                ? new CreateTransferResult(CreateTransferOutcome.Completed, existing)
                : await ExecuteAsync(existing, authorizationHeaderValue, cancellationToken);
        }

        var transfer = new Transfer
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            FromWalletId = fromWalletId,
            ToWalletId = toWalletId,
            AmountMinorUnits = amountMinorUnits,
            Currency = normalizedCurrency,
            Status = TransferStatus.Pending,
            IdempotencyKey = idempotencyKey,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };

        transferRepository.Add(transfer);

        try
        {
            await transferRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost a race with a concurrent request using the same
            // idempotency key — resume from whatever that request left
            // behind rather than guessing.
            var raceWinner = await transferRepository.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken)
                ?? throw new InvalidOperationException(
                    "Expected a transfer to exist after a unique-constraint conflict on IdempotencyKey.");

            return raceWinner.Status == TransferStatus.Completed
                ? new CreateTransferResult(CreateTransferOutcome.Completed, raceWinner)
                : await ExecuteAsync(raceWinner, authorizationHeaderValue, cancellationToken);
        }

        return await ExecuteAsync(transfer, authorizationHeaderValue, cancellationToken);
    }

    private async Task<CreateTransferResult> ExecuteAsync(
        Transfer transfer, string authorizationHeaderValue, CancellationToken cancellationToken)
    {
        if (transfer.FromWalletId == transfer.ToWalletId)
        {
            return await FailAsync(
                transfer, CreateTransferOutcome.SameWallet, "Cannot transfer a wallet to itself.", cancellationToken);
        }

        WalletDto? fromWallet;
        WalletDto? toWallet;

        try
        {
            fromWallet = await walletApiClient.GetOwnedWalletAsync(
                transfer.FromWalletId, authorizationHeaderValue, cancellationToken);
            toWallet = await walletApiClient.GetWalletAsync(transfer.ToWalletId, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Nothing has happened yet — safe, clean failure, retryable
            // with the same key.
            return await FailAsync(
                transfer,
                CreateTransferOutcome.DebitFailed,
                $"Could not validate wallets before debit: {ex.Message}",
                cancellationToken);
        }

        if (fromWallet is null)
        {
            return await FailAsync(
                transfer,
                CreateTransferOutcome.SourceWalletNotFound,
                "Source wallet not found or not owned by the caller.",
                cancellationToken);
        }

        if (toWallet is null)
        {
            return await FailAsync(
                transfer, CreateTransferOutcome.RecipientWalletNotFound, "Recipient wallet not found.", cancellationToken);
        }

        if (!string.Equals(fromWallet.Currency, transfer.Currency, StringComparison.Ordinal) ||
            !string.Equals(toWallet.Currency, transfer.Currency, StringComparison.Ordinal))
        {
            return await FailAsync(
                transfer,
                CreateTransferOutcome.CurrencyMismatch,
                "Both wallets must match the transfer currency.",
                cancellationToken);
        }

        WalletMutationResult debitResult;

        try
        {
            debitResult = await walletApiClient.DebitAsync(
                transfer.FromWalletId, transfer.AmountMinorUnits, transfer.Currency, transfer.IdempotencyKey, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // The debit call itself never got a response — we can't be
            // sure it didn't apply server-side, but it's idempotent on the
            // same key either way, so retrying is always safe.
            return await FailAsync(
                transfer, CreateTransferOutcome.DebitFailed, $"Debit call did not complete: {ex.Message}", cancellationToken);
        }

        switch (debitResult.Outcome)
        {
            case WalletMutationOutcome.InsufficientFunds:
                return await FailAsync(
                    transfer, CreateTransferOutcome.InsufficientFunds, "Insufficient funds in source wallet.", cancellationToken);
            case WalletMutationOutcome.CurrencyMismatch:
            case WalletMutationOutcome.NotFound:
                return await FailAsync(
                    transfer,
                    CreateTransferOutcome.SourceWalletNotFound,
                    "Source wallet changed state unexpectedly during debit.",
                    cancellationToken);
            case WalletMutationOutcome.IdempotencyKeyConflict:
                return await FailAsync(
                    transfer,
                    CreateTransferOutcome.IdempotencyKeyConflict,
                    "Idempotency key conflict on the debit step.",
                    cancellationToken);
            case WalletMutationOutcome.ConcurrentUpdateDetected:
            case WalletMutationOutcome.AmountTooLarge:
            case WalletMutationOutcome.UnexpectedError:
                // Debit never committed — nothing has moved. Safe, clean
                // failure; the client can retry with the same key.
                return await FailAsync(
                    transfer,
                    CreateTransferOutcome.DebitFailed,
                    $"Debit step did not complete ({debitResult.Outcome}).",
                    cancellationToken);
            case WalletMutationOutcome.Success:
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(debitResult.Outcome), debitResult.Outcome, "Unhandled WalletMutationOutcome.");
        }

        // Debit succeeded — record the authoritative double-entry. From
        // here on, money has left the sender; any failure (including the
        // HTTP calls themselves throwing) is a reconciliation concern, not
        // a clean rejection — we can no longer say nothing happened.
        RecordTransferResult ledgerResult;
        WalletMutationResult creditResult;

        try
        {
            ledgerResult = await ledgerApiClient.RecordTransferAsync(
                transfer.Id,
                transfer.FromWalletId,
                transfer.ToWalletId,
                transfer.AmountMinorUnits,
                transfer.Currency,
                transfer.IdempotencyKey,
                cancellationToken);

            if (ledgerResult.Outcome != RecordTransferOutcome.Recorded)
            {
                return await ReconciliationNeededAsync(
                    transfer, $"Ledger recording failed after debit ({ledgerResult.Outcome}).", cancellationToken);
            }

            creditResult = await walletApiClient.CreditAsync(
                transfer.ToWalletId, transfer.AmountMinorUnits, transfer.Currency, transfer.IdempotencyKey, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return await ReconciliationNeededAsync(
                transfer, $"Post-debit step did not complete: {ex.Message}", cancellationToken);
        }

        if (creditResult.Outcome != WalletMutationOutcome.Success)
        {
            return await ReconciliationNeededAsync(
                transfer,
                $"Recipient credit failed after debit+ledger ({creditResult.Outcome}).",
                cancellationToken);
        }

        transfer.Status = TransferStatus.Completed;
        transfer.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await transferRepository.SaveChangesAsync(cancellationToken);

        return new CreateTransferResult(CreateTransferOutcome.Completed, transfer);
    }

    private async Task<CreateTransferResult> FailAsync(
        Transfer transfer, CreateTransferOutcome outcome, string reason, CancellationToken cancellationToken)
    {
        transfer.Status = TransferStatus.Failed;
        transfer.FailureReason = reason;
        transfer.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await transferRepository.SaveChangesAsync(cancellationToken);

        return new CreateTransferResult(outcome, transfer);
    }

    private async Task<CreateTransferResult> ReconciliationNeededAsync(
        Transfer transfer, string reason, CancellationToken cancellationToken)
    {
        transfer.Status = TransferStatus.PendingReconciliation;
        transfer.FailureReason = reason;
        transfer.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await transferRepository.SaveChangesAsync(cancellationToken);

        return new CreateTransferResult(CreateTransferOutcome.PendingReconciliation, transfer);
    }
}
