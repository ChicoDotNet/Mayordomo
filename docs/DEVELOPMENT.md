# Development Guide

## Audience

This guide is for a developer or coding agent joining Mayordomo Engine without a live handoff.

## Repository role

`ChicoDotNet/Mayordomo` owns the reusable engine.

`ChicoDotNet/Mayordomo.Game` owns the private commercial product: Mayordomo rules/content, Studio, branded clients, licensed assets, and commercial integrations.

The private product consumes the engine. The engine never references the private product.

## First principles

1. Keep this repository public-safe.
2. Keep the engine product-agnostic.
3. Let real product pressure drive abstractions.
4. Keep authoritative behavior deterministic and headless-testable.
5. Begin new behavior with an observable contract.
6. Never invent product rules here.
7. Work from `dev` through a retained working branch.

## Prerequisites

- Git
- .NET SDK pinned by `global.json`
- Node.js available for repository-policy scripts (GitHub-hosted runners already provide it)

## Clone and validate

```bash
git clone https://github.com/ChicoDotNet/Mayordomo.git
cd Mayordomo
git checkout dev

dotnet restore Mayordomo.slnx
dotnet build Mayordomo.slnx --configuration Release --no-restore
dotnet test Mayordomo.slnx --configuration Release --no-build
node scripts/check-public-readiness.mjs
```

Do not report a gate as passing unless it was actually executed or exact CI evidence exists.

## Working branch

```bash
git checkout dev
git pull
git checkout -b features/<english-kebab-description>
```

Allowed prefixes:

- `features/`
- `bugs/`
- `releases/`
- `hotfixes/`
- `tags/`

## TDD workflow

For new behavior:

1. identify observable behavior;
2. write the smallest meaningful failing contract;
3. capture RED evidence;
4. implement the smallest correct behavior;
5. obtain GREEN;
6. refactor without changing behavior;
7. run proportional tests/invariants;
8. certify the exact candidate state;
9. document compatibility impact.

Never rewrite an expected result merely to match a failing implementation.

## Determinism

Authoritative engine code must not directly depend on uncontrolled:

- `Random.Shared`;
- wall-clock time;
- random GUID generation;
- environment/process state;
- database random ordering;
- client-asserted outcomes.

Inject or record deterministic context.

The engine must remain runnable without browser, Unity, network, database, or animation.

## Placement test

Place a behavior in this repository only when it can be accurately described without Mayordomo product vocabulary.

Examples:

- generic turn/phase primitive → engine;
- generic deterministic deck primitive → engine when real product need proves it;
- command/revision/idempotency → engine/application contract;
- Mayordomo tithe rule → private product;
- Mayordomo prison amount → private product;
- Mayordomo card effect → private product;
- Studio workflow → private product;
- branded client animation → private product.

## Cross-repository flow

When `Mayordomo.Game` discovers a missing reusable capability:

1. define the product need privately;
2. express the smallest generic observable engine contract;
3. implement that contract here through TDD;
4. release/reference the engine candidate;
5. consume it from the private product;
6. keep product semantics in the private repository.

Do not create an engine abstraction merely because a hypothetical future game might need it.

## Documentation

Update an ADR/specification when changing:

- public engine contracts;
- replay/determinism;
- persistence/event compatibility;
- versioning;
- public protocol shape;
- public/private boundary;
- licensing/provenance;
- major dependency policy.

## Merge flow

Working branch → `dev`: squash.

`dev` → `main`: squash after deliberate stable validation.

After promotion, regular merge `main → dev` only after proving the synchronization is content-neutral.

Working branches remain by default.
