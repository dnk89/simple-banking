# Roadmap

## Why this exists

The solution is fully scaffolded but carries **no domain logic**: every `.cs` file is stock `dotnet new` output, both web apps still serve `/weatherforecast`, `Contracts` holds an empty `Class1`, and all three test projects contain one empty `[Fact]`. Every env var already wired in `docker-compose.yml` is read by nothing. CI passes only because three empty tests pass.

The infrastructure, by contrast, is real: `.slnx` with 7 projects, Central Package Management with 18 pinned packages, three hand-written Dockerfiles, Compose with Postgres 17 + Kafka (KRaft) + a `kafka-init` job creating all four ADR-002 topics + Aspire Dashboard, and GitHub Actions CI. Four binding ADRs and both ISO 20022 XSDs are committed.

So the README and ADRs are a **design spec with no implementation**. Every README "Highlights" bullet is aspirational, all seven "Failure scenarios" rows carry placeholder `[TestName](link-to-test)` links, and `docs/images/trace.png` is a broken reference. This roadmap turns that spec into an ordered build plan whose end state is a repo where **every README claim is backed by a passing test**.

Ordering is deliberately **reliability-first**: the outbox, idempotency, retry and dead-letter machinery lands before the domain richness and before ISO 20022, because those patterns are the reason the project exists.

## Decisions taken up front

| Decision | Choice |
|---|---|
| Milestone ordering | Outbox/Kafka (M1) → idempotency/retry/DLT (M2) → domain/SEPA validation (M3) → ISO 20022/signing (M4) → tests/observability (M5), after an M0 foundation |
| Debtor name gap (`pain.001` mandates `Dbtr/Nm`; the contract has only `debtorIban`) | **Add required `debtorName`** (≤70, SEPA charset) to `POST /payments`. `InitgPty/Nm` comes from config |
| MockBank `pain.002` delivery | **Synchronous in the HTTP response.** The worker runs on `runtime:10.0` and has no HTTP server; an async callback would force an ASP.NET host in purely for realism |
| Retry-topic delay mechanism | **`retry-at` header + `Pause(tp)`/`Seek(tpo)`.** Pause is per-`TopicPartition`, so the other two partitions keep flowing |
| Shared outbox/Kafka code | **New `src/Messaging` project.** The worker dual-writes too (`sent_messages` + publish to `bank-status-events`), so it needs the same guarantee ADR-001 gives the API. `Contracts` stays a pure, dependency-free event package |
| State machine timing | **In M2, not M3.** It is a reliability pattern, and it avoids writing `ApplyBankStatusHandler` twice |

## Four hazards this plan works around

1. **The Dockerfiles copy only `src/<Project>/` and `src/Contracts/`.** So `src/Messaging/` (M0) and `docs/xsd/*.xsd` (M4) each need an explicit `COPY` line — otherwise **the local build passes and `docker compose build` fails**. This is why `docker compose build` is part of every milestone's verification.
2. **`tests/Integration.Tests` references both `Payments.Api` and `MockBank`,** which both use top-level statements. Each emits `internal partial class Program` in the global namespace, so `WebApplicationFactory<Program>` is ambiguous (CS0433). Fixed with distinct public marker types.
3. **Kafka publishes no host port** (`advertised: PLAINTEXT://kafka:9092` only), so `dotnet run` on the host can never reach the composed broker. Needs an `EXTERNAL://:9094` listener.
4. **Two DbContexts share one `payments` database** and will fight over a single `__EFMigrationsHistory` table unless each is pinned to its own schema's history table.

---

## M0 — Foundation

**Goal.** Delete every template artifact; create both EF contexts with their *full* (not placeholder) schemas; stand up `src/Messaging` and the Testcontainers harness.

**Exit criteria**
- `dotnet build` clean under `TreatWarningsAsErrors`; no `weatherforecast`, `Class1.cs`, `Worker.cs` or `UnitTest1.cs` remains.
- `docker compose up -d --build` → `/health/ready` returns 200; both `payments` and `bankgateway` schemas migrated.
- `dotnet test` applies both contexts' migrations against a real Postgres container.
- Every Compose env var is bound to an options type; a missing required value is a hard startup failure.

