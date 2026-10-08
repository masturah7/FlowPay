using FlowPay.BuildingBlocks;
using FlowPay.Transfers.Clients;
using FlowPay.Transfers.Data;
using FlowPay.Transfers.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowPay.Transfers.Features.Transfers;

public enum CreateTransferOutcome
{
    Completed,
    SubmittedExternally,
    PendingReconciliation,
    SourceWalletNotFound,
    RecipientWalletNotFound,
    CurrencyMismatch,
    SameWallet,
    InsufficientFunds,
    IdempotencyKeyConflict,

    /// <summary>Neither or both of ToWalletId/ToBeneficiaryId were given.</summary>
    InvalidDestination,

    /// <summary>ToBeneficiaryId was given but doesn't exist or isn't owned by the caller.</summary>
    BeneficiaryNotFound,

    PerTransactionLimitExceeded,
    DailyLimitExceeded,

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
        Guid? toWalletId,
        Guid? toBeneficiaryId,
        long amountMinorUnits,
        string currency,
        string authorizationHeaderValue,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<List<Transfer>> GetMineAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>Null if the transfer doesn't exist or wasn't sent by accountId.</summary>
    Task<Transfer?> GetOwnedByIdAsync(Guid accountId, Guid transferId, CancellationToken cancellationToken);
}

