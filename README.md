# Mayordomo Engine

**English (canonical)** · [Español (México)](README.es-MX.md)

Mayordomo Engine is the incubation repository for a reusable, deterministic, server-authoritative board-game engine.

**Mayordomo — El juego que no es un juego** is the first real commercial product used to prove the engine, but the engine does not know that Mayordomo exists.

> Product repositories know the engine. The engine never knows its products.

## Status

**Pre-alpha / active engine foundation.**

The repository is private during incubation but is maintained as **public-safe history from inception** so it can later be made public without rewriting Git history.

The reusable software in this repository is licensed under the **MIT License**.

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
PUBLICABLE ENGINE
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

## Architectural north star

The engine must remain:

- deterministic;
- replayable;
- server-authoritative;
- headless-testable;
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

This repository may contain reusable engine source, public contracts, generic deterministic tooling, synthetic fixtures, tests, simulations, CI, and redistribution-safe documentation.

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
7. package/release hardening;
8. public release readiness after the engine proves itself in the commercial product.

See [Roadmap](docs/roadmap/README.md).

## Contributing

External contributions are not yet solicited while the repository is private, but the repository is prepared for future contributions.

Contributions use DCO sign-off and must preserve determinism, clean-room provenance, TDD evidence, the one-way product dependency, and public-safe history.

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