| # | Task | Files | Key types / signatures | Proven by |
|---|---|---|---|---|
| 0.1 | Strip templates; add marker types | delete `src/Contracts/Class1.cs`, `src/BankGateway.Worker/Worker.cs`, `tests/*/UnitTest1.cs`; rewrite all three `Program.cs`; add `src/Payments.Api/PaymentsApiMarker.cs`, `src/MockBank/MockBankMarker.cs` | `public sealed class PaymentsApiMarker;` — `WebApplicationFactory<T>` only needs `T`'s assembly (hazard 2) | `dotnet build` |
| 0.2 | Fix ports / drop HTTPS | both `Properties/launchSettings.json`, both `.http` files | drop the `https` profile; `http://localhost:8080` (API) / `8081` (MockBank); **remove `UseHttpsRedirection()`** — only HTTP 8080 is exposed, so it merely logs warnings | manual |
| 0.3 | Create `src/Messaging` | new `src/Messaging/Messaging.csproj`; edit `PaymentGateway.slnx`, both consumer `.csproj`, **both Dockerfiles (two `COPY src/Messaging/` lines each — the csproj before restore, sources after)** | references `Contracts`; packages `Confluent.Kafka`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.Extensions.Hosting` added via `dotnet package add <Id> --project src/Messaging/Messaging.csproj` (no `-n`/`--no-restore`) | `docker compose build` — catches hazard 1 |
| 0.4 | Options + startup validation | `src/Messaging/Kafka/KafkaOptions.cs`, `src/Messaging/Outbox/OutboxOptions.cs`, `src/Messaging/Retry/RetryOptions.cs`, `src/Payments.Api/Configuration/{Auth,Idempotency,Database}Options.cs`, `src/BankGateway.Worker/Configuration/{MockBank,Signing,Iso20022}Options.cs`, all six `appsettings*.json` | `AddOptions<KafkaOptions>().Bind(cfg.GetSection("Kafka")).ValidateDataAnnotations().ValidateOnStart()` | `Payments.Api.Tests/Configuration/OptionsBindingTests.cs` → `MissingBootstrapServers_FailsOnStart` |
| 0.5 | `PaymentsDbContext` + full schema | `src/Payments.Api/Data/PaymentsDbContext.cs`, `Data/Entities/{Payment,IdempotencyRecord}.cs`, `Data/PaymentsDbContextFactory.cs`, `Domain/PaymentStatus.cs` | `HasDefaultSchema("payments")`; **complete ADR-003 column set now** (see Schemas below) so M3 adds validation and no migration; `UseXminAsConcurrencyToken()`; `PaymentStatus { Received, Submitted, Accepted, Rejected, Failed }` as `varchar(16)` via `HasConversion<string>()` | `Integration.Tests/Data/MigrationTests.cs` → `PaymentsContext_MigratesOnEmptyDatabase` |
| 0.6 | `BankGatewayDbContext` + schema | `src/BankGateway.Worker/Data/BankGatewayDbContext.cs`, `Data/Entities/{SentMessage,ReceivedStatus}.cs`, `Data/BankGatewayDbContextFactory.cs` | `HasDefaultSchema("bankgateway")`; **each context pins its own history table** — `o => o.MigrationsHistoryTable("__EFMigrationsHistory", "payments")` / `"bankgateway"` (hazard 4) | `MigrationTests.BothContexts_MigrateIntoOneDatabase_WithSeparateHistoryTables` |
| 0.7 | Migrations + startup migrator | `src/*/Migrations/*`, `src/Messaging/Data/DatabaseMigrator.cs` | `MigrateAsync<TContext>(IServiceProvider, CancellationToken)` guarded by `pg_advisory_xact_lock`; gated on `Database:MigrateOnStartup` (default `true`) | covered by 0.5/0.6 |
| 0.8 | Health endpoints | `src/Payments.Api/Health/DatabaseHealthCheck.cs`, `src/MockBank/Program.cs` | hand-rolled `CanConnectAsync` check (`HealthChecks.EntityFrameworkCore` is not pinned); `/health/live` (`predicate: _ => false`) + `/health/ready`. **The worker gets none** — it runs on `runtime:10.0` with no HTTP server | `Payments.Api.Tests/Health/HealthEndpointTests.cs` → `Live_ReturnsHealthy_WithoutDatabase` |
| 0.9 | OTel bootstrap | `src/Messaging/Observability/OpenTelemetryExtensions.cs`, all three `Program.cs` | `AddServiceObservability(this IHostApplicationBuilder, string serviceName)` → traces + metrics + OTLP logs. **Register the W3C propagator explicitly** (`Sdk.SetDefaultTextMapPropagator`) — M1's Kafka carrier depends on it | manual (traces at `:18888`) |
| 0.10 | Test harness | `tests/Integration.Tests/Fixtures/{Postgres,Kafka,Infrastructure}Fixture.cs`, `Fixtures/InfrastructureCollection.cs`, `GlobalUsings.cs` in all three test projects | xunit **v2** `ICollectionFixture` (check the pins — this is not the v3 stack); one container pair per assembly; a fresh database per test class; `Kafka:GroupIdSuffix = Guid` per test. **Config via `ConfigureAppConfiguration` in-memory, never `appsettings.json`** — three referenced projects copy theirs into the same output directory, last-write-wins | exercised by 0.5/0.6 |
| 0.11 | Compose + CI | `docker-compose.yml`, `.github/workflows/ci.yml` | Kafka `EXTERNAL://:9094` advertised as `localhost:9094` + `ports: ["9094:9094"]` (hazard 3); `worker depends_on mockbank`; `pg_isready -U payments -d payments`; CI NuGet cache keyed on `Directory.Packages.props` | CI green |

**Deliberately stubbed.** Nothing functional — M0 ships no behaviour. `PaymentStatus.Failed` exists but is unreachable until M2.

**Docs unlocked.** No Highlights bullet is earned yet. Adds this file and new **`docs/adr/005-schema-and-migration-ownership.md`** (one database, two schemas, each service migrates its own, separate history tables, no cross-schema FKs).

---

## M1 — Outbox, relay, Kafka plumbing

**Goal.** Prove the ADR-001 guarantee: payment + outbox row commit atomically, and a `FOR UPDATE SKIP LOCKED` relay publishes keyed, headered events to `payment-events` with nothing lost across a crash.

**Exit criteria**
- `POST /payments` → `201` + `Location`; `GET /payments/{id}` works; both behind `X-Api-Key`.
- Killing the process between DB commit and publish loses no event — the relay publishes on restart.
- Real Kafka receives one message per payment, keyed by payment ID, with `event-id` / `event-type` / `traceparent` / `schema-version` headers.
- Two relay instances against one database cannot double-publish or reorder.
- A poison row stops blocking the head after `Outbox:MaxAttempts`.

| # | Task | Files | Key types / signatures | Proven by |
|---|---|---|---|---|
| 1.1 | Event contracts — **full shape now** | `src/Contracts/Events/{PaymentReceived,PaymentSubmitted,PaymentAccepted,PaymentRejected,PaymentFailed}.cs`, `Events/EventTypes.cs`, `Messaging/{Topics,KafkaHeaders}.cs`, `Serialization/EventJsonContext.cs` | `record PaymentReceived(Guid PaymentId, string ClientId, string DebtorIban, string DebtorName, string CreditorName, string CreditorIban, string? CreditorBic, decimal Amount, string Currency, string? RemittanceInfo, DateOnly RequestedExecutionDate, string EndToEndId, DateTimeOffset OccurredAt)`. The full ADR-003 field set even though M1 does not validate it — this is what **removes all contract rework in M3** | `Payments.Api.Tests/Serialization/EventSerializationTests.cs` → `PaymentReceived_RoundTrips_CamelCase` |
| 1.2 | Kafka producer | `src/Messaging/Kafka/{OutboundMessage,IEventPublisher,KafkaEventPublisher,KafkaProducerFactory,KafkaHeaderCarrier}.cs` | `Task<DeliveryResult<string,byte[]>> PublishAsync(OutboundMessage, CancellationToken)`; `ProducerConfig { Acks = All, EnableIdempotence = true, Partitioner = Partitioner.Murmur2Random, MessageSendMaxRetries = 5 }` — `Murmur2Random` set explicitly so ADR-002's interop note lives in code, not just prose. `KafkaHeaderCarrier` implements inject/extract over `Headers` for `Propagators.DefaultTextMapPropagator` | `Integration.Tests/Kafka/ProducerTests.cs` → `Publish_SetsKeyAndHeaders`, `SameKey_LandsOnSamePartition` |
| 1.3 | Outbox entity + writer | `src/Messaging/Outbox/{OutboxMessage,IOutboxWriter,OutboxWriter,OutboxModelBuilderExtensions}.cs` | ADR-001's seven columns **plus `attempts`, `last_attempt_at`, `last_error`** (an ADR amendment). `void Enqueue<T>(DbContext, string aggregateId, string eventType, T payload)`; `payload` as `jsonb`; `Sequence` as `UseIdentityAlwaysColumn()`; **partial index `WHERE published_at IS NULL`** | `Integration.Tests/Outbox/OutboxWriterTests.cs` → `Enqueue_WritesJsonbAndPartialIndexIsUsed` |
| 1.4 | Relay claim query | `src/Messaging/Outbox/{IOutboxStore,EfOutboxStore}.cs` | `ClaimBatchAsync(int batchSize, int maxAttempts, CancellationToken)` via `FromSqlInterpolated("… WHERE published_at IS NULL AND attempts < {max} ORDER BY sequence LIMIT {n} FOR UPDATE SKIP LOCKED")`. **Two traps:** `LIMIT` must precede `FOR UPDATE SKIP LOCKED`, and appending *any* LINQ operator makes EF wrap the query in a subquery and **silently drop the row lock** | `Integration.Tests/Outbox/ClaimBatchTests.cs` → `ConcurrentClaims_DoNotOverlap` |
| 1.5 | Relay hosted service | `src/Messaging/Outbox/OutboxRelayService.cs`, `src/Messaging/Locking/PostgresAdvisoryLock.cs` | `OutboxRelayService<TContext> : BackgroundService where TContext : DbContext`. Cycle: begin transaction → `pg_try_advisory_xact_lock(727001)`, skip the cycle if not acquired → claim batch → publish sequentially awaiting broker ack → set `published_at` → commit. **The per-cycle transaction-scoped lock upgrades ADR-001's "single instance only" from prose to enforcement** at the cost of one extra statement per cycle. Failure backoff 1s → 30s | `Integration.Tests/Outbox/OutboxRelayTests.cs` → `UnpublishedRows_ArePublishedInSequenceOrder`, `TwoRelayInstances_PublishEachRowExactlyOnce` |
| 1.6 | Partial-failure + poison rows | same as 1.5 | On publish failure: stop the batch, commit `published_at` for the prefix that succeeded, bump `attempts`/`last_error`, commit, back off. At `attempts >= 10` the claim query excludes the row; `Error` log + `outbox.poisoned` metric. **Trades one payment's ordering for liveness** — the alternative, blocking the ordered head forever, is the exact silent failure ADR-001 complains about | `OutboxRelayTests.PoisonRow_DoesNotBlockLaterRows`, `PartialBatchFailure_CommitsPublishedPrefix` |
| 1.7 | Cleanup service | `src/Messaging/Outbox/OutboxCleanupService.cs` | deletes `published_at < now() - Outbox:Retention` (7 days) hourly in 1000-row chunks; later also prunes `processed_messages` and `idempotency_keys` | `Integration.Tests/Outbox/OutboxCleanupTests.cs` → `PublishedRowsOlderThanRetention_AreDeleted` |
| 1.8 | API-key auth | `src/Payments.Api/Auth/{ApiKeyAuthenticationHandler,ApiKeyOptions,ClientContext}.cs` | scheme `"ApiKey"`, header `X-Api-Key`, keys from `Auth:ApiKeys[{ClientId,Key}]`, compared with `CryptographicOperations.FixedTimeEquals` over SHA-256; sets a `client_id` claim; `/health/*` and `/openapi` anonymous | `Payments.Api.Tests/Auth/ApiKeyTests.cs` → `MissingKey_Returns401`, `WrongKey_Returns401`, `ValidKey_SetsClientId` |
| 1.9 | Create/read endpoints | `src/Payments.Api/Endpoints/PaymentEndpoints.cs`, `Contracts/{CreatePaymentRequest,PaymentResponse}.cs`, `Application/CreatePaymentHandler.cs` | `MapPost("/payments")` → `201` + `Location` + body; `MapGet("/payments/{id:guid}")`. The handler writes `Payment` + `OutboxWriter.Enqueue(...)` in **one** `SaveChangesAsync` inside `BeginTransactionAsync`. **Validation deliberately absent beyond required-fields-present** | `Integration.Tests/Api/CreatePaymentTests.cs` → `Post_WritesPaymentAndOutboxRowInOneTransaction`, `Post_Returns201WithLocation`, `Get_UnknownId_Returns404` |
| 1.10 | Idempotency — key only | `src/Payments.Api/Application/IdempotencyStore.cs` | unique index `(client_id, key)`; on unique violation re-read and return the existing payment with `200` + `Idempotency-Replayed: true`. The fingerprint column exists but is written as `""` and never compared | `Integration.Tests/Api/IdempotencyTests.cs` → `SameKeyAndBody_ReturnsSamePayment_NoDuplicate` ← **README row 1** |
| 1.11 | Crash recovery | `tests/Integration.Tests/Outbox/CrashRecoveryTests.cs` | commit a payment with the relay disabled, dispose and rebuild the host, assert the message lands on `payment-events` | `CrashAfterCommitBeforePublish_PublishesOnRestart` ← **README row 3** |
| 1.12 | ProblemDetails + wire-up | `src/Payments.Api/Program.cs`, `Errors/ProblemTypes.cs` | `AddProblemDetails()`, RFC 9457 shapes, `type = https://simple-banking.dev/problems/{slug}`; register the relay and cleanup services as hosted services | `Payments.Api.Tests/Errors/ProblemDetailsTests.cs` |

**Deliberately stubbed → replaced in.** No SEPA validation (M3) · idempotency without fingerprinting, no `422` path (M3) · `Status` assigned directly, no transition table (M2) · nothing consumes `payment-events` yet (M2).

**Docs unlocked.** Highlights **"Transactional outbox"** and **"Idempotent API"** (word the latter as "a retried request never creates a second payment" — the `422` half is M3) · README rows **1** and **3** · project structure gains `src/Messaging/` and `docs/xsd/` · **ADR-001 amendments**: add the three new columns; replace "ordering is only guaranteed with a single relay instance" with the advisory-lock design; note the relay is shared with the worker; document the poison-row escape hatch.

---

## M2 — Idempotent consumers, retry, DLT, state machine

**Goal.** Consume `payment-events` exactly-once-in-effect, call a *stub* bank client, publish status back through the worker's own outbox, and route failures through `payment-events.retry` into `payment-events.dlt` with a visible `Failed` outcome.

**Exit criteria**
- A duplicate `event-id` produces exactly one bank submission (verifiable via `GET /bank/v1/_messages`).
- A bank timeout → three in-process retries → a `.retry` message with a future `retry-at`; the retry consumer neither spins nor stalls other partitions.
- After five retry cycles → `.dlt` **and** the payment becomes `Failed` via `PaymentFailed`.
- The API consumes `bank-status-events`; the payment reaches `Submitted` then `Accepted`.
- Offsets are stored only after successful processing.
- An `Accepted` arriving after `Rejected` is ignored, not an error.

| # | Task | Files | Key types / signatures | Proven by |
|---|---|---|---|---|
| 2.1 | Consumer base class | `src/Messaging/Kafka/{KafkaConsumerService,ConsumeContext,IMessageHandler}.cs` | `abstract class KafkaConsumerService<THandler> : BackgroundService`; `ConsumerConfig { EnableAutoCommit = true, AutoCommitIntervalMs = 5000, EnableAutoOffsetStore = false, AutoOffsetReset = Earliest, SessionTimeoutMs = 45000 }` — the idiomatic librdkafka reading of ADR-002's "automatic offset storing disabled". Poll loop on a `LongRunning` thread; `StoreOffset` **after** the handler returns; explicit `Commit()` + `Close()` on shutdown. `ActivityKind.Consumer` span from the extracted `traceparent` | `Integration.Tests/Kafka/ConsumerTests.cs` → `HandlerThrows_OffsetNotStored_MessageRedelivered` |
| 2.2 | Dedup store | `src/Messaging/Dedup/{ProcessedMessage,IProcessedMessageStore,EfProcessedMessageStore,ProcessedMessageModelBuilderExtensions}.cs` | `Task<bool> TryBeginAsync(string consumer, Guid eventId, DbContext, CancellationToken)` — inserts PK `(consumer, event_id)` **inside the handler's business transaction**, returning false on unique violation. `consumer` is a **logical** name (`"bank-gateway-worker"`), not the Kafka group id, so the main and retry consumers share dedup state | `Integration.Tests/Dedup/ProcessedMessageTests.cs` → `SecondInsertOfSameEventId_IsRejected` |
| 2.3 | Stub bank client | `src/BankGateway.Worker/Bank/{IBankClient,BankSubmission,BankSubmissionResult,BankStatusCode,StubBankClient}.cs` | `Task<BankSubmissionResult> SubmitAsync(BankSubmission, CancellationToken)`. **Keep this interface business-shaped and XML-free** so M4's `Iso20022BankClient` is a DI-registration swap rather than a rewrite — do *not* type it against JSON DTOs | `BankGateway.Worker.Tests/Bank/StubBankClientTests.cs` |
| 2.4 | MockBank canned responder | `src/MockBank/Endpoints/StubBankEndpoints.cs`, `Bank/{BankBehaviour,BehaviourDirectives,SubmissionLog}.cs`, `Auth/ApiKeyMiddleware.cs` | `POST /bank/v1/stub-submissions` → canned `ACTC`/`ACSP`. **Deterministic directives** parsed from `remittanceInfo`/`endToEndId`: `FAIL:AC04`, `DELAY:7000`, `HTTP500:2`, `TIMEOUT` (every character is SEPA-legal), plus global `Bank:Behaviour:{DelayMs,RejectRate,ErrorRate}` and a `_control` endpoint. `GET /bank/v1/_messages` lists submissions. **A repeat of a known id returns the stored original response.** Deterministic triggers beat random rates because integration tests must be reliable | `Integration.Tests/MockBank/BehaviourTests.cs` → `RepeatedMessageId_ReturnsStoredResponse_NoSecondSubmission` |
| 2.5 | Payment-events handler | `src/BankGateway.Worker/Handlers/PaymentReceivedHandler.cs`, `Consumers/PaymentEventsConsumer.cs`, `Bank/MessageIdFactory.cs` | group `bank-gateway-worker`. Per message: transaction → `TryBeginAsync` (duplicate → store offset and return) → **check `sent_messages` for an existing `Sent` row for this payment; if present, skip submission** → insert `SentMessage{Status=Pending}` → commit → `SubmitAsync` → transaction: mark `Sent`, enqueue `PaymentSubmitted` + the mapped status event to the worker outbox → commit. IDs: `MessageId = "PG" + paymentId("N")` (34 chars ≤ 35), `InstructionId = paymentId("N")` (32, per ADR-003). **Deterministic IDs + bank-side dedup + the skip check together make bank submission effectively exactly-once despite at-least-once Kafka** | `Integration.Tests/Worker/DuplicateDeliveryTests.cs` → `SameEventIdTwice_SubmitsBankFileOnce` ← **README row 4** |
| 2.6 | Worker outbox + relay | `src/BankGateway.Worker/Program.cs`, `src/Messaging/Kafka/TopicRouter.cs` | register `OutboxRelayService<BankGatewayDbContext>` with advisory lock key `727002`, routing `event_type` → topic | `Integration.Tests/Worker/StatusPublishingTests.cs` → `SubmittedEvent_ReachesBankStatusEvents` |
| 2.7 | Retry dispatcher | `src/Messaging/Retry/{RetryDispatcher,IRetryClassifier}.cs` | `DispatchAsync(ConsumeContext, Exception, CancellationToken)`: transient → republish to `.retry` with `retry-attempt`, `retry-at` (unix ms), `original-topic`, `error-reason`; attempt > 5 → `.dlt`. **Non-transient failures bypass retry entirely.** In-process: three attempts at 200/400/800 ms + jitter. Retry cycles: five at `5s, 15s, 60s, 300s, 900s` (~21 min of recovery window); tests override to 200 ms tiers | `BankGateway.Worker.Tests/Retry/RetryDispatcherTests.cs` → `TransientError_SetsRetryAtAndAttempt`, `DeterministicError_GoesStraightToDlt` |
| 2.8 | Delayed retry consumer | `src/BankGateway.Worker/Consumers/PaymentEventsRetryConsumer.cs` | group `bank-gateway-worker-retry`. If `retry-at` is in the future: `Pause([tp])`, `Seek(tpo)`, `await Task.Delay(min(remaining, 5s))`, `Resume([tp])`, and **do not store the offset**. **Pause is per-`TopicPartition`, so the other two partitions keep flowing** — that is the whole reason to pause and seek rather than block the poll loop. Messages are **processed in place** and never republished to `payment-events` (which would create a feedback loop); a further failure republishes to `.retry` with `attempt + 1` | `Integration.Tests/Worker/RetryTopicTests.cs` → `FutureRetryAt_IsNotProcessedEarly`, `PausedPartition_DoesNotStallOtherPartitions` |
| 2.9 | Bank timeout path | `src/BankGateway.Worker/Program.cs`, `Bank/BankErrorClassifier.cs` | `AddHttpClient<IBankClient,…>().AddStandardResilienceHandler()`; `MockBank:Timeout` 5s; `TimeoutRejectedException` / `HttpRequestException` / 5xx → transient; 4xx other than 408/429 → deterministic | `Integration.Tests/Worker/BankTimeoutTests.cs` → `BankTimesOut_RetriesThenRoutesToRetryTopic` ← **README row 5** |
| 2.10 | DLT + visible failure | `src/Messaging/Retry/RetryDispatcher.cs`, `Handlers/PaymentReceivedHandler.cs` | when dispatching to the DLT, also enqueue `PaymentFailed(PaymentId, Reason, OriginalTopic, FailedAt)` **in the same transaction as the DLT decision**, so a dead-lettered payment never sits in `Submitted` forever. `payments.dlt` metric + `Critical` log with the payment ID. **No DLT consumer** (per ADR-002) — surfacing is status + metric + log | `Integration.Tests/Worker/DeadLetterTests.cs` → `PersistentFailure_RoutesToDeadLetterTopic`, `PersistentFailure_MarksPaymentFailed` ← **README row 6** |
| 2.11 | State machine | `src/Payments.Api/Domain/PaymentStateMachine.cs`, `Domain/Payment.cs` | `static bool CanTransition(PaymentStatus from, PaymentStatus to)`; `TransitionOutcome Payment.TryApply(PaymentStatus next, string? reasonCode, DateTimeOffset at)` → `Applied \| IgnoredBackward \| IgnoredTerminal \| IgnoredSameState`. Table: `Received → {Submitted, Rejected, Failed}`; `Submitted → {Accepted, Rejected, Failed}`; `Accepted` and `Rejected` terminal; `Failed → {Submitted, Accepted, Rejected}` so a manually replayed DLT message can heal a payment. ~100 lines, pure, no dependencies — which is why it belongs here rather than in M3 | `Payments.Api.Tests/Domain/PaymentStateMachineTests.cs` → exhaustive 5×5 matrix theory + `AcceptedAfterRejected_IsIgnored` |
| 2.12 | API status consumer | `src/Payments.Api/Consumers/BankStatusConsumer.cs`, `Application/ApplyBankStatusHandler.cs` | group `payments-api-status`, a second hosted service alongside the relay (safe — the relay is producer/DB-bound, the consumer is consumer-bound, no shared mutable state). Handler: dedup → load → **`TryApply`** → save. Ignored transitions → `LogInformation` + `payments.transitions.ignored{from,to}` metric + **store the offset**, because ignoring is success, not failure — otherwise the retry topic deadlocks the consumer. On `DbUpdateConcurrencyException`, reload and retry once | `Integration.Tests/Api/OutOfOrderStatusTests.cs` → `AcceptedAfterTerminalRejected_IsIgnored` ← **README row 7**; `BankStatusConsumerTests.SubmittedThenAccepted_UpdatesPayment` |

**Deliberately stubbed → replaced in.** `StubBankClient` + `/bank/v1/stub-submissions` + their test are **deleted in M4** — roughly half a day of throwaway code, and worth it: it lets the retry/DLT tests inject timeouts and 500s deterministically with no XML in the picture · `SentMessage.Xml` is nullable and always null → M4 fills it and tightens the column · no `pain.002` parsing → M4.

**Docs unlocked.** Highlights **"Idempotent Kafka consumers"** · README rows **4, 5, 6, 7** · the lifecycle diagram gains `Received → Failed` and `Submitted → Failed` (and loses the "Update if you add states" comment) · **ADR-002 amendments**: pin the retry mechanics (header names, pause/seek, in-place processing, the 3 + 5 attempt budget, the transient-vs-deterministic split), record that DLT routing emits `PaymentFailed`, record the `EnableAutoCommit`/`EnableAutoOffsetStore` reading · new **`docs/adr/006-dead-letter-visibility.md`**.

---

## M3 — Domain and SEPA validation

**Goal.** Replace M1's placeholder validation with the real ADR-003 edge rules, request fingerprinting with `422`, and tenancy scoping.

**Exit criteria**
- Every ADR-003 rule rejects with `400` + ProblemDetails listing each violated field.
- Same key + different body → `422`; same key + same body → `200` replay, including across a `125.50` vs `125.5` formatting difference.
- `GET` for another client's payment → `404`, with no existence leak.

| # | Task | Files | Key types / signatures | Proven by |
|---|---|---|---|---|
| 3.1 | IBAN validation | `src/Payments.Api/Domain/Validation/Iban.cs` | `static bool TryParse(string?, out Iban)`; mod-97-10 over the rearranged string, a per-country length table (all SEPA countries; reject non-SEPA country codes), whitespace stripped, upper-cased | `Payments.Api.Tests/Domain/IbanTests.cs` → theory over `LV80BANK0000435195001`, `DE89370400440532013000`, bad checksum, bad length, lowercase, spaced |
| 3.2 | SEPA character set | `src/Payments.Api/Domain/Validation/SepaCharacterSet.cs` | `IsValid(string)` over `a-z A-Z 0-9 / - ? : ( ) . , ' +` and space; `FindFirstInvalid(string)` for the error detail. Note `InvariantGlobalization=true` is on, so no culture-dependent casing | `SepaCharacterSetTests` → `Umlaut_IsRejected`, `AllowedPunctuation_IsAccepted` |
| 3.3 | Amount / currency / lengths | `src/Payments.Api/Domain/Validation/CreatePaymentValidator.cs` | `ValidationResult Validate(CreatePaymentRequest)`: amount `> 0` and `decimal.Round(a,2) == a`; currency `== "EUR"`; names ≤70; remittance ≤140; endToEndId ≤35. **A past `requestedExecutionDate` is clamped forward with a warning, not rejected** — the README's own example body is already in the past, and banks treat backdated instructions as "today" | `CreatePaymentValidatorTests` → one test per rule + `AllViolations_AreReportedTogether` |
| 3.4 | `debtorName` contract change | `src/Payments.Api/Contracts/CreatePaymentRequest.cs`, `README.md`, `src/Payments.Api/Payments.Api.http`, `docs/adr/003-iso20022-adapter.md` | add required `string DebtorName` (≤70, SEPA charset) — `pain.001` mandates `Dbtr/Nm`, so the current contract literally cannot produce a valid file. `InitgPty/Nm` comes separately from `Iso20022:InitiatingPartyName`. **Omit `DbtrAgt`** (EPC 2025 permits IBAN-only); fall back to `FinInstnId/Othr/Id = NOTPROVIDED` if a bank ever demands it | `CreatePaymentValidatorTests.MissingDebtorName_Returns400` |
| 3.5 | Idempotency fingerprint | `src/Payments.Api/Application/RequestFingerprint.cs`, `Application/IdempotencyStore.cs` | `static string Compute(CreatePaymentRequest)` = lowercase hex SHA-256 over `0x1F`-joined canonical fields: IBAN/BIC upper-cased and whitespace-stripped, text `Trim()`ed (null → `""`), **amount as `a.ToString("0.00", CultureInfo.InvariantCulture)`** so `125.50` ≡ `125.5`, date as `yyyy-MM-dd`. Stored in `request_fingerprint char(64)`; a mismatch → `422` | `RequestFingerprintTests.AmountScaleDifference_ProducesSameFingerprint`; `IdempotencyTests.SameKeyDifferentBody_Returns422` ← **README row 2** |
| 3.6 | Replay semantics | `src/Payments.Api/Application/CreatePaymentHandler.cs` | A replay **rebuilds** `PaymentResponse` from the current payment row — no stored response blob, no drift, and the client sees the *current* status (documented consequence: a replay's status may differ from the original `201`). `200` + `Idempotency-Replayed: true`. Missing key → `400`; any string ≤255 chars is a valid key, no UUID requirement. **No `409`:** the payment and the idempotency row commit in one transaction, so a concurrent same-key request blocks on the unique index and then replays — document that `409` is unreachable by design rather than inventing an unused code | `IdempotencyTests.ConcurrentSameKey_OneCreateOneReplay_NoDuplicate`, `MissingIdempotencyKey_Returns400` |
| 3.7 | Idempotency TTL | `src/Payments.Api/Application/IdempotencyCleanupService.cs` | delete records older than `Idempotency:Retention` (24h). Deleting a key must **not** delete the payment — that is why it is its own table rather than two columns on `payments` | `IdempotencyCleanupTests.ExpiredKey_AllowsNewPaymentWithSameKey` |
| 3.8 | Tenancy scoping | `src/Payments.Api/Endpoints/PaymentEndpoints.cs` | `GET` filters on the `client_id` claim and returns `404` for a foreign payment; keys are already scoped `(client_id, key)` so two clients can reuse one UUID | `TenancyTests.OtherClientsPayment_Returns404`, `SameKeyDifferentClient_CreatesTwoPayments` |
| 3.9 | OpenAPI documentation | `src/Payments.Api/Endpoints/PaymentEndpoints.cs` | `.Produces<PaymentResponse>(201)`, `.ProducesProblem(400)`, `.ProducesProblem(422)`, `WithSummary`/`WithDescription`, `Idempotency-Key` and `X-Api-Key` declared as parameters | `OpenApiDocumentTests.Document_ListsAllPaymentResponses` |

**Deliberately stubbed.** Nothing new. The only remaining stub is M2's bank protocol, replaced in M4.

**Docs unlocked.** README row **2** · update the `curl` example and `Payments.Api.http` with `debtorName` · **ADR-003 amendments**: add `debtorName` to the accepted-fields and edge-validation lists; record the `DbtrAgt` omission; record date clamping.

---

## M4 — ISO 20022 and signing

**Goal.** Replace the stub protocol with real signed `pain.001.001.13` submission, XSD validation before every send, and transaction-level `pain.002.001.15` parsing.

**Exit criteria**
- The generated `pain.001.001.13` validates against `docs/xsd/pain.001.001.13.xsd` with `NbOfTxs=1` and `CtrlSum` equal to the amount.
- The XML carries a valid enveloped XMLDSig; MockBank verifies it and rejects a tampered document.
- MockBank returns a `pain.002.001.15` whose **transaction-level** status the worker maps per ADR-003.
- Resubmitting a payment yields the same `MsgId`, MockBank dedups it, and no second file is recorded.
- **`docker compose build` succeeds** with the XSDs embedded.

| # | Task | Files | Key types / signatures | Proven by |
|---|---|---|---|---|
| 4.1 | Embed the XSDs | `src/BankGateway.Worker/BankGateway.Worker.csproj`, `src/MockBank/MockBank.csproj`, **both Dockerfiles** | `<EmbeddedResource Include="..\..\docs\xsd\pain.001.001.13.xsd" Link="Xsd\pain.001.001.13.xsd" />` (plus the `.002`); **both Dockerfiles need `COPY docs/xsd/ docs/xsd/` before `dotnet publish`** or the container build breaks while the local build passes (hazard 1) | `SchemaSetTests.BothSchemas_LoadFromEmbeddedResources` + a CI `docker compose build` step |
| 4.2 | Schema set + validator | `src/BankGateway.Worker/Iso20022/{Iso20022SchemaSet,Iso20022Validator}.cs`, `src/MockBank/Bank/Iso20022Validator.cs` | `Lazy<XmlSchemaSet>` compiled once, thread-safe; `ValidationOutcome Validate(XDocument, string targetNamespace)` collecting *all* errors. Deliberately duplicated into MockBank — it is the counterparty, not part of our system | `ValidatorTests.GoldenValidFile_Passes`, `MissingCtrlSum_Fails` |
| 4.3 | pain.001 POCOs | `src/BankGateway.Worker/Iso20022/Pain001/*.cs` (~20 types) | `XmlSerializer` attributes, `[XmlRoot("Document", Namespace = "urn:iso:std:iso:20022:tech:xsd:pain.001.001.13")]`. **Hand-written, not generated:** the schema has ~200 `complexType`s of which ADR-004's one-payment-per-file profile needs ~20; generated code would run to thousands of lines, dwarfing the hand-written domain in a portfolio repo, and would fight `TreatWarningsAsErrors` + `EnforceCodeStyleInBuild`. The XSD remains the correctness net via 4.2. *Fallback if the subset proves unmaintainable:* XmlSchemaClassGenerator as a local `dotnet tool`, output checked in under `Iso20022/Generated/` with auto-generated headers | `Pain001SerializationTests.Serialize_ProducesXsdValidDocument` |
| 4.4 | pain.001 mapper | `src/BankGateway.Worker/Iso20022/Pain001Mapper.cs` | `Document Map(PaymentReceived, Iso20022Options)` generating `MsgId`, `CreDtTm`, `NbOfTxs="1"`, `CtrlSum=amount`, `InitgPty/Nm` from config, `PmtInfId`, `PmtMtd=TRF`, `SvcLvl/Cd=SEPA`, `ChrgBr=SLEV`, `InstrId=paymentId("N")`, `ReqdExctnDt`, `Dbtr/Nm`, `DbtrAcct/Id/IBAN`, `Cdtr/Nm`, `CdtrAcct/Id/IBAN`, optional `CdtrAgt/FinInstnId/BICFI`, `RmtInf/Ustrd`, `InstdAmt Ccy="EUR"`. Inject `TimeProvider` so golden-file tests stay stable | `Pain001MapperTests` → golden-file comparison against `tests/BankGateway.Worker.Tests/Golden/pain001-example.xml` + `Mapped_ValidatesAgainstXsd` |
| 4.5 | XML signer | `src/BankGateway.Worker/Signing/{XmlSigner,SigningCertificateProvider}.cs`, `scripts/generate-signing-cert.sh` | `SignedXml` with `Reference URI=""` + enveloped and C14N transforms, RSA-SHA256 / SHA-256, `KeyInfo/X509Data/X509Certificate`, `<ds:Signature>` appended as the **last child of `Document`**. **Critical ordering: XSD-validate the *unsigned* document, then sign** — ISO 20022's `Document` type permits no `ds:Signature` child, so doing it the other way makes every submission fail validation forever. MockBank must strip `Signature` before validating. Certificate: `Signing:Mode = EphemeralSelfSigned` (default — `CertificateRequest` + RSA-2048 generated at startup, thumbprint logged, so `docker compose up` needs zero manual steps) or `PfxFile` with the password via env/user-secrets; gitignore `certs/` | `XmlSignerTests.SignedDocument_PassesCheckSignature`, `TamperedDocument_FailsCheckSignature`, and **`UnsignedDocument_IsXsdValid_SignedIsNot`** which pins the ordering rule |
| 4.6 | Real bank client | `src/BankGateway.Worker/Bank/Iso20022BankClient.cs`, `Program.cs` | implements the **unchanged** `IBankClient` from M2: map → validate → sign → `POST {MockBank:BaseUrl}/bank/v1/pain001` with `application/xml`, `X-Api-Key`, `X-Message-Id` → parse the `pain.002` response. Swap the DI registration; delete `StubBankClient` and MockBank's `/bank/v1/stub-submissions` | `Iso20022SubmissionTests.Submit_SendsSignedXsdValidPain001` |
| 4.7 | Runtime XSD validation policy | `src/BankGateway.Worker/Bank/Iso20022BankClient.cs` | `Iso20022:ValidateBeforeSend = true` (default). **Validate at runtime *and* in tests** — this resolves the README-vs-ADR-003 conflict in favour of both. On failure the error is deterministic, so **no retry**: straight to `.dlt` + `PaymentFailed(schema-invalid)` + an `Error` log carrying the validation errors | `ValidationFailureTests.SchemaInvalidDocument_IsNotSent_AndGoesToDlt` |
| 4.8 | MockBank pain.001 endpoint | `src/MockBank/Endpoints/BankEndpoints.cs`, `Bank/{Pain001Reader,Pain002Writer,SignatureVerifier,SubmissionStore}.cs` | `POST /bank/v1/pain001`. Pipeline: API key → `XDocument.Parse` → **verify the signature** (`CheckSignature(cert, verifySignatureOnly: true)`; thumbprint trust checked only when `Bank:TrustedThumbprints` is non-empty — a signature nobody checks proves nothing) → strip `Signature` → XSD-validate → extract `MsgId`/`InstrId`/`EndToEndId`/`InstdAmt` via `XDocument` (no POCOs needed bank-side) → apply behaviour directives → emit `pain.002.001.15`. **Synchronous in the HTTP response**, `200 OK`, `application/xml`, group status `ACTC` with transaction status `ACSP` or `RJCT` + `StsRsnInf/Rsn/Cd`. Malformed, bad signature or schema-invalid → `400` + a group-level `RJCT` with reason `FF01`. **A known `MsgId` returns `200` with the stored original** | `Pain001EndpointTests.ValidSignedFile_ReturnsAcspPain002`, `TamperedSignature_Returns400Rjct`, `RepeatedMsgId_ReturnsStoredPain002` |
| 4.9 | pain.002 parser | `src/BankGateway.Worker/Iso20022/{Pain002Parser,BankStatusReport}.cs` | `BankStatusReport Parse(string xml)` reading `OrgnlGrpInfAndSts/GrpSts` **and** `OrgnlPmtInfAndSts/TxInfAndSts/{OrgnlInstrId, OrgnlEndToEndId, TxSts, StsRsnInf/Rsn/Cd}`. **Transaction-level status wins when it disagrees with the group status** (ADR-004) — `ACTC` group + `RJCT` transaction is exactly MockBank's rejection shape. Correlate by `OrgnlMsgId` + `OrgnlInstrId`; store the raw XML in `received_statuses` | `Pain002ParserTests.GroupAcptWithTxRjct_ReportsRejection`, `PartGroupStatus_ReadsEachTransaction`, theory over all six statuses |
| 4.10 | Status → event mapping | `src/BankGateway.Worker/Handlers/BankStatusMapper.cs` | `IEnumerable<object> Map(BankStatusReport, Guid paymentId)`: `ACTC`/`PDNG` → empty, `ACCP`/`ACSP`/`ACSC` → `PaymentAccepted`, `RJCT` → `PaymentRejected(ReasonCode)`; an unknown code logs and emits nothing (not a failure) | `BankStatusMapperTests` → the ADR-003 table as a theory |
| 4.11 | Tighten `sent_messages.xml` | `src/BankGateway.Worker/Migrations/*`, `Data/Entities/SentMessage.cs` | migration setting `xml NOT NULL` now that it is always populated; add an index on `received_statuses(message_id, instruction_id)` | the M0 migration test |

**Docs unlocked.** Highlights — **fix `pain.001.001.09`/`pain.002.001.10` → `.13`/`.15`** and reword to "XSD validation before every submission" · Simplifications gains "`ds:Signature` is enveloped inside `Document`; real bank profiles often use detached signatures or a transport container" · new **`docs/mockbank.md`** documenting the directive syntax and the `_control`/`_messages` endpoints, which makes the README's "configurable failures" claim true · "How AI was used" — "XSD-based classes" is no longer accurate, reword to "AI drafted the ISO 20022 POCOs from the XSD" · **ADR-003 amendments**: the validate-then-sign ordering, runtime validation, the synchronous-`pain.002` contract · **ADR-004 amendment**: transaction-level status wins over group status · new **`docs/adr/007-xml-signature-profile.md`**.

---

## M5 — Tests, observability, documentation

**Goal.** Close the test pyramid, make one payment's trace legible across all four services, and bring the documentation into exact agreement with the code.

**Exit criteria**
- One `[Trait("Category","E2E")]` test drives a payment from `POST /payments` to `Accepted` through real Postgres + Kafka + in-process MockBank.
- **All seven README failure-scenario rows link to a real passing test.**
- `docs/images/trace.png` exists and shows a single trace spanning api → kafka → worker → mockbank.
- CI: cached restore, `dotnet format --verify-no-changes`, `docker compose build`, coverage collected, E2E as a separate step.
- No README or CLAUDE.md statement contradicts the code.

| # | Task | Files | Key types / signatures | Proven by |
|---|---|---|---|---|
| 5.1 | E2E harness | `tests/Integration.Tests/E2E/{PaymentGatewayFixture,HappyPathTests}.cs`, `src/BankGateway.Worker/WorkerHost.cs` | one fixture starting `WebApplicationFactory<PaymentsApiMarker>`, `WebApplicationFactory<MockBankMarker>` (hazard 2), and the worker via a new `public static class WorkerHost { static HostApplicationBuilder CreateBuilder(string[] args, Action<IConfigurationBuilder>? configure = null); }` extracted from `Program.cs` so tests build the same DI graph. Wire the worker's `MockBank:BaseUrl` to the MockBank factory's `HttpClient` through a delegating `IHttpClientFactory`. Poll `GET /payments/{id}` on a 30s budget | `HappyPathTests.OnePayment_ReachesAccepted` |
| 5.2 | Rejection E2E | `tests/Integration.Tests/E2E/RejectionTests.cs` | drive `FAIL:AC04` through remittance info; assert `Rejected` with `rejectReasonCode == "AC04"` | `RejectionTests.BankRejects_PaymentIsRejectedWithReasonCode` |
| 5.3 | Trace propagation polish | `src/Messaging/Kafka/*`, `src/Messaging/Observability/MessagingActivitySource.cs` | producer span `publish {topic}` (`ActivityKind.Producer`), consumer span `process {topic}` (`ActivityKind.Consumer`) with `parentContext` from the extracted `traceparent` **plus an `ActivityLink`** to the producer span. OTel messaging semantic-convention attributes. **The inject/extract carrier itself shipped in M1/M2** — deferring it to here would have meant retrofitting every produce and consume site. `OpenTelemetry.Instrumentation.ConfluentKafka` is deliberately **not** added: it is not pinned, and the hand-rolled propagation is the more instructive artefact | `TracePropagationTests.ApiAndWorkerSpans_ShareOneTraceId` via an in-memory `ActivityListener` |
| 5.4 | Metrics | `src/Messaging/Observability/MessagingMetrics.cs`, `src/Payments.Api/Observability/PaymentMetrics.cs`, `src/BankGateway.Worker/Observability/BankMetrics.cs` | counters `payments.created`, `payments.transitions.ignored`, `payments.dlt`, `outbox.published`, `outbox.poisoned`, `bank.submissions`, `bank.retries`; histogram `bank.submission.duration`; `ObservableGauge outbox.pending` fed from a **value cached by the relay** — never query the database inside a gauge callback, it runs on the metrics export thread | `MetricsTests.CreatePayment_IncrementsCounter` via `MeterListener` |
| 5.5 | Log correlation | all three `Program.cs`, `OpenTelemetryExtensions.cs` | OTLP log exporter, `IncludeScopes = true`, and a `payment.id` log scope pushed in the API handler and both consumers | manual (Aspire structured logs) |
| 5.6 | Trace screenshot | `docs/images/trace.png`, `README.md` | compose up, create one payment, screenshot the Aspire trace view — fixes the currently-broken README image reference | visual |
| 5.7 | CI hardening | `.github/workflows/ci.yml` | NuGet cache; `dotnet format --verify-no-changes`; `dotnet test --filter "Category!=E2E" --collect:"XPlat Code Coverage"`; a separate E2E step; a **`docker compose build` step** which catches the hazard-1 class of bug | CI green on a PR |
| 5.8 | README truth pass | `README.md`, `CLAUDE.md` | fill all seven `Proven by` links with real test names and permalinks; final Highlights wording; `docker compose up -d --build` consistently in both files; project structure including `src/Messaging/` and `docs/xsd/` | reviewer read-through |
| 5.9 | ADR consolidation | `docs/adr/001`–`004` amendments, new `005`–`007` | land every amendment listed above; add an "Amended" header line to each touched ADR, per this project's own rule that deviations are ADR updates rather than silent code changes | reviewer read-through |

---

## Schemas created in M0

**`payments.payments`** — `id uuid pk`, `client_id`, `status varchar(16)`, `debtor_iban varchar(34)`, `debtor_name varchar(70)`, `creditor_name varchar(70)`, `creditor_iban varchar(34)`, `creditor_bic varchar(11) null`, `amount numeric(18,2)`, `currency char(3)`, `remittance_info varchar(140) null`, `requested_execution_date date`, `end_to_end_id varchar(35)`, `reject_reason_code varchar(35) null`, `failure_reason text null`, `created_at`, `updated_at`, `xmin` (concurrency token). Index on `(client_id, status, created_at)`.

**`payments.idempotency_keys`** — `client_id`, `key varchar(255)`, `request_fingerprint char(64)`, `payment_id`, `created_at`. Unique on `(client_id, key)`. Its own table so TTL cleanup never touches payments.

**`<schema>.outbox_messages`** (in both schemas) — ADR-001's `sequence` (identity always), `event_id`, `aggregate_id`, `event_type`, `payload jsonb`, `created_at`, `published_at`, **plus `attempts`, `last_attempt_at`, `last_error`**. **Partial index `WHERE published_at IS NULL`** — the one index that matters as the table grows.

**`<schema>.processed_messages`** (in both schemas) — PK `(consumer, event_id)`, `processed_at`. Seven-day retention.

**`bankgateway.sent_messages`** — `payment_id pk`, `message_id unique`, `payment_info_id`, `instruction_id`, `xml` (nullable until M4.11), `status`, `sent_at`, `created_at`, `xmin`.

**`bankgateway.received_statuses`** — raw `pain.002` XML plus the parsed status, indexed on `(message_id, instruction_id)`.

## Configuration surface

`Outbox:{PollingInterval=00:00:01, BatchSize=20, MaxAttempts=10, CleanupInterval=01:00:00, Retention=7.00:00:00}` · `Retry:{InProcessAttempts=3, MaxCycles=5, Delays=[5s,15s,60s,300s,900s]}` · `Kafka:{BootstrapServers, GroupIdSuffix}` · `Auth:ApiKeys[{ClientId,Key}]` · `Idempotency:Retention=24:00:00` · `Database:MigrateOnStartup=true` · `MockBank:{BaseUrl, Timeout=00:00:05}` · `Signing:{Mode=EphemeralSelfSigned, PfxPath, PfxPassword}` · `Iso20022:{InitiatingPartyName, ValidateBeforeSend=true}` · `Bank:{Behaviour:{DelayMs,RejectRate,ErrorRate}, TrustedThumbprints}`.

Batch size 20 keeps the Kafka-ack-holding transaction short, per ADR-001's own "batches must stay small". Advisory lock keys: `727001` for the API relay, `727002` for the worker relay. Kafka consumer groups: `bank-gateway-worker`, `bank-gateway-worker-retry`, `payments-api-status`.

## Known rework this ordering costs

Most potential rework was designed away by giving M0 and M1 the *full* shape — all `payments` columns, all event fields, `PaymentStatus.Failed`, and a business-shaped `IBankClient` — so later milestones add behaviour rather than migrations or contract changes. Two real costs remain, both accepted:

1. **`sent_messages.xml` churns twice.** Nullable in M2 (the stub has no XML), `NOT NULL` in M4.11. One extra EF migration, roughly twenty minutes. The alternative — omitting the column in M2 — is the same migration count but leaves M2's audit story incomplete and gives the "already submitted, skip" check nothing to show.
2. **`StubBankClient` and `/bank/v1/stub-submissions` are deleted in M4.** Roughly half a day of throwaway code, and the unavoidable price of reliability-first ordering. It is worth paying: it lets M2's retry and DLT tests inject timeouts and 500s deterministically with no XML in the picture, which makes those tests far easier to write and read than if they first had to construct a valid signed `pain.001`.

Placing the state machine in M2 (task 2.11) eliminated the third and worst cost — a two-milestone window in which the retry topic could reorder events while the API happily applied a stale `Submitted` over an `Accepted`, leaving README row 7 unclaimable.

## Verification

Per milestone: `dotnet build` (warnings are errors), `dotnet test`, and **`docker compose build`**. The last is not optional, because the Dockerfiles' selective `COPY` means `src/Messaging/` (M0) and `docs/xsd/` (M4) can compile locally and still fail inside the container.

End to end, after M4:

```bash
docker compose up -d --build
curl -X POST http://localhost:8080/payments \
  -H "Content-Type: application/json" -H "X-Api-Key: $KEY" \
  -H "Idempotency-Key: 3f9a2c1e-0000-0000-0000-000000000001" \
  -d '{"debtorName":"Acme Holdings SIA","debtorIban":"LV80BANK0000435195001",
       "creditorName":"Example SIA","creditorIban":"DE89370400440532013000",
       "amount":125.50,"currency":"EUR","remittanceInfo":"Invoice 2026-104",
       "requestedExecutionDate":"2026-10-05","endToEndId":"INV-2026-104"}'

curl -H "X-Api-Key: $KEY" http://localhost:8080/payments/{id}   # -> Accepted
curl http://localhost:8081/bank/v1/_messages                     # the signed pain.001 we sent
open http://localhost:18888                                      # one trace, four services
```

Replaying the same request returns `200` + `Idempotency-Replayed: true` with the same payment. Changing the amount under the same key returns `422`. Sending `"remittanceInfo":"FAIL:AC04"` ends in `Rejected` with reason code `AC04`.

After M5, `dotnet test --filter "Category=E2E"` passes and every README failure-scenario row links to a test that exists.
