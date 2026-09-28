# 0002 — Performance and footprint budgets

**Status:** Accepted

## Context

Mayordomo Engine is intended to be adopted by developers who should not have to accept a large dependency graph or heavyweight runtime cost merely to obtain deterministic board-game primitives.

"Small and fast" must therefore be an architectural constraint with measurable evidence.

## Decision

The core engine follows explicit budgets.

### Core assembly footprint

For `Mayordomo.Core.dll` in Release/net10.0:

- design target: **≤ 256 KiB**;
- hard CI ceiling: **≤ 512 KiB**.

The hard ceiling may only be changed through an explicit architecture decision that explains the product value gained and alternatives considered.

### Runtime dependencies

`Mayordomo.Core` has **zero mandatory third-party runtime package dependencies**.

Test, benchmark, analyzer, source-generation, tooling, or build-only packages may exist outside the runtime Core when justified.

### Performance

Performance claims must be reproducible.

When the first meaningful deterministic walking skeleton exists, establish a benchmark suite that records:

- engine SHA/version;
- runtime/SDK;
- OS/architecture;
- CPU/machine or GitHub runner class;
- scenario;
- operations/second;
- latency distribution where meaningful;
- allocated bytes/op;
- engine assembly/package size.

Once a stable baseline exists, regressions beyond an agreed tolerance require explicit review.

### Headless execution

Core engine behavior must execute without:

- browser;
- Unity;
- Avalonia;
- network;
- database;
- graphics;
- animation.

### Dependency philosophy

Prefer BCL/runtime capabilities over adding a package for small convenience.

A dependency is justified by net value, not popularity.

## Consequences

### Positive

- low adoption friction;
- fast restore/build/startup paths;
- easier embedding in multiple products;
- smaller attack/dependency surface;
- clearer benchmarks;
- stronger pressure toward focused abstractions.

### Trade-off

Some conveniences may be implemented locally or placed in optional companion packages rather than increasing Core weight for every consumer.

## Enforcement

CI runs `scripts/check-engine-budget.mjs` after Release build.

The project site publishes current footprint metrics and, once meaningful benchmarks exist, performance evidence.
