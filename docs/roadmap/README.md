# Engine roadmap

This roadmap is intentionally capability-oriented. Mayordomo.Game supplies real product pressure; the engine does not pre-build hypothetical features.

## Foundation — current

- repository governance;
- MIT licensing;
- public-safe history policy;
- clean-room architecture;
- TDD/branch workflow;
- public/private repository split;
- repository-policy CI.

## Engine walking skeleton

Prove through tests:

- match identity/state revision;
- participants;
- explicit command;
- explicit event;
- controlled state transition;
- legal action query;
- deterministic random source;
- replayable history;
- deterministic equivalence.

## Product-driven primitives

Add only primitives demanded by real Mayordomo.Game slices, keeping product vocabulary private.

Expected candidates include:

- turns/phases;
- board movement/topology;
- decks;
- resources/value transfer;
- choices;
- ownership;
- effect execution boundaries;
- timeouts/game clock;
- player projections.

Candidates are not commitments until a real slice needs them.

## Reliability

- command idempotency;
- optimistic concurrency;
- invariant validator;
- deterministic state hash;
- golden replay tests;
- headless simulation;
- simple bot participants;
- fuzz/property-based tests where they add signal.

## Integration surface

- stable public contracts required by Mayordomo.Game;
- package/version strategy;
- compatibility policy;
- application/persistence ports only when required;
- real-time/server host adapters without contaminating the engine.

## Public-visibility readiness

Before changing GitHub repository visibility from private bootstrap to public:

- public-readiness CI green;
- all reachable branches/history reviewed;
- no proprietary content;
- dependency/license review complete;
- API documentation sufficient for an independent consumer;
- package/release workflow proven;
- security and contribution paths verified;
- at least one real private product consuming the engine successfully.

## Public evolution

Let independent consumers and a second real game provide evidence for further abstractions.
