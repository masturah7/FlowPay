using FlowPay.Identity.Domain;

namespace FlowPay.Identity.Features.Accounts;

public record RegisterAccountResponse(Guid Id, string Email, AccountStatus Status);
