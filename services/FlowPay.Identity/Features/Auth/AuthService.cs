using FlowPay.Identity.Domain;
using FlowPay.Identity.Features.Accounts;
using Microsoft.AspNetCore.Identity;

namespace FlowPay.Identity.Features.Auth;

public enum LoginOutcome
{
    Success,
    InvalidCredentials,
    AccountSuspended,
}

public record LoginResult(LoginOutcome Outcome, LoginResponse? Response);

public interface IAuthService
{
    Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken);
}

public class AuthService(
    IAccountService accountService,
    IPasswordHasher<Account> passwordHasher,
    IJwtTokenGenerator tokenGenerator) : IAuthService
{
    // Used only to burn roughly the same CPU time as a real password
    // verification when the email doesn't exist, so response timing doesn't
    // reveal account existence. Never persisted, never a real account.
    private static readonly Account TimingPaddingAccount = new()
    {
        Id = Guid.Empty,
        Email = "no-such-account@flowpay.invalid",
        PasswordHash = string.Empty,
        Status = AccountStatus.PendingVerification,
        CreatedAtUtc = DateTimeOffset.UnixEpoch,
    };

    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var account = await accountService.GetByEmailAsync(email, cancellationToken);

        if (account is null)
        {
            passwordHasher.HashPassword(TimingPaddingAccount, password);
            return new LoginResult(LoginOutcome.InvalidCredentials, null);
        }

        var verification = passwordHasher.VerifyHashedPassword(account, account.PasswordHash, password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return new LoginResult(LoginOutcome.InvalidCredentials, null);
        }

        if (account.Status == AccountStatus.Suspended)
        {
            return new LoginResult(LoginOutcome.AccountSuspended, null);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            account.PasswordHash = passwordHasher.HashPassword(account, password);
            await accountService.PersistPasswordRehashAsync(cancellationToken);
        }

        return new LoginResult(LoginOutcome.Success, tokenGenerator.GenerateFor(account));
    }
}
