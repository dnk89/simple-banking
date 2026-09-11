# ADR-004: One payment per pain.001 file

**Status:** Accepted
**Date:** 2026-09-11

## Context

A single `pain.001` message can carry many payments. It has one group header, one or more payment information blocks (each with its own debtor account and execution date), and one or more transactions in each block.

Real corporate systems usually batch payments into one file:

- it reduces the number of bank submissions, and some banks charge per file;
- a batch can be booked as a single debit on the debtor's statement;
- approval workflows can sign a whole batch at once.

Batching also adds significant complexity:

- **Grouping and scheduling:** rules for which payments belong together (same debtor account and execution date), plus a time window or scheduler to collect them.
- **Partial results:** a `pain.002` can accept a file but reject individual transactions in it (group status `PART`).
- **Risky retries:** resending a partially accepted file can pay some creditors twice.
- **Correlation:** one file maps to many payments, each with its own status.

## Decision

Each `pain.001` message contains **exactly one payment**: one group header, one payment information block and one transaction. `NbOfTxs` is `1`, and `CtrlSum` equals the payment amount.

The `pain.002` parser still reads transaction-level statuses, not only the group status, so batching can be added later without rewriting it.

## Alternatives considered

- **Batch by debtor account and execution date within a time window:** the realistic approach, but it needs a scheduler, partial-status handling and safe resubmission rules. Out of scope for the first version.
- **Batch by count (send when N payments are collected):** a simpler trigger, but the same complexity in status handling, and it adds unpredictable delay when volume is low.

## Consequences

**Positive**

- One file maps to exactly one payment, so correlation and audit are trivial.
- A retry or rejection affects exactly one payment.
- No scheduler is needed, and payments are sent as soon as they are received.

**Negative**

- **Not realistic for high volumes:** one bank submission per payment, and potentially higher bank fees.
- **No batch booking** on the debtor's statement.

## When to revisit

When debtors send many payments per day, or when a bank charges per file. A likely design is a `PaymentBatch` entity in the worker, grouped by debtor IBAN and execution date, with explicit handling of `PART` statuses.
