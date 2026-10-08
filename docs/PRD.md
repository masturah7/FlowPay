# FlowPay — Digital Wallet & Payments Platform

## Product & Functional Requirements

- **Document Type:** Product Requirements Document (PRD)
- **Architecture:** Microservices
- **Domain:** Fintech / Digital Wallet / Payments
- **Implementation Language:** Not specified
- **Purpose:** Define the product, business, functional, financial, security, reliability, and operational requirements before choosing implementation technologies.

> This is the source PRD as provided. It's also reflected in more detail
> across [Product Overview](product-overview.md),
> [Product Requirements](product-requirements.md), and
> [Architecture](architecture.md) — keep this file and those in sync if either changes.

## 1. Product Overview

FlowPay is a digital wallet and payment platform that enables customers to:

- Register and manage an account
- Authenticate securely
- Complete identity verification
- Create and manage wallets
- Hold supported currencies
- Fund wallets
- Transfer money to other FlowPay users
- Transfer money to external bank accounts
- Save beneficiaries
- View transaction history
- Pay transaction fees
- Operate within configured transaction limits
- Receive transaction notifications
- Track pending, successful, failed, and reversed transactions

The platform must maintain accurate financial records at all times. For any
transaction, the system must be able to answer:

1. **Where did the money come from?**
2. **Where did the money go?**
3. **Can we prove exactly what happened?**

When failures occur, the system must additionally answer:

- Did money move?
- Should money move?
- Can this request safely be retried?
- Does anything need to be reversed?
- Does anything require reconciliation?
- Can operations reconstruct what happened?

Financial correctness, security, traceability, and recoverability take
priority over implementation convenience.
