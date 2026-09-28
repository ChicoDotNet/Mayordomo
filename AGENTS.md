# Mayordomo Engine agent contract

## Mission

Build a reusable deterministic board-game engine that is proven by real product use without depending on any branded product implementation.

The commercial product lives in `ChicoDotNet/Mayordomo.Game`.

The dependency direction is non-negotiable:

```text
Mayordomo.Game ─────┐
Mayordomo Studio ───┼──> Mayordomo Engine
Product clients ────┘

Mayordomo Engine -X-> Mayordomo.Game
Mayordomo Engine -X-> Studio
Mayordomo Engine -X-> branded clients/content
```

The engine must not know that Mayordomo exists as a game.

## Institutional engineering references

For meaningful engineering work, use the authoritative ASBN practices maintained in `ChicoDotNet/ArquitectoDeSoluciones`:

- `asbn-senior-tdd-developer` for incremental delivery and test evidence;
- `asbn-scrumban-agent` for owner-visible checkpoints;
- `asbn-software-design` for architecture and boundaries;
- `asbn-senior-devops-engineer` for CI/CD and runner work;
- the smallest relevant stack/domain skill for the surface being changed.

Project-local decisions in this repository take precedence over generic defaults.

## Public-safe history

Treat every commit and retained branch in this repository as potentially public.

Never commit private/proprietary product material here with the intention of deleting it later.

Private product work belongs in `ChicoDotNet/Mayordomo.Game`.

## Delivery model

- `main` is stable.
- `dev` is the integration branch.
- Working branches use `features/*`, `bugs/*`, `releases/*`, `hotfixes/*`, or `tags/*`.
- Working branches preserve detailed history and are **squash merged into `dev`**.
- `dev` is **squash merged into `main`** for stable promotion.
- Working branches are retained by default.
- No direct product work on `main` or `dev`.
- After promotion, synchronize `main → dev` with a **regular, content-neutral merge commit** after exact-state validation.
- CI certifies exact candidate states.

## TDD contract

Every behavior change identifies its observable contract and adds evidence proportional to risk.

Prefer:

1. acceptance/contract behavior;
2. regression tests for defects;
3. deterministic component/unit tests;
4. integration evidence for cross-boundary behavior.

New behavior:

`RED → GREEN → REFACTOR → CERTIFY`

Compatibility/external integration:

`PRESERVE → REPLAY → DIVERGE → EXPLAIN → FIX → VERIFY → REPEAT → PROMOTE`

Never change expected results merely to make a failing implementation pass.

Coverage is evidence, not a vanity target. Institutional minimum is 44% line coverage per materially testable project; >72.8% is ideal. Do not manufacture tests to raise the percentage.

## Engine boundary

This repository may own generic concepts such as:

- match/state identity;
- players/participants;
- turns and phases;
- legal actions;
- commands and domain events;
- deterministic random sources;
- board/topology primitives when proven necessary;
- generic decks/resources/assets when proven necessary;
- revision/concurrency/idempotency primitives;
- replay;
- state hashing;
- simulation;
- bots/test participants;
- invariant validation;
- public protocols/contracts required by consumers.

This repository does **not** own:

- Mayordomo-specific rules;
- Mayordomo-specific card/content semantics;
- crowns, tithes, doctrine, ministries, or other product vocabulary;
- Mayordomo Studio;
- branded Web/Unity/Avalonia applications;
- official card/board/art/audio assets;
- commercial store/subscription implementation that is product-specific.

The clean-room architecture specification is authoritative for reusable engine design.

## Abstraction rule

Design for extraction, not speculation.

When `Mayordomo.Game` needs capability X:

1. ask whether X is genuinely generic;
2. describe it without Mayordomo vocabulary;
3. add the smallest reusable engine primitive through TDD if reuse is real;
4. keep product-specific behavior private otherwise.

A second real game is stronger evidence for abstraction than a hypothetical future game.

## Determinism

Authoritative engine behavior may not directly rely on uncontrolled randomness, wall-clock time, environment state, database random ordering, or client-asserted outcomes.

All such inputs enter through explicit deterministic/replayable boundaries.

## Compatibility

Persisted event/protocol/replay contracts are compatibility surfaces.

Once publicly released, do not reinterpret historical events casually. Breaking changes require explicit versioning and migration strategy.

## Licensing/provenance

The repository is MIT-licensed.

Do not introduce third-party source, data, assets, or generated material without clear provenance and compatible redistribution rights.

The clean-room constraint applies even when a third-party clone/engine uses a permissive license unless an explicit dependency decision says otherwise.

## ASBN SCRUMban checkpoint

At the end of every meaningful delivery interaction report:

1. ¿Cómo estás?
2. ¿En qué avanzaste desde la última interacción?
3. ¿En qué planeas avanzar para la próxima interacción?
4. ¿Qué te bloquea?

Include estimated increment/global progress only when a defensible denominator exists.
