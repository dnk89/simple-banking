# ADR-001: Transactional outbox instead of dual writes

**Status:** Accepted
**Date:** 2026-09-11

## Context

When a client creates a payment, the Payments API must do two things: store the payment in PostgreSQL and publish a `PaymentReceived` event to Kafka, so the bank gateway worker can send the payment to the bank.

These are two separate systems, and no transaction covers both. Kafka cannot take part in a PostgreSQL transaction, and Kafka's own transactions only make writes atomic within Kafka.

Writing to both directly (a "dual write") fails in two dangerous ways:

- **The database commit succeeds, but publishing fails** (crash, network error, broker unavailable). The payment is stored but never reaches the bank. It stays in `Received` forever, and nobody notices.
- **Publishing succeeds, but the database commit fails.** The worker sends a payment to the bank that does not exist in our database. In a payments system this is the worst possible outcome: money moves with no record of why.

## Decision

The API writes the event to an `outbox_messages` table **in the same database transaction** as the payment. Either both are saved, or neither is.

Outbox table columns:

| Column | Purpose |
|---|---|
| `sequence` | Identity column that defines publishing order |
| `event_id` | Unique event ID (UUID), sent as a Kafka header for duplicate detection |
| `aggregate_id` | Payment ID, used as the Kafka message key |
| `event_type` | Event name, for example `PaymentReceived` |
| `payload` | Event body (`jsonb`) |
| `created_at` | When the event was stored |
| `published_at` | Null until the event is published |

A background relay runs as a hosted service inside the API process:

1. In a transaction, select a small batch of unpublished rows ordered by `sequence`, using `FOR UPDATE SKIP LOCKED`.
2. Publish each event to Kafka (key and headers as described in ADR-002) and wait for broker acknowledgement.
3. Set `published_at` on the published rows and commit.

The polling interval is configurable, with a default of 1 second.

## Alternatives considered

- **Dual write (commit, then publish):** the simplest option, but events are lost when the process crashes between the two steps.
- **Publish first, then commit:** creates payments at the bank that do not exist in our database. Rejected outright.
- **Distributed transaction (two-phase commit):** Kafka does not support XA transactions with PostgreSQL, and two-phase commit adds coupling and availability problems even where it is supported.
- **Change data capture (Debezium reading the PostgreSQL write-ahead log):** no polling and scales well, but requires running Kafka Connect and Debezium. Too much infrastructure for this project, and a reasonable next step at higher volume.
- **Publish to Kafka first, then consume our own event to write the database:** the API could no longer confirm synchronously that a payment is stored, and idempotency-key checks become much harder.

## Consequences

**Positive**

- No lost events and no phantom payments. The database is the single source of truth.
- Kafka downtime does not block payment creation. Events wait in the outbox and are published when Kafka is available again.

**Negative**

- **Delivery is at-least-once.** If the relay crashes after Kafka acknowledges but before the transaction commits, the same events are published again after restart. Every consumer must be idempotent.
- **Publishing is delayed** by up to the polling interval.
- **Ordering is only guaranteed with a single relay instance.** `SKIP LOCKED` stops two instances from publishing the same rows at the same time, but they could publish events of one payment out of order. One relay instance is the supported setup, and consumers still tolerate out-of-order events (see ADR-002).
- **The relay holds a database transaction open** while waiting for Kafka acknowledgements, so batches must stay small.
- **The outbox table grows** and needs a cleanup job that deletes published rows after a retention period.
