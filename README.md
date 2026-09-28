# Mayordomo Engine

**English (canonical)** · [Español (México)](README.es-MX.md)

Mayordomo Engine is an **open-source-from-day-one**, MIT-licensed, reusable, deterministic, server-authoritative board-game engine.

The repository is temporarily private only while the initial public-safety bootstrap is completed. Its code, governance, contribution model, and history are maintained as open-source/public-safe from inception.

**Mayordomo — El juego que no es un juego** is the first real commercial product used to prove the engine, but the engine does not know that Mayordomo exists.

> Product repositories know the engine. The engine never knows its products.

## Status

**Pre-alpha / active engine foundation.**

## Why this engine exists

The engine was born from a concrete problem: digitize a real board game without turning its rules, clients, content, networking, and authoring tools into one inseparable application.

That constraint produced a stronger goal:

> Build a board-game engine so small, fast, deterministic, and easy to consume that another developer can adopt it without adopting our product.

Mayordomo proves the engine against a demanding real game. Future original or properly licensed games should be able to consume the same reusable core without teaching the core their brand vocabulary.

## Open-source promise

This repository is MIT-licensed from inception.

Open source here means more than exposing source code later:

- public-safe Git history from the first engineering increments;
- contributor governance and DCO;
- reproducible build/test contracts;
- documented architecture decisions;
- no proprietary Mayordomo content hidden in old commits;
- no reverse dependency on the private product;
- measurable footprint/performance budgets.

See [LICENSE](LICENSE), [NOTICE.md](NOTICE.md), and [CONTRIBUTING.md](CONTRIBUTING.md).

## Repository split

```text
PRIVATE PRODUCT
ChicoDotNet/Mayordomo.Game
  ├── Mayordomo ruleset and product logic
  ├── official/authorized content and brand assets
  ├── Mayordomo Studio
  ├── React web client
  ├── Unity client
  ├── Avalonia desktop client
  └── commercial integrations
                │
                │ consumes
                ▼
OPEN-SOURCE ENGINE
ChicoDotNet/Mayordomo
  ├── deterministic game execution
  ├── turn/phase/state primitives
  ├── legal-action contracts
  ├── deterministic randomness
  ├── command/event/replay primitives
  ├── simulation and invariant tooling
  └── reusable public contracts
```

The dependency is intentionally one-way.

`Mayordomo.Game → Mayordomo Engine` is allowed.

`Mayordomo Engine → Mayordomo.Game` is prohibited.

See [ADR 0001](docs/architecture/0001-public-engine-private-game-boundary.md).

## Small and fast by contract

"Small and fast" is an engineering budget, not a slogan.

Initial Core budgets:

- **design target:** `Mayordomo.Core.dll ≤ 256 KiB` in Release;
- **hard CI ceiling:** `≤ 512 KiB`;
- **mandatory third-party runtime dependencies in Core:** **0**;
- headless execution is mandatory;
- performance baselines become release gates once the first meaningful command/state walking skeleton exists;
- published benchmarks must identify runtime, machine/runner, engine SHA, scenario, throughput, latency, and allocations.

A larger feature is not automatically better if it makes every game carry weight it does not use.

See [Performance and footprint](docs/performance/README.md) and [ADR 0002](docs/architecture/0002-performance-and-footprint-budgets.md).

## Architectural north star

The engine must remain:

- deterministic;
- replayable;
- server-authoritative;
- headless-testable;
- tiny enough to embed comfortably;
- dependency-light;
- independent from UI technology;
- independent from persistence/cloud vendors;
- independent from branded game content;
- useful to a second properly licensed game without changing the engine merely to teach it brand vocabulary.

Read the [clean-room board-game engine architecture](docs/architecture/clean-room-board-game-engine.md).

## Current executable foundation

```text
Mayordomo.slnx
├── src/
│   └── Mayordomo.Core/
└── tests/
    └── Mayordomo.Core.Tests/
```

The current project name may evolve as real slices justify stronger module boundaries. Do not perform speculative mass renames solely to match a diagram.

## Build and test

The .NET SDK is pinned in `global.json`.

```bash
dotnet restore Mayordomo.slnx
dotnet build Mayordomo.slnx --configuration Release --no-restore
dotnet test Mayordomo.slnx --configuration Release --no-build
node scripts/check-engine-budget.mjs
node scripts/check-public-readiness.mjs
```

See [Development Guide](docs/DEVELOPMENT.md).

## TDD

New behavior follows:

`RED → GREEN → REFACTOR → CERTIFY`

Compatibility work follows:

`PRESERVE → REPLAY → DIVERGE → EXPLAIN → FIX → VERIFY → REPEAT → PROMOTE`

The test judges the implementation. Expected behavior is never rewritten merely to make a failing implementation pass.

## Delivery model

```text
working branch
    ↓ squash
   dev
    ↓ squash
   main
    ↓ regular content-neutral merge
   dev
```

Working branches use `features/*`, `bugs/*`, `releases/*`, `hotfixes/*`, or `tags/*` and are retained by default as detailed delivery history.

Read [CONTRIBUTING.md](CONTRIBUTING.md) and [AGENTS.md](AGENTS.md) before changing behavior.

## Public/private boundary

This repository may contain reusable engine source, public contracts, generic deterministic tooling, synthetic fixtures, tests, simulations, CI, benchmarks, and redistribution-safe documentation.

It must not contain private `Mayordomo.Game` rules implementation, Studio code, official/private cards, licensed brand assets, secrets, or private player/customer data.

See [NOTICE.md](NOTICE.md).

## Roadmap

The engine roadmap is driven by real product pressure:

1. deterministic walking skeleton;
2. command/event transition and explicit revisions;
3. turn/phase model and legal actions;
4. deterministic RNG and replay;
5. headless simulation/invariants;
6. application/protocol boundary required by `Mayordomo.Game`;
7. measurable size/performance baselines;
8. package/release hardening.

See [Roadmap](docs/roadmap/README.md).

## Project site

The GitHub Pages site lives in `docs/site` and tells the engine's story, architecture boundary, adoption philosophy, and measured footprint/performance.

## Contributing

Contributions use DCO sign-off and must preserve determinism, clean-room provenance, TDD evidence, the one-way product dependency, small-footprint budgets, and public-safe history.

See:

- [CONTRIBUTING.md](CONTRIBUTING.md)
- [GOVERNANCE.md](GOVERNANCE.md)
- [DCO.md](DCO.md)
- [SECURITY.md](SECURITY.md)
- [SUPPORT.md](SUPPORT.md)
- [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)

## Licensing

Repository software and documentation authored for this project are MIT-licensed unless a file explicitly says otherwise.

The license does not grant rights to the Mayordomo product identity or any third-party game, trademark, character, artwork, audio, card text, board artwork, or licensed content.

See [LICENSE](LICENSE) and [NOTICE.md](NOTICE.md).