/// <summary>
/// Orchestrates one transfer across FlowPay.Wallet, FlowPay.Ledger, and (for
/// external transfers) a simulated settlement/fee leg. See
/// docs/epics/05-transfers.md for the original step order and
/// docs/epics/07-external-transfers-beneficiaries-fees-limits.md for
/// destination resolution, limits, fees, and external transfers. This is the
/// only place in the codebase that calls another service's API as part of a
/// single business operation — keep the controller a thin wrapper around
/// this.
/// </summary>
public class TransferService(
    ITransferRepository transferRepository,
    IBeneficiaryRepository beneficiaryRepository,
    IWalletApiClient walletApiClient,
    ILedgerApiClient ledgerApiClient,
    INotificationApiClient notificationApiClient,
    TransferPolicyOptions transferPolicyOptions) : ITransferService
{
    public async Task<CreateTransferResult> CreateAsync(
        Guid accountId,
        Guid fromWalletId,
        Guid? toWalletId,
        Guid? toBeneficiaryId,
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
                existing.ToBeneficiaryId == toBeneficiaryId &&
                (toBeneficiaryId is not null || existing.ToWalletId == toWalletId) &&
                existing.AmountMinorUnits == amountMinorUnits &&
                existing.Currency == normalizedCurrency;

            if (!isSameRequest)
            {
                return new CreateTransferResult(CreateTransferOutcome.IdempotencyKeyConflict, null);
            }

            // Only truly terminal outcomes replay as-is — everything else
            // re-runs ExecuteAsync: every downstream call is idempotent on
            // the same key, so steps already done just no-op/replay, and a
            // Failed transfer gets an honest fresh attempt rather than a
            // cached rejection that might no longer be accurate (e.g. the
            // sender now has sufficient funds).
            return existing.Status is TransferStatus.Completed or TransferStatus.SubmittedExternally
                ? new CreateTransferResult(MapTerminalStatus(existing.Status), existing)
                : await ExecuteAsync(existing, authorizationHeaderValue, cancellationToken);
        }

        var destination = await ResolveDestinationAsync(accountId, toWalletId, toBeneficiaryId, cancellationToken);

        if (destination.Outcome != ResolveDestinationOutcome.Resolved)
        {
            return new CreateTransferResult(
                destination.Outcome == ResolveDestinationOutcome.InvalidDestination
                    ? CreateTransferOutcome.InvalidDestination
                    : CreateTransferOutcome.BeneficiaryNotFound,
                null);
        }

        var transfer = new Transfer
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            FromWalletId = fromWalletId,
            DestinationType = destination.DestinationType,
            ToBeneficiaryId = toBeneficiaryId,
            ToWalletId = destination.WalletId,
            ExternalBankName = destination.BankName,
            ExternalBankAccountNumber = destination.BankAccountNumber,
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

            return raceWinner.Status is TransferStatus.Completed or TransferStatus.SubmittedExternally
                ? new CreateTransferResult(MapTerminalStatus(raceWinner.Status), raceWinner)
                : await ExecuteAsync(raceWinner, authorizationHeaderValue, cancellationToken);
        }

        return await ExecuteAsync(transfer, authorizationHeaderValue, cancellationToken);
    }

    public Task<List<Transfer>> GetMineAsync(Guid accountId, CancellationToken cancellationToken) =>
        transferRepository.GetByAccountIdAsync(accountId, cancellationToken);

    public async Task<Transfer?> GetOwnedByIdAsync(Guid accountId, Guid transferId, CancellationToken cancellationToken)
    {
        var transfer = await transferRepository.GetByIdAsync(transferId, cancellationToken);
        return transfer is not null && transfer.AccountId == accountId ? transfer : null;
    }

    private enum ResolveDestinationOutcome
    {
        Resolved,
        InvalidDestination,
        BeneficiaryNotFound,
    }

    private record ResolvedDestination(
        ResolveDestinationOutcome Outcome,
        TransferDestinationType DestinationType = default,
        Guid? WalletId = null,
        string? BankName = null,
        string? BankAccountNumber = null);

    private async Task<ResolvedDestination> ResolveDestinationAsync(
        Guid accountId, Guid? toWalletId, Guid? toBeneficiaryId, CancellationToken cancellationToken)
    {
        if ((toWalletId is null) == (toBeneficiaryId is null))
        {
            return new ResolvedDestination(ResolveDestinationOutcome.InvalidDestination);
        }

        if (toWalletId is not null)
        {
            return new ResolvedDestination(
                ResolveDestinationOutcome.Resolved, TransferDestinationType.InternalWallet, WalletId: toWalletId);
        }

        var beneficiary = await beneficiaryRepository.GetByIdAsync(toBeneficiaryId!.Value, cancellationToken);

        if (beneficiary is null || beneficiary.AccountId != accountId)
        {
            return new ResolvedDestination(ResolveDestinationOutcome.BeneficiaryNotFound);
        }

        return beneficiary.Type == BeneficiaryType.InternalWallet
            ? new ResolvedDestination(
                ResolveDestinationOutcome.Resolved, TransferDestinationType.InternalWallet, WalletId: beneficiary.WalletId)
            : new ResolvedDestination(
                ResolveDestinationOutcome.Resolved,
                TransferDestinationType.ExternalBank,
                BankName: beneficiary.BankName,
                BankAccountNumber: beneficiary.BankAccountNumber);
    }

    private static CreateTransferOutcome MapTerminalStatus(TransferStatus status) => status switch
    {
        TransferStatus.Completed => CreateTransferOutcome.Completed,
        TransferStatus.SubmittedExternally => CreateTransferOutcome.SubmittedExternally,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Not a terminal success status."),
    };

    private async Task<CreateTransferResult> ExecuteAsync(
        Transfer transfer, string authorizationHeaderValue, CancellationToken cancellationToken)
    {
        if (transfer.ToWalletId == transfer.FromWalletId)
        {
            return await FailAsync(
                transfer, CreateTransferOutcome.SameWallet, "Cannot transfer a wallet to itself.", cancellationToken);
        }

        if (transfer.AmountMinorUnits > transferPolicyOptions.MaxPerTransactionMinorUnits)
        {
            return await FailAsync(
                transfer,
                CreateTransferOutcome.PerTransactionLimitExceeded,
                $"Amount exceeds the per-transaction limit of {transferPolicyOptions.MaxPerTransactionMinorUnits} minor units.",
                cancellationToken);
        }

        var startOfUtcDay = new DateTimeOffset(DateOnly.FromDateTime(DateTime.UtcNow).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var sentToday = await transferRepository.GetSentAmountSinceAsync(
            transfer.AccountId, startOfUtcDay, transfer.Id, cancellationToken);

        if (sentToday + transfer.AmountMinorUnits > transferPolicyOptions.MaxDailyMinorUnits)
        {
            return await FailAsync(
                transfer,
                CreateTransferOutcome.DailyLimitExceeded,
                $"This transfer would exceed the daily sending limit of {transferPolicyOptions.MaxDailyMinorUnits} minor units.",
                cancellationToken);
        }

        var fee = transfer.DestinationType == TransferDestinationType.ExternalBank
            ? transferPolicyOptions.ExternalTransferFeeMinorUnits
            : 0;

        WalletDto? fromWallet;

        try
        {
            fromWallet = await walletApiClient.GetOwnedWalletAsync(
                transfer.FromWalletId, authorizationHeaderValue, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Nothing has happened yet — safe, clean failure, retryable
            // with the same key.
            return await FailAsync(
                transfer,
                CreateTransferOutcome.DebitFailed,
                $"Could not validate the source wallet before debit: {ex.Message}",
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

        Guid creditWalletId;
        Guid? recipientAccountId = null;

        if (transfer.DestinationType == TransferDestinationType.InternalWallet)
        {
            WalletDto? toWallet;

            try
            {
                toWallet = await walletApiClient.GetWalletAsync(transfer.ToWalletId!.Value, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                return await FailAsync(
                    transfer,
                    CreateTransferOutcome.DebitFailed,
                    $"Could not validate the recipient wallet before debit: {ex.Message}",
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

            creditWalletId = toWallet.Id;
            recipientAccountId = toWallet.AccountId;
        }
        else
        {
            if (!string.Equals(fromWallet.Currency, transfer.Currency, StringComparison.Ordinal))
            {
                return await FailAsync(
                    transfer,
                    CreateTransferOutcome.CurrencyMismatch,
                    "Source wallet must match the transfer currency.",
                    cancellationToken);
            }

            try
            {
                creditWalletId = await walletApiClient.GetOrCreateSystemWalletIdAsync(
                    SystemAccountIds.ExternalSettlement, transfer.Currency, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                return await FailAsync(
                    transfer,
                    CreateTransferOutcome.DebitFailed,
                    $"Could not resolve the external-settlement wallet before debit: {ex.Message}",
                    cancellationToken);
            }
        }

        if (fromWallet.BalanceMinorUnits < transfer.AmountMinorUnits + fee)
        {
            return await FailAsync(
                transfer,
                CreateTransferOutcome.InsufficientFunds,
                "Insufficient funds in source wallet for the transfer amount plus fee.",
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
        try
        {
            var ledgerResult = await ledgerApiClient.RecordTransferAsync(
                transfer.Id,
                transfer.FromWalletId,
                creditWalletId,
                transfer.AmountMinorUnits,
                transfer.Currency,
                transfer.IdempotencyKey,
                cancellationToken);

            if (ledgerResult.Outcome != RecordTransferOutcome.Recorded)
            {
                return await ReconciliationNeededAsync(
                    transfer, $"Ledger recording failed after debit ({ledgerResult.Outcome}).", cancellationToken);
            }

            var creditResult = await walletApiClient.CreditAsync(
                creditWalletId, transfer.AmountMinorUnits, transfer.Currency, transfer.IdempotencyKey, cancellationToken);

            if (creditResult.Outcome != WalletMutationOutcome.Success)
            {
                return await ReconciliationNeededAsync(
                    transfer,
                    $"Recipient credit failed after debit+ledger ({creditResult.Outcome}).",
                    cancellationToken);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return await ReconciliationNeededAsync(
                transfer, $"Post-debit step did not complete: {ex.Message}", cancellationToken);
        }

        if (fee > 0)
        {
            var feeOutcome = await ChargeFeeAsync(transfer, fee, cancellationToken);

            if (feeOutcome is not null)
            {
                return feeOutcome;
            }
        }

        transfer.Status = transfer.DestinationType == TransferDestinationType.InternalWallet
            ? TransferStatus.Completed
            : TransferStatus.SubmittedExternally;
        transfer.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await transferRepository.SaveChangesAsync(cancellationToken);

        await notificationApiClient.NotifyAsync(
            transfer.AccountId,
            NotificationType.TransferSent,
            SenderSentMessage(transfer),
            transfer.Id,
            cancellationToken);

        if (recipientAccountId is not null)
        {
            await notificationApiClient.NotifyAsync(
                recipientAccountId.Value,
                NotificationType.TransferReceived,
                $"You received {transfer.AmountMinorUnits} {transfer.Currency} (transfer {transfer.Id}).",
                transfer.Id,
                cancellationToken);
        }

        return new CreateTransferResult(MapTerminalStatus(transfer.Status), transfer);
    }

    /// <summary>
    /// The fee leg: a second, independent debit → ledger → credit cycle
    /// (sender → FeeRevenue system wallet) with its own idempotency key and
    /// ledger grouping reference, run only after the main transfer leg has
    /// already succeeded. Returns null on success (caller continues to
    /// Completed/SubmittedExternally); returns a PendingReconciliation
    /// result if any step here fails — the main transfer already moved
    /// money correctly, only the fee collection is in an unclear state.
    /// </summary>
    private async Task<CreateTransferResult?> ChargeFeeAsync(
        Transfer transfer, long fee, CancellationToken cancellationToken)
    {
        transfer.FeeLedgerReference ??= Guid.NewGuid();
        var feeIdempotencyKey = $"{transfer.IdempotencyKey}:fee";

        try
        {
            var feeDebitResult = await walletApiClient.DebitAsync(
                transfer.FromWalletId, fee, transfer.Currency, feeIdempotencyKey, cancellationToken);

            if (feeDebitResult.Outcome != WalletMutationOutcome.Success)
            {
                return await ReconciliationNeededAsync(
                    transfer,
                    $"Main transfer succeeded but fee debit did not complete ({feeDebitResult.Outcome}).",
                    cancellationToken);
            }

            var feeRevenueWalletId = await walletApiClient.GetOrCreateSystemWalletIdAsync(
                SystemAccountIds.FeeRevenue, transfer.Currency, cancellationToken);

            var feeLedgerResult = await ledgerApiClient.RecordTransferAsync(
                transfer.FeeLedgerReference.Value,
                transfer.FromWalletId,
                feeRevenueWalletId,
                fee,
                transfer.Currency,
                feeIdempotencyKey,
                cancellationToken);

            if (feeLedgerResult.Outcome != RecordTransferOutcome.Recorded)
            {
                return await ReconciliationNeededAsync(
                    transfer, "Main transfer succeeded but fee ledger recording failed.", cancellationToken);
            }

            var feeCreditResult = await walletApiClient.CreditAsync(
                feeRevenueWalletId, fee, transfer.Currency, feeIdempotencyKey, cancellationToken);

            if (feeCreditResult.Outcome != WalletMutationOutcome.Success)
            {
                return await ReconciliationNeededAsync(
                    transfer,
                    $"Main transfer succeeded but fee revenue credit did not complete ({feeCreditResult.Outcome}).",
                    cancellationToken);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return await ReconciliationNeededAsync(
                transfer, $"Main transfer succeeded but the fee leg did not complete: {ex.Message}", cancellationToken);
        }

        transfer.FeeMinorUnits = fee;
        return null;
    }

    private static string SenderSentMessage(Transfer transfer) => transfer.DestinationType switch
    {
        TransferDestinationType.InternalWallet =>
            $"You sent {transfer.AmountMinorUnits} {transfer.Currency} (transfer {transfer.Id}).",
        TransferDestinationType.ExternalBank =>
            $"Your transfer of {transfer.AmountMinorUnits} {transfer.Currency} " +
            $"(fee: {transfer.FeeMinorUnits} {transfer.Currency}) to {transfer.ExternalBankName} has been " +
            $"submitted. This environment simulates external transfers — no real bank settlement occurs.",
        _ => throw new ArgumentOutOfRangeException(
            nameof(transfer.DestinationType), transfer.DestinationType, "Unhandled TransferDestinationType."),
    };

    private async Task<CreateTransferResult> FailAsync(
        Transfer transfer, CreateTransferOutcome outcome, string reason, CancellationToken cancellationToken)
    {
        transfer.Status = TransferStatus.Failed;
        transfer.FailureReason = reason;
        transfer.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await transferRepository.SaveChangesAsync(cancellationToken);

        await notificationApiClient.NotifyAsync(
            transfer.AccountId,
            NotificationType.TransferFailed,
            $"Your transfer of {transfer.AmountMinorUnits} {transfer.Currency} failed: {reason}",
            transfer.Id,
            cancellationToken);

        return new CreateTransferResult(outcome, transfer);
    }

    private async Task<CreateTransferResult> ReconciliationNeededAsync(
        Transfer transfer, string reason, CancellationToken cancellationToken)
    {
        transfer.Status = TransferStatus.PendingReconciliation;
        transfer.FailureReason = reason;
        transfer.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await transferRepository.SaveChangesAsync(cancellationToken);

        await notificationApiClient.NotifyAsync(
            transfer.AccountId,
            NotificationType.TransferPendingReconciliation,
            $"Your transfer of {transfer.AmountMinorUnits} {transfer.Currency} is being reconciled: {reason}",
            transfer.Id,
            cancellationToken);

        return new CreateTransferResult(CreateTransferOutcome.PendingReconciliation, transfer);
    }
}
