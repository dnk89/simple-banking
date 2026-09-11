# ISO 20022 schemas

This directory holds the XSDs used to validate generated/received messages, per [ADR-003](../docs/adr/003-iso20022-adapter.md):

- `pain.001.001.13` (Customer Credit Transfer Initiation) — validated before sending.
- `pain.002.001.15` (Customer Payment Status Report) — validated on receipt.

Not included in this repository. Download the official schemas from the ISO 20022 Message Definition Reports at https://www.iso20022.org and place them here before wiring up XSD validation in `BankGateway.Worker`.
