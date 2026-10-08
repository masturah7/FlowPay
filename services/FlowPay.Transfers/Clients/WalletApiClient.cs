using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowPay.BuildingBlocks;

namespace FlowPay.Transfers.Clients;

public record WalletDto(Guid Id, Guid AccountId, string Currency, long BalanceMinorUnits);

public enum WalletMutationOutcome
{
    Success,
    NotFound,
    CurrencyMismatch,
    InsufficientFunds,
    AmountTooLarge,
    IdempotencyKeyConflict,
    ConcurrentUpdateDetected,
    UnexpectedError,
}

public record WalletMutationResult(WalletMutationOutcome Outcome, WalletDto? Wallet);

public interface IWalletApiClient
{
    /// <summary>Ownership-checked lookup — forwards the caller's own bearer token.</summary>
    Task<WalletDto?> GetOwnedWalletAsync(
        Guid walletId, string authorizationHeaderValue, CancellationToken cancellationToken);

    /// <summary>Internal, no ownership check — used to validate a transfer recipient's wallet.</summary>
    Task<WalletDto?> GetWalletAsync(Guid walletId, CancellationToken cancellationToken);

    Task<WalletMutationResult> DebitAsync(
        Guid walletId, long amountMinorUnits, string currency, string idempotencyKey, CancellationToken cancellationToken);

    Task<WalletMutationResult> CreditAsync(
        Guid walletId, long amountMinorUnits, string currency, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Internal-only: resolves (and lazily creates) a platform-owned system wallet's id.</summary>
    Task<Guid> GetOrCreateSystemWalletIdAsync(Guid systemAccountId, string currency, CancellationToken cancellationToken);
}

/// <summary>
/// Typed HttpClient for calling FlowPay.Wallet. BaseAddress is configured
/// where this is registered (Program.cs). Internal-endpoint calls attach
/// the shared internal API key; the one caller-ownership-checked call
/// forwards the end user's own token instead.
/// </summary>
public class WalletApiClient(HttpClient httpClient, InternalApiKeyOptions internalApiKeyOptions) : IWalletApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<WalletDto?> GetOwnedWalletAsync(
        Guid walletId, string authorizationHeaderValue, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/wallets/{walletId}");
        request.Headers.TryAddWithoutValidation("Authorization", authorizationHeaderValue);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<WalletDto>(JsonOptions, cancellationToken);
    }

    public async Task<WalletDto?> GetWalletAsync(Guid walletId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/internal/wallets/{walletId}");
        AddInternalApiKey(request);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<WalletDto>(JsonOptions, cancellationToken);
    }

    public Task<WalletMutationResult> DebitAsync(
        Guid walletId, long amountMinorUnits, string currency, string idempotencyKey, CancellationToken cancellationToken) =>
        PostMutationAsync($"api/v1/internal/wallets/{walletId}/debit", amountMinorUnits, currency, idempotencyKey, cancellationToken);

    public Task<WalletMutationResult> CreditAsync(
        Guid walletId, long amountMinorUnits, string currency, string idempotencyKey, CancellationToken cancellationToken) =>
        PostMutationAsync($"api/v1/internal/wallets/{walletId}/credit", amountMinorUnits, currency, idempotencyKey, cancellationToken);

    public async Task<Guid> GetOrCreateSystemWalletIdAsync(
        Guid systemAccountId, string currency, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/internal/wallets/system")
        {
            Content = JsonContent.Create(new { SystemAccountId = systemAccountId, Currency = currency }),
        };
        AddInternalApiKey(request);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var wallet = await response.Content.ReadFromJsonAsync<WalletDto>(JsonOptions, cancellationToken);

        return wallet!.Id;
    }

    private async Task<WalletMutationResult> PostMutationAsync(
        string path, long amountMinorUnits, string currency, string idempotencyKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new { AmountMinorUnits = amountMinorUnits, Currency = currency }),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        AddInternalApiKey(request);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new WalletMutationResult(WalletMutationOutcome.NotFound, null);
        }

        if (response.IsSuccessStatusCode)
        {
            var wallet = await response.Content.ReadFromJsonAsync<WalletDto>(JsonOptions, cancellationToken);
            return new WalletMutationResult(WalletMutationOutcome.Success, wallet);
        }

        var errorCode = await TryReadErrorCodeAsync(response, cancellationToken);

        return errorCode switch
        {
            nameof(WalletMutationOutcome.CurrencyMismatch) =>
                new WalletMutationResult(WalletMutationOutcome.CurrencyMismatch, null),
            nameof(WalletMutationOutcome.InsufficientFunds) =>
                new WalletMutationResult(WalletMutationOutcome.InsufficientFunds, null),
            nameof(WalletMutationOutcome.AmountTooLarge) =>
                new WalletMutationResult(WalletMutationOutcome.AmountTooLarge, null),
            nameof(WalletMutationOutcome.IdempotencyKeyConflict) =>
                new WalletMutationResult(WalletMutationOutcome.IdempotencyKeyConflict, null),
            nameof(WalletMutationOutcome.ConcurrentUpdateDetected) =>
                new WalletMutationResult(WalletMutationOutcome.ConcurrentUpdateDetected, null),
            _ => new WalletMutationResult(WalletMutationOutcome.UnexpectedError, null),
        };
    }

    private static async Task<string?> TryReadErrorCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsWithErrorCode>(
                JsonOptions, cancellationToken);

            return problem?.ErrorCode;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void AddInternalApiKey(HttpRequestMessage request) =>
        request.Headers.Add(RequireInternalApiKeyAttribute.HeaderName, internalApiKeyOptions.ApiKey);

    private record ProblemDetailsWithErrorCode(string? ErrorCode);
}
