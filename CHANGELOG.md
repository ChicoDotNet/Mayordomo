# Changelog

All notable changes to Mayordomo Engine are documented here.

## 1.0 release train

This is the first supported stable package line for `Mayordomo.Core`.
Concrete releases use the version form `1.0.YYYYMMDD.build`.

### Added

- deterministic, replayable, server-authoritative match execution;
- turn, phase, participant position, relocation, held-item and deterministic deck primitives;
- deterministic random generation and replay validation;
- command idempotency with conflict detection;
- formal match invariants, deterministic simulation, soak/property/adversarial fuzz suites;
- reproducible benchmarks with hard performance ceilings;
- canonical state-hash format v2, including the processed-command idempotency ledger;
- NuGet packaging with portable symbols, Source Link metadata and clean consumer smoke tests;
- NuGet artifact auditing for package identity, MIT metadata, source SHA, contents and zero runtime dependencies;
- Trusted Publishing through GitHub OIDC with no long-lived NuGet API key;
- immutable Git tag and GitHub Release finalization from the exact successfully published source.

### Compatibility

- public package id: `Mayordomo.Core`;
- assembly compatibility line: `1.0.0.0`;
- canonical state-hash format v2;
- frozen 1.0 public API SHA-256: `20B4806BBD144D2B317E701BB8EF3FED656AE9E731E1D48414D53298F199E70A`;
- golden replay compatibility fixture is part of release certification.

### Release integrity

Every supported release is built from stable `main`, certified before credential acquisition, published through Trusted Publishing, and finalized with package/symbol artifacts, SHA-256 package checksums, benchmark evidence and a machine-readable release manifest.
