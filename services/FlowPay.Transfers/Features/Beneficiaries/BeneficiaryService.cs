using FlowPay.Transfers.Clients;
using FlowPay.Transfers.Data;
using FlowPay.Transfers.Domain;

namespace FlowPay.Transfers.Features.Beneficiaries;

public enum CreateBeneficiaryOutcome
{
    Created,

    /// <summary>Missing the fields required for the given Type (WalletId for
    /// InternalWallet; BankName + BankAccountNumber for ExternalBank).</summary>
    InvalidRequest,

    /// <summary>InternalWallet type, but WalletId doesn't exist.</summary>
    WalletNotFound,
}

public record CreateBeneficiaryResult(CreateBeneficiaryOutcome Outcome, Beneficiary? Beneficiary);

public enum DeleteBeneficiaryOutcome
{
    Deleted,
    NotFound,
}

public interface IBeneficiaryService
{
    Task<CreateBeneficiaryResult> CreateAsync(
        Guid accountId, CreateBeneficiaryRequest request, CancellationToken cancellationToken);

    Task<List<Beneficiary>> GetMineAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>Null if the beneficiary doesn't exist or isn't owned by accountId.</summary>
    Task<Beneficiary?> GetOwnedByIdAsync(Guid accountId, Guid beneficiaryId, CancellationToken cancellationToken);

    Task<DeleteBeneficiaryOutcome> DeleteAsync(Guid accountId, Guid beneficiaryId, CancellationToken cancellationToken);
}

public class BeneficiaryService(
    IBeneficiaryRepository beneficiaryRepository, IWalletApiClient walletApiClient) : IBeneficiaryService
{
    public async Task<CreateBeneficiaryResult> CreateAsync(
        Guid accountId, CreateBeneficiaryRequest request, CancellationToken cancellationToken)
    {
        if (request.Type == BeneficiaryType.InternalWallet)
        {
            if (request.WalletId is null)
            {
                return new CreateBeneficiaryResult(CreateBeneficiaryOutcome.InvalidRequest, null);
            }

            var wallet = await walletApiClient.GetWalletAsync(request.WalletId.Value, cancellationToken);

            if (wallet is null)
            {
                return new CreateBeneficiaryResult(CreateBeneficiaryOutcome.WalletNotFound, null);
            }
        }
        else if (string.IsNullOrWhiteSpace(request.BankName) || string.IsNullOrWhiteSpace(request.BankAccountNumber))
        {
            return new CreateBeneficiaryResult(CreateBeneficiaryOutcome.InvalidRequest, null);
        }

        var beneficiary = new Beneficiary
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Label = request.Label.Trim(),
            Type = request.Type,
            WalletId = request.Type == BeneficiaryType.InternalWallet ? request.WalletId : null,
            BankName = request.Type == BeneficiaryType.ExternalBank ? request.BankName!.Trim() : null,
            BankAccountNumber = request.Type == BeneficiaryType.ExternalBank
                ? request.BankAccountNumber!.Trim()
                : null,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        beneficiaryRepository.Add(beneficiary);
        await beneficiaryRepository.SaveChangesAsync(cancellationToken);

        return new CreateBeneficiaryResult(CreateBeneficiaryOutcome.Created, beneficiary);
    }

    public Task<List<Beneficiary>> GetMineAsync(Guid accountId, CancellationToken cancellationToken) =>
        beneficiaryRepository.GetByAccountIdAsync(accountId, cancellationToken);

    public async Task<Beneficiary?> GetOwnedByIdAsync(
        Guid accountId, Guid beneficiaryId, CancellationToken cancellationToken)
    {
        var beneficiary = await beneficiaryRepository.GetByIdAsync(beneficiaryId, cancellationToken);
        return beneficiary is not null && beneficiary.AccountId == accountId ? beneficiary : null;
    }

    public async Task<DeleteBeneficiaryOutcome> DeleteAsync(
        Guid accountId, Guid beneficiaryId, CancellationToken cancellationToken)
    {
        var beneficiary = await GetOwnedByIdAsync(accountId, beneficiaryId, cancellationToken);

        if (beneficiary is null)
        {
            return DeleteBeneficiaryOutcome.NotFound;
        }

        beneficiaryRepository.Remove(beneficiary);
        await beneficiaryRepository.SaveChangesAsync(cancellationToken);

        return DeleteBeneficiaryOutcome.Deleted;
    }
}
