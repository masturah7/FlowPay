using FlowPay.Transfers.Data;
using FlowPay.Transfers.Features.Transfers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FlowPay.Transfers;

/// <summary>
/// Periodically resumes PendingReconciliation transfers. See
/// docs/epics/08-reconciliation.md for why this re-enters
/// TransferService.ResumeReconciliationAsync rather than the public
/// CreateAsync/ExecuteAsync path (no end-user bearer token is available
/// here). Single-instance assumption: see the epic doc's "Decisions"
/// section for what breaks if FlowPay.Transfers ever runs more than one
/// replica.
/// </summary>
public class ReconciliationBackgroundService(
    IServiceScopeFactory scopeFactory,
    ReconciliationOptions options,
    ILogger<ReconciliationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(options.PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingReconciliationAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A sweep-level failure (e.g. the database being briefly
                // unreachable) must never kill this background service —
                // the next tick tries again. Per-transfer failures are
                // already caught individually below; this is the outer net.
                logger.LogError(ex, "Reconciliation sweep failed unexpectedly.");
            }

            try
            {
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }

    private async Task ProcessPendingReconciliationAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var transferRepository = scope.ServiceProvider.GetRequiredService<ITransferRepository>();
        var transferService = scope.ServiceProvider.GetRequiredService<ITransferService>();

        var pending = await transferRepository.GetPendingReconciliationAsync(options.MaxAttempts, cancellationToken);

        foreach (var transfer in pending)
        {
            try
            {
                await transferService.ResumeReconciliationAsync(transfer, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One stuck/broken transfer must not stop the rest of this
                // sweep from being processed — it simply stays
                // PendingReconciliation and gets picked up again next tick.
                logger.LogWarning(ex, "Reconciliation attempt threw for transfer {TransferId}.", transfer.Id);
            }
        }
    }
}
