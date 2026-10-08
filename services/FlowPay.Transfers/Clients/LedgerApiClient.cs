using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowPay.BuildingBlocks;

namespace FlowPay.Transfers.Clients;

public enum RecordTransferOutcome
{
    Recorded,
    IdempotencyKeyConflict,
    UnexpectedError,
}

public record RecordTransferResult(RecordTransferOutcome Outcome);

public interface ILedgerApiClient
{
    Task<RecordTransferResult> RecordTransferAsync(
        Guid transferReference,
        Guid fromWalletId,
        Guid toWalletId,
        long amountMinorUnits,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<long> GetBalanceAsync(Guid walletId, CancellationToken cancellationToken);
}

/// <summary>Typed HttpClient for calling FlowPay.Ledger. BaseAddress configured in Program.cs.</summary>
public class LedgerApiClient(HttpClient httpClient, InternalApiKeyOptions internalApiKeyOptions) : ILedgerApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<RecordTransferResult> RecordTransferAsync(
        Guid transferReference,
        Guid fromWalletId,
        Guid toWalletId,
        long amountMinorUnits,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/ledger/transfers")
        {
            Content = JsonContent.Create(new
            {
                TransferReference = transferReference,
                FromWalletId = fromWalletId,
                ToWalletId = toWalletId,
                AmountMinorUnits = amountMinorUnits,
                Currency = currency,
            }),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Headers.Add(RequireInternalApiKeyAttribute.HeaderName, internalApiKeyOptions.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new RecordTransferResult(RecordTransferOutcome.Recorded);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return new RecordTransferResult(RecordTransferOutcome.IdempotencyKeyConflict);
        }

        return new RecordTransferResult(RecordTransferOutcome.UnexpectedError);
    }

    public async Task<long> GetBalanceAsync(Guid walletId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/ledger/wallets/{walletId}/balance");
        request.Headers.Add(RequireInternalApiKeyAttribute.HeaderName, internalApiKeyOptions.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var balance = await response.Content.ReadFromJsonAsync<WalletBalanceDto>(JsonOptions, cancellationToken);

        return balance?.BalanceMinorUnits ?? 0;
    }

    private record WalletBalanceDto(Guid WalletId, long BalanceMinorUnits);
}
