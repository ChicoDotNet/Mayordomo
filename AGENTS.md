# Mayordomo agent contract

## Mission

Build the digital Mayordomo platform while preserving official game behavior as explicit, versioned, testable contracts.

The product must separate the reusable board-game platform from Mayordomo-specific rules, content, artwork, and branding so future **properly licensed** game packs can reuse infrastructure without contaminating the Mayordomo ruleset.

Do not invent missing rules, card text, artwork, doctrine, or product claims. Unknowns remain explicit until an authorized canonical source resolves them.

## Institutional engineering references

For meaningful engineering work, use the authoritative ASBN practices maintained in `ChicoDotNet/ArquitectoDeSoluciones`:

- `asbn-senior-tdd-developer` for incremental product delivery and test evidence;
- `asbn-scrumban-agent` for owner-visible checkpoints;
- `asbn-software-design` for architecture and boundaries;
- `asbn-senior-devops-engineer` for CI/CD and runner work;
- `asbn-ux-cx-architect` for material UI/UX/CX/accessibility work;
- the smallest relevant stack skill for the surface being changed.

Project-local decisions in this repository take precedence over generic defaults.

## Delivery model

- `main` is stable and receives deliberate promotion from `dev`.
- `dev` is the integration branch.
- Working branches use `features/*`, `bugs/*`, `releases/*`, `hotfixes/*`, or `tags/*`.
- Working branches keep their internal history and are **squash merged into `dev`**.
- `dev` stays intentionally clean: one integration commit per accepted working-branch outcome.
- `dev` is **squash merged into `main`** so `main` stays intentionally clean.
- Working branches are retained by default as delivery history.
- No direct product work on `main` or `dev` after bootstrap.
- After each `dev → main` squash promotion, synchronize `main → dev` with a **regular, content-neutral merge commit** after exact-state validation.
- CI certifies an exact candidate SHA, not merely a branch name.

## TDD contract

Every behavior change must identify its observable contract and add validation proportional to risk.

Prefer:

1. acceptance/contract behavior;
2. regression tests for defects;
3. deterministic component/unit tests;
4. integration/E2E evidence for cross-boundary behavior.

For new behavior prefer:

`RED → GREEN → REFACTOR`

For compatibility or external-package integration prefer:

`PRESERVE → REPLAY → DIVERGE → EXPLAIN → FIX → VERIFY → REPEAT → PROMOTE`

Never change expected results merely to make a failing implementation pass.

Coverage is evidence, not a vanity target. The ASBN institutional minimum is 44% line coverage per materially testable project, with >72.8% ideal. Do not manufacture tests merely to raise the percentage.

## Product boundaries

The architecture distinguishes four concepts:

1. **Engine** — reusable turn, movement, state, event, rule-execution and multiplayer primitives.
2. **Ruleset** — versioned executable behavior for a game/edition.
3. **Content Pack** — cards, board definitions, trivia, localized text and other data.
4. **Brand/Game Pack** — licensed artwork, names, sound, presentation and commercial packaging.

Mayordomo is the first product and first canonical ruleset. Generic engine abstractions must not weaken or reinterpret Mayordomo behavior.

Future third-party game packs are out of scope until rights are secured. Their possible existence justifies clean boundaries, not speculative implementation.

## Source and content authority

- Official Mayordomo rules are canonical product inputs.
- Every ruleset must be versioned.
- Every production card/effect must have provenance to an authorized source.
- A reduced development corpus is allowed only when explicitly authorized or clearly marked as non-production fixture data.
- Do not silently copy older-edition wording into a newer ruleset.
- Game behavior and game content are separate concerns.
- Once a match starts, its ruleset/content version is immutable for that match.
- Configurable values such as salary belong in versioned game configuration rather than source-code constants unless the official rules require otherwise.

## Implementation language policy

- C# / .NET is the default authoritative implementation language.
- React UI code uses TypeScript.
- Unity uses C# with UI Toolkit/UXML/USS; do not describe Unity UI as XAML.
- Avalonia is the preferred XAML desktop surface for Windows, Linux, and macOS.
- Rust should enter through FerrumWeave when a justified .NET-compatible boundary exists and must not become a beta dependency until the required FerrumWeave capability is certified.

## Server authority

Multiplayer gameplay is server-authoritative.

Clients may present allowed actions and optimistic visual feedback, but cannot be the authority for:

- dice/random results;
- card draws;
- money;
- ownership;
- turn order;
- penalties;
- crowns;
- ruleset transitions;
- victory.

## ASBN SCRUMban checkpoint

At the end of every meaningful delivery interaction report:

1. ¿Cómo estás?
2. ¿En qué avanzaste desde la última interacción?
3. ¿En qué planeas avanzar para la próxima interacción?
4. ¿Qué te bloquea?

Include estimated increment and global-project progress when a defensible denominator exists.
