# Contributing to Mayordomo Engine

Thank you for contributing.

Mayordomo Engine is MIT-licensed open-source software from inception. Repository visibility may remain private briefly during bootstrap, but every contribution must be safe for public distribution together with its Git history.

## Read first

Before writing code, read:

1. [README.md](README.md)
2. [AGENTS.md](AGENTS.md)
3. [Development Guide](docs/DEVELOPMENT.md)
4. [Clean-room engine architecture](docs/architecture/clean-room-board-game-engine.md)
5. [Public engine / private game boundary](docs/architecture/0001-public-engine-private-game-boundary.md)

## Public-safe rule

Do not commit proprietary or confidential `Mayordomo.Game` material here, even temporarily.

That includes official/private cards, proprietary rule implementation intended to remain closed, artwork, audio, licensed text, commercial secrets, credentials, or private player/customer data.

Use deterministic synthetic fixtures for engine tests.

## Canonical language

Code, identifiers, tests, schemas, ADRs, and canonical engineering documentation are English.

## Technology policy

- C# / .NET is the default authoritative engine technology.
- Authoritative behavior must be deterministic and headless-testable.
- UI, transport, cloud, and persistence technologies must not leak into engine rules.
- Rust/.NET experiments prefer FerrumWeave only after a justified boundary exists.
- Branded Web/Unity/Avalonia clients and Mayordomo Studio belong in the private product repository.\n- Engine Core changes must respect the documented size/dependency budgets; benchmark-sensitive changes must include proportional performance evidence.

## TDD

New behavior follows:

`RED → GREEN → REFACTOR → CERTIFY`

Compatibility work follows:

`PRESERVE → REPLAY → DIVERGE → EXPLAIN → FIX → VERIFY → REPEAT → PROMOTE`

Expected results are not rewritten merely to match a failing implementation.

Coverage is evidence, not a vanity target.

## Branch flow

Normal work starts from current `dev`.

Allowed working branch prefixes:

- `features/`
- `bugs/`
- `releases/`
- `hotfixes/`
- `tags/`

Delivery:

1. working branch from `dev`;
2. preserve detailed RED/GREEN/refactor history;
3. PR to `dev`;
4. certify the exact candidate SHA;
5. squash working branch → `dev`;
6. deliberate stable promotion via squash `dev → main`;
7. regular content-neutral `main → dev` ancestry synchronization after exact validation;
8. retain working branches by default.

## Pull requests

Explain:

- bounded outcome and non-goals;
- observable contract;
- architecture layer;
- tests/evidence actually executed;
- exact SHA when certification matters;
- determinism/replay impact;
- compatibility/versioning impact;
- public/private/provenance impact;
- known unknowns.

## Clean-room rule

Do not copy or structurally imitate third-party board-game clone/engine source code into this implementation.

Public standards, public platform documentation, general software-engineering concepts, and properly evaluated dependencies may be used.

## AI-assisted contributions

AI-assisted development is allowed. The contributor remains accountable for correctness, licensing, provenance, confidentiality, security, tests, and maintainability.

Material AI assistance should be disclosed in the PR when it affected implementation or evidence.

## DCO

Contributions use Developer Certificate of Origin 1.1 sign-off.

Use:

`git commit -s`

See [DCO.md](DCO.md).

## Community

Review the change, not the author. Strong technical disagreement is welcome; hostility and harassment are not.

See [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
