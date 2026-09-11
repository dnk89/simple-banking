# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status

**No source code exists yet.** The repository currently contains only `README.md` and four ADRs under `docs/adr/`. There is no `.sln`/`.csproj`, no `src/` or `tests/` tree, no Docker Compose file, and no CI workflow, even though the README describes them as if present. Treat the README and ADRs as the approved design spec to implement against, not as a description of existing code.

Because there is no code yet, there are no build/lint/test commands to run. Once the projects are scaffolded, the intended test command (per README) is `dotnet test`; update this file with real commands (including how to run a single test) as soon as the solution exists.

## What this project is

A .NET 10 backend ("Payment Gateway (ISO 20022)") that accepts payment orders over REST, turns them into signed ISO 20022 `pain.001` files, sends them to a simulated bank, and tracks each payment to its final status. It exists to demonstrate reliable messaging patterns in a payments context: transactional outbox, idempotent consumers, and retries over Kafka.

Intended service topology (see README "Project structure"):

```text
src/
  Payments.Api/          REST API, domain model, outbox
  BankGateway.Worker/    Kafka consumer, ISO 20022 mapping and signing
  MockBank/              Simulated bank with configurable failures
  Contracts/             Shared event contracts
tests/
  [test projects]
```

Flow: Client → `POST /payments` (with `Idempotency-Key`) → Payments API writes payment + outbox record in one DB transaction → outbox relay publishes to Kafka (`payment-events`, keyed by payment ID) → BankGateway.Worker maps to signed `pain.001`, sends to MockBank → MockBank returns `pain.002` → Worker publishes to `bank-status-events` → API updates payment status.

## Architecture decisions that constrain implementation

The ADRs in `docs/adr/` are binding design decisions, not background reading — read the relevant one before touching related code, since each documents *why* the obvious alternative was rejected.

- **[ADR-001](docs/adr/001-transactional-outbox.md) — Transactional outbox.** Payment + `PaymentReceived` event are written in the same DB transaction (`outbox_messages` table, `sequence`/`event_id`/`aggregate_id`/`event_type`/`payload`/`published_at`). A hosted-service relay polls unpublished rows with `FOR UPDATE SKIP LOCKED` and publishes them. Only **one relay instance** is supported (multiple instances can reorder events of one payment). Delivery is at-least-once, so **every consumer must be idempotent**.
- **[ADR-002](docs/adr/002-kafka-message-key.md) — Kafka topics and keying.** Topics: `payment-events`, `bank-status-events`, `payment-events.retry`, `payment-events.dlt`. **Every message is keyed by payment ID** so all events for one payment land on the same partition and stay ordered. Producers use `acks=all` + `enable.idempotence=true`; consumers commit offsets only after full processing (auto-commit disabled). Worker retry flow: a few in-process retries → `payment-events.retry` (delayed) → `payment-events.dlt` after the retry limit. The retry topic can deliver a message *after* later events of the same payment, so the API's state machine must ignore invalid/backward transitions rather than error on them. All producers use Confluent.Kafka (librdkafka) for partitioner consistency — do not mix in a Java Kafka client without matching `murmur2_random`.
- **[ADR-003](docs/adr/003-iso20022-adapter.md) — ISO 20022 stays out of the domain.** The API's public contract and all Kafka events use business fields only (debtor/creditor IBAN, name, amount, currency, remittance info, execution date, end-to-end ID) — never XML. **Only `BankGateway.Worker` knows ISO 20022** (`pain.001.001.13` generation + signing + XSD validation, `pain.002.001.15` parsing). The API still validates SEPA-format constraints at the edge (amount/currency/IBAN checksum/name & remittance length/character set) so payments that could never become valid `pain.001` fail fast with `400` instead of failing later, asynchronously. `pain.002` status mapping: `ACTC`/`PDNG` → no event, `ACCP`/`ACSP`/`ACSC` → `PaymentAccepted`, `RJCT` → `PaymentRejected`.
- **[ADR-004](docs/adr/004-no-batching.md) — One payment per `pain.001` file.** No batching: `NbOfTxs=1`, `CtrlSum` = the single payment amount. The `pain.002` parser reads transaction-level (not just group-level) statuses so batching can be added later without a rewrite.

## Simplifications (per README, intentional for this project's scope)

- MockBank replaces real bank connectivity; XML is signed with a self-signed certificate.
- API key authentication instead of OAuth2.
- Single Kafka broker (KRaft mode) in Docker Compose, 3 partitions/topic, replication factor 1 — not production topology.

## Working conventions

- The README's "Failure scenarios" table is meant to only list a row once a test proves it — don't add scenarios there speculatively.
- Keep ADR-documented invariants intact: single outbox relay instance, payment-ID Kafka keying, ISO 20022 confined to the bank adapter, one payment per `pain.001`. If an implementation needs to deviate from one of these, that's an ADR update, not a silent code change.
