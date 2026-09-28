# Governance

Mayordomo Engine currently uses a **founding maintainer model** while reusable engine contracts are being established through the first real commercial product.

## Principles

Governance optimizes for:

- evidence over status;
- deterministic, testable behavior;
- public-safe repository history;
- explicit IP/provenance boundaries;
- reversible increments;
- stable versioned contracts;
- contributor stewardship;
- portability;
- avoidance of speculative abstraction.

## Current model

The repository owner acts as founding maintainer and final steward of merge, release, licensing, and public/private-boundary decisions during foundation.

## Material decisions

The following normally require explicit maintainer approval and an ADR/design record:

- public engine contracts;
- deterministic/replay compatibility;
- persisted event compatibility;
- versioning;
- public protocol compatibility;
- licensing or repository boundary;
- authentication/authorization contracts;
- new runtime dependencies with broad impact;
- Rust/native boundaries;
- breaking changes.

## Product authority

The engine does not decide Mayordomo doctrine or invent official Mayordomo rules.

Those belong to the private product and its authorized sources.

## Open-source path

This repository may become public after real Mayordomo development proves the engine useful and a release review confirms that the reachable history is safe to disclose.

Public release does not require publication of `Mayordomo.Game`, Studio, official card corpus, licensed assets, or commercial integrations.

## No purchased authority

Employment, sponsorship, contracting, licensing, or procurement do not automatically grant technical governance authority.

## Path to broader governance

Sustained external contributors, multiple maintainers, independent adopters, ecosystem dependencies, or a neutral-stewardship need should trigger a governance review.

## Amendments

Governance changes require a pull request describing the problem, effect, compatibility impact, and transition.
