# Epic 6: Transaction Visibility & Notifications

Maps to the PRD capabilities: "View transaction history," "Receive
transaction notifications," "Track pending, successful, failed, and
reversed transactions."

## Summary

Letting a customer see what happened to their money after Epic 5
(Transfers) moves it: status tracking and history on demand, notifications
pushed proactively. This is a read/notify layer over the ledger — it
doesn't move money itself.

## User stories

### US-6.1: <!-- TODO title, e.g. "View transaction history" -->
As a <!-- user type -->, I want to <!-- action -->, so that <!-- benefit -->.

**Acceptance criteria**
- [ ] <!-- TODO -->

## Open questions

- Notification channels (push, email, SMS, in-app) and which events trigger
  which channel.
- Does transaction history read from `FlowPay.Ledger` directly, or does
  `FlowPay.Notifications`/a new read-model service maintain its own
  projection for query performance?
