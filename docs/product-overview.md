# Product Overview

**Document Type:** Product Requirements Document (PRD) — Overview
**Architecture:** Microservices
**Domain:** Fintech / Digital Wallet / Payments
**Implementation Language:** .NET (ASP.NET Core)
**Purpose:** Define the product, business, functional, financial, security, reliability, and operational requirements before choosing implementation technologies.

## Problem

<!-- TODO: What problem does FlowPay solve? e.g. gap in existing payment rails,
     friction in sending/receiving money, lack of a digital wallet for a
     specific market/segment, etc. -->

## What FlowPay does

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

The platform must maintain accurate financial records at all times and be
able to answer, for any transaction:

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

**Financial correctness, security, traceability, and recoverability take
priority over implementation convenience.** See
[Product Requirements](product-requirements.md) for the full breakdown.

## Target users

<!-- TODO: Who uses FlowPay? e.g. individual consumers, merchants, freelancers,
     specific geography/demographic. -->

## Goals

<!-- TODO: What does success look like? e.g. number of active wallets,
     transaction volume, time-to-first-transfer. -->

## Scope

### In scope

<!-- TODO -->

### Out of scope

<!-- TODO -->

## Related docs

- [PRD (source document)](PRD.md)
- [Product Requirements](product-requirements.md)
- [Architecture](architecture.md)
- [Epics](epics/)
- [Glossary](glossary.md)
