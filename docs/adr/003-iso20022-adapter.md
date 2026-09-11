# ADR-003: Domain model in the API, ISO 20022 only in the bank adapter

**Status:** Accepted
**Date:** 2026-09-11

## Context

The bank communicates in ISO 20022 XML. Following the EPC SEPA Credit Transfer customer-to-bank implementation guidelines (2025), payments are initiated with `pain.001.001.13`, and rejections are reported with `pain.002.001.15`.

These messages contain many technical elements that have nothing to do with the business meaning of a payment: group headers, payment information blocks, message and instruction identifiers, service level and charge bearer codes. Banks also differ in the message versions and national variants they support.

If ISO 20022 leaks into the API contract or into events, every format or version change ripples through all services and all API clients.

## Decision

**The API accepts a simplified payment** with business fields only: debtor IBAN, creditor name, creditor IBAN, optional creditor BIC, amount, currency, remittance information, requested execution date and end-to-end ID.

**The domain model and all Kafka events carry business data only**, never XML.

**The bank gateway worker is the only component that knows ISO 20022.** Its adapter:

- maps a payment to `pain.001.001.13` and generates the technical fields: message ID, creation time, number of transactions, control sum, payment information ID, service level `SEPA` and charge bearer `SLEV`;
- uses the payment ID without hyphens (32 characters, within the 35-character limit) as the instruction ID;
- signs the XML and validates it against the official XSD before sending;
- stores the sent message identifiers and XML in its own database schema, for correlation and audit;
- publishes `PaymentSubmitted` once the bank has received the file;
- parses `pain.002` reports, finds the payment by original message ID and original instruction ID, and maps statuses to business events.

The EPC guidelines define `pain.002` for rejections. Many banks also report positive statuses, so the mock bank sends both.

| ISO 20022 status | Meaning | Business event |
|---|---|---|
| `ACTC` | Technical validation passed | None (payment stays `Submitted`) |
| `PDNG` | Pending | None |
| `ACCP`, `ACSP`, `ACSC` | Accepted, settlement in process, or settlement completed | `PaymentAccepted` |
| `RJCT` | Rejected | `PaymentRejected`, with the reason code |

**The API validates the format's constraints at the edge.** A payment that could never become a valid `pain.001` is rejected immediately with `400`, instead of failing later and asynchronously. The API checks that:

- the amount is greater than zero, with at most two decimal places;
- the currency is EUR;
- both IBANs have valid checksums;
- names are at most 70 characters;
- remittance information is at most 140 characters;
- the end-to-end ID is at most 35 characters;
- all text uses only the SEPA character set.

## Alternatives considered

- **Accept `pain.001` XML directly in the API:** realistic for corporate-to-bank gateways, but it pushes format complexity onto every client and ties the public contract to one message version.
- **Generate XML in the API and publish it in events:** couples every event consumer to the bank format and puts large XML payloads into Kafka.
- **ISO 20022-shaped JSON as the API model:** looks simpler, but still ties the contract to the structure and version of one message.

## Consequences

**Positive**

- The API contract stays stable when bank formats or versions change.
- Supporting a new format or bank means adding an adapter, not changing every service.
- Domain logic can be tested without XML.

**Negative**

- **Validation rules exist in two places.** The API checks format limits for fast feedback, and the adapter validates against the XSD as a safety net. If a future adapter has stricter limits, the API validation must be revisited.
- **The mapping layer must be maintained and tested**, including XSD validation in unit tests.
- **Some rejections can only come from the bank**, such as a closed creditor account, and still arrive asynchronously.
