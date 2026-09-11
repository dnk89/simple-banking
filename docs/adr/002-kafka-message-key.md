# ADR-002: Kafka as the event broker, with payment ID as message key

**Status:** Accepted
**Date:** 2026-09-11

## Context

Services communicate asynchronously through events. The Payments API publishes payment events, and the bank gateway worker publishes bank status events.

Events for a single payment must be processed in order. For example, the API must not apply `Accepted` to a payment before `Submitted`.

Kafka guarantees ordering only within a partition, not across a whole topic. The partition a message goes to is determined by its key.

The project deliberately uses Kafka to demonstrate log-based messaging, alongside production experience with a queue-based broker (Azure Service Bus).

## Decision

Kafka is the event broker, with these topics:

| Topic | Producer | Consumer |
|---|---|---|
| `payment-events` | Payments API (outbox relay) | Bank gateway worker |
| `bank-status-events` | Bank gateway worker | Payments API |
| `payment-events.retry` | Bank gateway worker | Bank gateway worker |
| `payment-events.dlt` | Bank gateway worker | None (manual inspection) |

**Every message uses the payment ID as its key.** All events for one payment land on the same partition and are consumed in order.

**Producers** use `acks=all` and `enable.idempotence=true`, so producer retries neither lose nor duplicate messages within a producer session.

**Consumers** store an offset only after a message has been fully processed. Automatic offset storing is disabled. Each service uses its own consumer group.

**Message headers** carry the event ID (for duplicate detection), the event type, and W3C trace context (`traceparent`) for distributed tracing.

**Retries in the worker** work in three steps:

1. A few in-process retries with exponential backoff for transient errors.
2. If those fail, the message moves to `payment-events.retry`. The retry consumer waits until the message's scheduled retry time before processing it.
3. After the retry limit, the message moves to `payment-events.dlt`.

**Local setup:** a single broker in KRaft mode, 3 partitions per topic, replication factor 1. A production setup would use at least 3 brokers, replication factor 3 and `min.insync.replicas=2`.

## Alternatives considered

- **No message key:** the best load distribution, but events of one payment can land on different partitions and be processed out of order. Rejected.
- **Debtor account as key:** orders all payments from one account, which this system does not need. Large accounts would create hot partitions and reduce parallelism. It would be the right choice if processing depended on account-level ordering, such as balance checks.
- **One partition per topic:** global ordering, but no parallelism at all.
- **RabbitMQ or Azure Service Bus:** native per-message retries and dead-lettering, and Service Bus sessions give per-key ordering. Kafka was chosen for log retention and replay, which help with audit and reprocessing, and to demonstrate log-based messaging.

## Consequences

**Positive**

- Events of each payment are processed in order.
- Parallelism scales with the number of partitions.
- Events can be replayed by resetting consumer offsets.

**Negative**

- **Retries and dead-lettering are custom code**, not broker features.
- **The retry topic breaks ordering.** A message in the retry topic can be processed after later events of the same payment. The API's state machine therefore ignores invalid or backward transitions instead of failing on them.
- **Changing the partition count later moves keys to different partitions**, which can break ordering during the change. Partition counts are chosen up front.
- **Key hashing must be consistent across producers.** Confluent.Kafka (librdkafka) hashes keys differently by default than the Java client. All producers here use Confluent.Kafka. If a Java producer is ever added, both must use the same partitioner (`murmur2_random` in librdkafka).
- **Consumer parallelism is capped** by the number of partitions.
