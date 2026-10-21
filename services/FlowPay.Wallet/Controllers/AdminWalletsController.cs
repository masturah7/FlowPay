using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using FlowPay.BuildingBlocks;
using FlowPay.Wallet.Features.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowPay.Wallet.Controllers;

/// <summary>
/// Human-operator surface for the platform-owned system wallets
/// (FeeRevenue, ExternalSettlement — see
/// docs/epics/07-external-transfers-beneficiaries-fees-limits.md). JWT +
/// Admin policy, reachable through the gateway.
/// </summary>
[Authorize(Policy = "Admin")]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/wallets")]
public class AdminWalletsController(IWalletService walletService) : ControllerBase
{
    [HttpGet("system")]
    [ProducesResponseType(typeof(AdminSystemWalletsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSystemWallets(
        [FromQuery] [Required] [StringLength(3, MinimumLength = 3)] string currency,
        CancellationToken cancellationToken)
    {
        var feeRevenueWallet = await walletService.GetOrCreateSystemWalletAsync(
            SystemAccountIds.FeeRevenue, currency, cancellationToken);
        var externalSettlementWallet = await walletService.GetOrCreateSystemWalletAsync(
            SystemAccountIds.ExternalSettlement, currency, cancellationToken);

        return Ok(new AdminSystemWalletsResponse(
            currency.Trim().ToUpperInvariant(),
            WalletResponse.From(feeRevenueWallet),
            WalletResponse.From(externalSettlementWallet)));
    }
}
