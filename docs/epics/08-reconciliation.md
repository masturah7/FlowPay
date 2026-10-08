# Epic 8: Automated Reconciliation

Resolves the open question left in `docs/epics/05-transfers.md` and
`docs/epics/07-external-transfers-beneficiaries-fees-limits.md`: "How does a
`PendingReconciliation` transfer actually get resolved?" Not a PRD capability
by itself — this is the PRD's own financial-integrity requirement ("does
anything require reconciliation?" must be answerable, and the answer can't
forever be "yes, and nobody is doing anything about it").

## Summary

Since Epic 5, every downstream step a transfer takes (debit, ledger record,
credit, fee debit, fee ledger record, fee credit) has been idempotent on a
stable key — that was always meant to make retrying safe, not just to survive
a single client retry. This epic adds a background job that *uses* that
property: it periodically re-attempts every `PendingReconciliation` transfer
by resuming from exactly where it stopped, with no end-user request driving
it.

## Decisions

**Resume from after the debit, not from the top.** A transfer only ever
reaches `PendingReconciliation` *after* its debit has already succeeded —
that's the one invariant every path into this state shares (see
`ReconciliationNeededAsync`'s call sites). So the resume path
(`TransferService.ResumeReconciliationAsync`) skips every pre-debit check
(ownership, limits, currency, balance) entirely and re-resolves only the
credit destination before re-running the shared post-debit sequence
(ledger → credit → fee leg → finalize). This isn't just an optimization: the
background job has no end-user bearer token to re-run the
ownership-checked source-wallet lookup with, so re-validating ownership on
every resume attempt was never an option — and re-validating currency/limits
that were already proven true before the debit succeeded would be pure
busywork. The shared tail (`FinishAfterDebitAsync`) is now the single place
both the normal post-debit flow and the reconciliation resume flow go
through — no separate, drifting copy of the ledger/credit/fee logic.

**Capped retries, with an honest give-up state.** Retrying forever on a
permanently-broken transfer (bad config, a downstream service deleted, data
corruption) would just hide the problem instead of surfacing it. After
`Reconciliation:MaxAttempts` resume attempts, a transfer moves to
`TransferStatus.ReconciliationFailed` — terminal, distinct from
`PendingReconciliation`, and not picked up by the job again. This needs a
human. A client retry (same Idempotency-Key) against a
`ReconciliationFailed` transfer gets a `409` back with that status rather
than silently re-attempting it — being stuck is the honest answer, not
another spin of the wheel.

**Single-instance assumption, stated not hidden.** The job processes
`PendingReconciliation` transfers sequentially within one poll tick, so
there's no in-process double-processing. There is no cross-instance lock
(e.g. `SELECT ... FOR UPDATE SKIP LOCKED`) — fine as long as exactly one
`FlowPay.Transfers` replica runs, which is the current deployment. Running
more than one replica without adding that lock would let two instances
pick up the same transfer in the same tick; both would land on the same
safe, idempotent outcome (nothing double-moves), but duplicate work would
happen. Flagged, not built around, consistent with this project's existing
stance on saga/outbox infrastructure.

**A config change mid-flight can wedge a transfer, and that's accepted.**
The fee amount is recomputed from `TransferPolicy:ExternalTransferFeeMinorUnits`
on every attempt, not snapshotted at creation. If an operator changes the fee
config while a transfer is stuck in `PendingReconciliation`, a resume attempt
would try to charge a *different* fee under the *same* fee idempotency key —
`FlowPay.Wallet`'s own idempotency-conflict check catches this (refuses to
silently charge a different amount under a reused key) rather than
corrupting anything, but the transfer stays stuck until `MaxAttempts` is
hit and it surfaces as `ReconciliationFailed`. Fixing this fully means
snapshotting the quoted fee at creation time instead of recomputing it —
left as an open question rather than built now, since it's a narrow
operational edge case, not a reachable one from normal usage.

**`FailureReason` is cleared on eventual success.** A transfer that failed
once at the ledger/credit step and later resolves to `Completed` or
`SubmittedExternally` no longer carries a stale `FailureReason` describing
the attempt that didn't work. Minor pre-existing rough edge from Epic 5,
fixed here since it's directly adjacent to the code this epic touches.

## User story

### US-8.1: Automatically resolve pending reconciliation

As the platform, I want `PendingReconciliation` transfers to be retried
automatically, so a transient downstream failure doesn't leave money in an
unresolved state until a human notices and manually retries it.

**Acceptance criteria**
- [x] A background job (`ReconciliationBackgroundService`) polls every
      `Reconciliation:PollIntervalSeconds` and re-attempts every
      `PendingReconciliation` transfer with fewer than
      `Reconciliation:MaxAttempts` attempts so far
- [x] A resumed transfer that completes its remaining steps reaches
      `Completed`/`SubmittedExternally` exactly as if nothing had gone wrong,
      including the sender (and recipient, for internal transfers)
      notification
- [x] A transfer that exhausts `MaxAttempts` moves to the terminal
      `ReconciliationFailed` status and stops being retried automatically
- [x] One bad transfer throwing during a sweep doesn't stop the rest of that
      sweep from being processed, and doesn't crash the background service

**Status:** Implemented in `services/FlowPay.Transfers/ReconciliationBackgroundService.cs`
(new), `TransferService.ResumeReconciliationAsync`/`FinishAfterDebitAsync`
(new/extracted), `TransferRepository.GetPendingReconciliationAsync` (new).

## Open questions

- Snapshotting the quoted fee at transfer-creation time instead of
  recomputing it on every attempt (see "a config change mid-flight" above).
- No cross-instance locking — fine for a single replica, not for scaling
  `FlowPay.Transfers` horizontally.
- No alerting/paging when a transfer reaches `ReconciliationFailed` — an
  operator currently has to know to query for that status.
