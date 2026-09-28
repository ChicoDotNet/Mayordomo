# Clean-Room Board-Game Engine Architecture

## Status and authority

This document defines the target architecture for the reusable board-game platform behind Mayordomo.

It is intentionally implementation-independent. It describes observable behavior, invariants, boundaries, contracts, tests, and delivery sequencing without depending on the source code, internal names, tests, schemas, assets, or directory structures of third-party board-game implementations.

This document is subordinate to \`AGENTS.md\`. If the two ever conflict, \`AGENTS.md\` is authoritative until an explicit repository decision updates one or both.

The goal is not to turn Mayordomo into a generic framework before Mayordomo itself works. The goal is to make Mayordomo the **first real game implemented on a reusable engine** whose boundaries are strong enough to support future original or properly licensed game packs.

---

## 1. Clean-room engineering constraint

### Allowed inputs

Implementation may use:

- this repository and its own history;
- official or otherwise authorized Mayordomo rules and content;
- generally known software-engineering concepts;
- standards and public platform documentation;
- project-local ADRs and technical specifications;
- institutional engineering guidance from \`ChicoDotNet/ArquitectoDeSoluciones\`.

Generally known concepts include, for example:

- turn-based state machines;
- command processing;
- domain events;
- reducers/state transitions;
- deterministic simulation;
- seeded pseudo-random generators;
- authoritative multiplayer servers;
- optimistic concurrency;
- idempotency;
- event logs;
- snapshots;
- replay;
- ports/adapters;
- modular monoliths;
- TDD;
- property-based testing;
- WebSockets/SignalR;
- content-driven configuration.

### Prohibited implementation inputs

For this work, do not consult, copy, translate, adapt, or structurally imitate third-party board-game engine or clone source code, including:

- source files;
- tests;
- internal class/interface names;
- algorithms as implemented;
- schemas;
- API shapes;
- directory layouts;
- board JSON;
- asset packs;
- comments;
- implementation-specific documentation.

A permissive third-party software license does not change this project-local engineering constraint.

If a future dependency is proposed for direct incorporation, evaluate it separately for:

1. product fit;
2. license compatibility;
3. provenance;
4. attribution requirements;
5. security;
6. long-term maintenance.

This clean-room requirement is an engineering policy for independent implementation. It is not a legal conclusion about the protectability of any particular game mechanic.

---

## 2. Architectural north star

The platform must preserve this dependency direction:

~~~text
Mayordomo-specific rules/content/presentation
                    |
                    v
         Reusable board-game engine
                    |
                    v
      Infrastructure and host adapters
~~~

The engine **must not know that Mayordomo exists**.

Mayordomo may depend on reusable engine contracts. The reusable engine must never depend on Mayordomo-specific names, crowns, cards, doctrine, text, artwork, or rule concepts.

The project distinguishes four product concepts already established by \`AGENTS.md\`:

1. **Engine** — reusable state, turn, movement, command, event, random, rule-execution, simulation, and multiplayer primitives.
2. **Ruleset** — versioned executable behavior for a game/edition.
3. **Content Pack** — authorized board data, cards, localized text, trivia, and related content.
4. **Brand/Game Pack** — names, artwork, sound, presentation, packaging, and other licensed brand material.

A future properly licensed game should be able to provide its own ruleset, content, and brand pack without modifying the engine merely to teach it brand-specific vocabulary.

---

## 3. Fundamental execution model

The central model is:

~~~text
Current state
+ Command
+ Deterministic execution context
---------------------------------
Domain events
+ Next state
~~~

Conceptually:

~~~text
Events = Execute(CurrentState, Command, Context)
NextState = Reduce(CurrentState, Events)
~~~

The exact code shape is not prescribed. The behavioral contract is.

### Required properties

A command:

1. identifies the match;
2. identifies the actor when applicable;
3. carries a unique command identifier;
4. declares the state revision it expects;
5. is authenticated and authorized before domain execution;
6. is validated against the current state;
7. is validated against current game rules;
8. produces zero or more explicit domain events;
9. causes state transition only through an explicit controlled path;
10. is idempotent at the application boundary.

Arbitrary state mutation from UI, transport, persistence, or infrastructure code is prohibited.

---

## 4. Determinism is a first-class invariant

A match must be reproducible.

Given the same:

~~~text
Engine version
+ Ruleset version
+ Content version
+ Initial configuration
+ Initial seed
+ Ordered command stream
================================
Same ordered domain events
+ Same final state
+ Same deterministic state hash
~~~

No authoritative domain behavior may directly depend on uncontrolled sources such as:

~~~csharp
Random.Shared
DateTime.Now
DateTimeOffset.Now
Guid.NewGuid()
~~~

or equivalent process/environment entropy.

All non-deterministic inputs must enter through explicit abstractions or pre-generated values recorded in the authoritative history.

Typical boundaries include:

- game random source;
- game clock;
- identifier source when generated during execution;
- external decision input;
- timeout/expiry input.

Tests must be able to substitute deterministic implementations.

---

## 5. Replay is a product capability, not a debugging afterthought

Every authoritative match should eventually be reconstructible from recorded inputs.

Minimum replay identity:

~~~text
GameId
EngineVersion
RulesetVersion
ContentVersion
InitialConfiguration
InitialSeed
OrderedCommands and/or authoritative event history
~~~

The implementation must support a deterministic replay operation that can prove equivalence between the original execution and reconstructed execution.

At appropriate maturity, replay verification should include a deterministic state hash:

~~~text
hash(replayed final state) == hash(original final state)
~~~

Replay enables:

- regression reproduction;
- support;
- dispute investigation;
- audit;
- deterministic bug reports;
- golden-match tests;
- analytics;
- simulations;
- future bot evaluation;
- historical inspection.

Historical events are contracts. Once persisted in production, they must not be casually reinterpreted by later code.

---

## 6. Core domain vocabulary

The exact type names may evolve, but the architecture must represent concepts equivalent to:

- Game/Match identity;
- Game definition;
- Game configuration;
- Ruleset identity/version;
- Content identity/version;
- Player identity;
- Player state/status;
- Turn;
- Turn order;
- Phase;
- Command;
- Domain event;
- Legal action;
- Board;
- Board location/space/node;
- Token/piece;
- Position;
- Deck;
- Card;
- Discard area;
- Resource;
- Currency/value;
- Asset;
- Ownership;
- Transaction/ledger entry;
- Rule condition;
- Rule effect;
- Game result;
- deterministic seed;
- game clock;
- state revision.

Not every game uses every concept.

The engine should not require cards, money, a board, dice, trading, or hidden information merely because Mayordomo uses some of them.

Prefer optional capabilities and composition over a universal inheritance hierarchy.

---

## 7. Game definition contract

A game/ruleset must be able to provide behavior equivalent to:

~~~text
CreateInitialState(setup)
Execute(state, command, deterministicContext)
GetLegalActions(state, player)
ProjectState(state, viewer)
EvaluateCompletion(state)
~~~

This is a conceptual contract, not a prescribed interface signature.

A game definition should own or reference:

- player-count constraints;
- initial setup rules;
- board definition;
- turn/phase model;
- game-specific commands;
- game-specific events;
- rule behavior;
- victory/termination conditions;
- optional capabilities;
- associated content version.

The engine should provide reusable machinery without becoming an omniscient rule language.

---

## 8. Generic behavior versus Mayordomo behavior

The engine may understand generic concepts such as:

- advance a phase;
- begin/end a turn;
- move a token;
- draw/discard a card;
- transfer a resource;
- record ownership;
- generate deterministic random values;
- publish a domain event;
- evaluate legal actions;
- reconstruct state.

It must not understand Mayordomo-specific concepts such as:

- named crowns;
- Mayordomo-specific prison semantics;
- tithe-specific doctrine;
- Mayordomo card wording;
- Mayordomo victory semantics;
- named Mayordomo spaces.

Those belong in the Mayordomo ruleset/content layers.

A useful test for placement is:

> Can the concept be described accurately without using Mayordomo vocabulary?

If not, it probably does not belong in the reusable engine.

---

## 9. Rules: explicit code before speculative DSLs

Do not begin by inventing a universal declarative language for every possible board game.

Initially prefer clear, typed, testable domain code.

The architecture should nevertheless allow rule concepts equivalent to:

~~~text
Trigger
Condition
Effect
Priority
Validation
~~~

A future declarative rule layer may emerge when repeated real use cases justify it.

Until then:

- clarity beats cleverness;
- explicit domain behavior beats meta-programming;
- testability beats minimizing lines of code.

---

## 10. Turn and phase state machine

Do not model a turn as only "current player".

A turn may contain explicit phases, for example:

~~~text
Start
Action
Resolution
Optional actions
End
~~~

A specific game may instead require:

- simultaneous decisions;
- reactions;
- interrupts;
- skipped turns;
- extra turns;
- nested phases;
- actions outside the nominal current player's turn.

Therefore the architecture must expose explicit phase/state transitions rather than hard-coding a single \`Roll -> Move -> End\` flow.

Every command must be checked against the current phase.

---

## 11. Legal actions are authoritative output

The authoritative application must be able to answer:

> What may this player do now?

through a concept equivalent to:

~~~text
GetLegalActions(state, playerId)
~~~

The UI should primarily render possibilities from this result.

The UI must not independently recreate authoritative rules such as:

~~~typescript
if (money >= cost && square.type === "property") {
  showBuyButton();
}
~~~

Prefer:

~~~typescript
if (legalActions contains "BuyAsset") {
  showBuyButton();
}
~~~

Client-side checks may improve UX, but they do not replace server-side rule validation.

---

## 12. State revision and optimistic concurrency

Every authoritative match state must have a monotonic revision.

Example:

~~~text
Revision 127
Revision 128
Revision 129
~~~

Commands should declare the revision they expect.

If a client submits a command based on revision 127 while the server is already at 128, the server must explicitly reject or reconcile according to a defined policy.

Silent mutation against stale state is prohibited.

The normal safe default is rejection with enough information for the caller to refresh/reconcile.

---

## 13. Command idempotency

Every command must have a stable unique identifier.

If transport retries send the same command more than once, the application must not execute the domain action multiple times.

This is especially important for:

- payments;
- purchases;
- trades;
- movement;
- card use;
- penalties;
- end-turn actions.

The authoritative application should return the already-known outcome for a previously accepted command when feasible.

---

## 14. Domain events

Meaningful state changes should be represented with immutable, serializable, versionable domain events.

Generic examples include:

- GameCreated;
- PlayerJoined;
- GameStarted;
- TurnStarted;
- PhaseChanged;
- RandomValueGenerated/DiceRolled where appropriate;
- TokenMoved;
- CardDrawn;
- CardPlayed;
- ResourceTransferred;
- AssetOwnershipChanged;
- PlayerStatusChanged;
- TurnEnded;
- GameEnded.

Games may define additional events.

Each persisted event should be:

- identifiable;
- ordered;
- associated with a game/match;
- associated with a state revision or sequence;
- versionable;
- replayable;
- sufficiently semantic to explain what happened without storing prose as the source of truth.

Presentation layers may turn events into localized narrative text.

---

## 15. Board abstraction

Do not assume all boards are circular lists.

The architecture should be able to evolve toward:

- linear paths;
- circular paths;
- graphs;
- grids;
- hex grids;
- territories;
- branches;
- portals;
- zones;
- multiple routes.

A graph-like model may eventually be the broadest abstraction, but do not force graph complexity into the first Mayordomo slice unless needed.

The important boundary is that game rules query board semantics rather than depending on UI coordinates.

Board geometry and board presentation are separate from authoritative topology.

---

## 16. Randomness and dice

All random behavior must derive from the deterministic game random source.

Dice are one possible consumer, not the random system itself.

The engine should be able to support, when justified:

- standard dice;
- multiple dice;
- custom-sided dice;
- weighted random outcomes;
- card shuffles;
- random selection among legal elements.

A shuffle must be deterministic for the same seed and command history.

Do not use storage-order tricks or database random ordering as authoritative game randomness.

### Random streams

Consider, but do not prematurely implement, independent deterministic streams such as:

~~~text
root seed
  |- dice stream
  |- deck stream
  |- generic game stream
~~~

The benefit is that changing one random subsystem may avoid perturbing unrelated sequences.

If adopted, document the choice in an ADR because it affects replay compatibility.

---

## 17. Decks and cards

A reusable deck abstraction may eventually support operations such as:

- shuffle;
- draw;
- discard;
- return;
- reinsert;
- peek;
- remove;
- reshuffle.

A particular game enables only the operations its rules allow.

Card identity/content and card behavior must be versioned and traceable to authorized content.

Do not let presentation assets become the canonical rule source.

---

## 18. Economy, resources, and ledger semantics

For games with money or quantitative resources, prefer explicit transactions over silent property mutation.

Conceptually:

~~~text
Transaction
- source
- destination
- resource/currency
- amount
- reason
- originating command/event
~~~

Instead of treating:

~~~csharp
player.Money -= 500;
bank.Money += 500;
~~~

as sufficient evidence, represent the semantic transfer so it can be audited and replayed.

Useful invariants include:

- conservation of value unless a rule explicitly creates/destroys it;
- no impossible negative balance unless a rule allows debt;
- exclusive assets cannot have multiple exclusive owners;
- transfers are atomic.

The engine must not assume only one currency.

---

## 19. Trades are atomic domain operations

Player-to-player exchange may eventually include:

- money;
- assets;
- cards;
- resources;
- rights;
- obligations.

A trade must be all-or-nothing from the authoritative state perspective.

Partial persistence of a multi-party trade is prohibited.

Negotiation workflow may live outside the final atomic commit, but acceptance must revalidate current state/revision before execution.

---

## 20. Game result is richer than WinnerId

A finished game must produce an explicit result object/contract capable of representing:

- one winner;
- multiple winners;
- ties;
- draws;
- rankings;
- scores;
- completion reason;
- victory reason;
- elimination state;
- summary statistics.

Mayordomo defines its own semantics on top of this generic result capability.

---

## 21. Server-authoritative multiplayer

The server is authoritative for all game-changing behavior.

Clients may request:

~~~text
Roll
Move
Choose
Buy
Trade
EndTurn
~~~

but must not assert authoritative results such as:

~~~text
I rolled 12
I now own asset X
My balance is Y
I won
~~~

The server:

1. authenticates identity;
2. verifies match membership;
3. authorizes the command;
4. validates expected revision;
5. validates the game rule;
6. obtains deterministic random input if needed;
7. emits domain events;
8. persists atomically;
9. advances revision;
10. publishes resulting updates.

This boundary is required for multiplayer integrity and replay correctness.

---

## 22. Hidden information and player projections

The internal authoritative state may contain more information than any one client is allowed to see.

Support the concept:

~~~text
Full authoritative state
        |
        v
Project for viewer
        |
        v
Viewer-visible state
~~~

This keeps the engine compatible with future games that use:

- private hands;
- secret objectives;
- hidden decks;
- fog of war;
- asymmetric information.

Projection rules belong to the ruleset/application boundary.

A spectator is not necessarily a player and may receive a different projection.

---

## 23. Persistence boundary

Domain code must not depend on a particular persistence technology.

The domain should not know about:

- Entity Framework;
- SQL Server/PostgreSQL;
- Redis;
- Azure;
- JSON files;
- message brokers.

Infrastructure adapters may persist:

- game metadata;
- ruleset/content versions;
- current state/snapshot;
- command outcomes;
- ordered events;
- revision;
- players;
- deterministic seed;
- timestamps that are operational rather than authoritative game logic.

The engine itself must remain executable fully in memory for tests and simulations.

---

## 24. Event log and snapshots

The authoritative history should remain reconstructible without requiring every read to start at event zero forever.

Support snapshots as an optimization:

~~~text
events 1..1000
snapshot at 1000
events 1001..N
~~~

The snapshot is not a substitute for authoritative history.

Snapshot frequency is an infrastructure/application concern and should be configurable.

A corrupted or missing snapshot should be recoverable from earlier history when operationally feasible.

---

## 25. Versioning and match immutability

A match must record the exact versions that define its behavior.

At minimum consider:

- EngineVersion;
- RulesetVersion;
- ContentVersion;
- StateSchemaVersion;
- EventSchemaVersion.

A game started under Ruleset 1.2 must not silently change because Ruleset 1.3 is deployed.

Existing matches should either:

- remain pinned to the original version; or
- undergo an explicit, tested migration whose semantics are documented.

Persisted events are especially sensitive: prefer additive version evolution, upcasters, or migrations over silently changing old event meaning.

---

## 26. Protocol contracts

Do not serialize internal domain objects directly as the public wire protocol.

Prefer explicit contracts equivalent to:

- CommandEnvelope;
- CommandResult;
- EventEnvelope;
- GameSnapshot;
- PlayerProjection;
- LegalActionSet;
- Error/RuleViolation response.

Public contracts should be independently versionable from internal implementation details.

This protects client compatibility and domain refactoring.

---

## 27. Real-time transport and reconnection

Transport is an adapter, not game logic.

A likely split for .NET is:

- HTTP for match creation, joining, metadata, snapshot retrieval;
- SignalR/WebSocket for live commands, events, presence, and updates.

The exact transport is not mandated.

A disconnected player must be able to:

1. reconnect;
2. declare the last revision known;
3. receive missing events or a new snapshot;
4. continue without restarting the match.

Connection presence must not be stored as authoritative game state unless a rule explicitly makes connectivity itself part of the game.

---

## 28. Presence is not domain state

Keep transport presence concepts separate:

- online/offline;
- connection id;
- heartbeat;
- latency;
- reconnect status.

These are infrastructure/session concerns.

The ruleset should not depend on a WebSocket object being alive.

If a rule needs timeouts or forfeiture, model the rule semantically using game-clock/deadline concepts rather than transport internals.

---

## 29. Game clock

Any rule that depends on time must use an abstract game clock.

Production may bind it to wall-clock time.

Tests must be able to use a controlled fake clock.

This enables deterministic testing of:

- timers;
- expirations;
- asynchronous turns;
- grace periods;
- timeouts.

Do not use \`Task.Delay\` inside domain logic to model game rules.

---

## 30. Bots and agents

Define a boundary that allows a participant decision source to be:

- human;
- random legal-action bot;
- heuristic bot;
- future AI agent;
- remote integration.

The first useful bot is intentionally simple:

> choose randomly from current legal actions using deterministic testable randomness.

This enables high-volume headless testing before sophisticated AI exists.

Bots must use the same legal-action and command contracts as human clients wherever practical.

---

## 31. Headless simulation

The authoritative engine must run with:

- no browser;
- no database;
- no network;
- no animation.

A simulation harness should eventually support:

~~~text
game definition
+ configuration
+ deterministic seed
+ N bot participants
--------------------------------
completed match + history + result
~~~

This capability is crucial for:

- deadlock detection;
- invariant testing;
- balance analysis;
- fuzzing;
- probability checks;
- performance;
- regression discovery.

The goal should be thousands of matches as CPU-bound tests/tools, not human-time animations.

---

## 32. UI/client boundary

Preferred web technology remains React + TypeScript as established by project guidance.

The client is a projection/rendering surface:

~~~text
Authoritative server state/events
        |
        v
Viewer projection
        |
        v
Client view model
        |
        v
React presentation
~~~

The client may animate consequences, but animations must never control authoritative state progression.

For example:

~~~text
DiceRolled(5)
TokenMoved(A, F)
~~~

are authoritative events.

The UI may then animate dice and movement however it wants.

If the animation never runs, the authoritative game must still be correct.

The engine must also remain usable by:

- CLI;
- automated bots;
- simulations;
- future desktop/mobile clients;
- other presentation technologies.

---

## 33. Accessibility and semantic state

Authoritative game meaning must not exist only as pixels or coordinates.

Every critical element must have semantic identity.

This supports:

- accessibility;
- text/CLI clients;
- automated testing;
- bots;
- alternate renderers.

A board square must be identifiable independently from its screen rectangle.

A card must be identifiable independently from its artwork.

---

## 34. Localization

The engine emits semantics, not localized prose.

Game/content packs provide localizable text/resources.

Presentation chooses locale.

Do not hard-code user-facing sentences into reusable engine events where a semantic event can be rendered later.

---

## 35. Security boundaries

Never trust identifiers or permissions asserted by a client.

The application flow is conceptually:

~~~text
Authenticated account
        |
        v
Game membership
        |
        v
Actor/player identity
        |
        v
Authorized command types
        |
        v
Ruleset legality
~~~

Authorization and game-rule validation are related but distinct.

Examples:

- "this account may act as player P2" is authorization;
- "P2 may buy this asset in the current phase" is a game rule.

Do not collapse them into one opaque permission check.

---

## 36. Transaction boundary and publication

A command is conceptually one atomic transaction:

~~~text
load authoritative state
validate command id/revision
authorize
execute rules
produce events
persist events/outcome
persist state/snapshot as required
advance revision
commit
publish
~~~

It must not be possible to persist half a domain action.

If reliable event publication becomes necessary across transactional persistence boundaries, consider an Outbox pattern.

Do not add an Outbox before there is a real persistence/publication requirement.

---

## 37. Observability and audit

Operational telemetry should make a command traceable through the system.

Useful correlation fields include:

- GameId;
- CommandId;
- EventId;
- PlayerId/ActorId where safe;
- revision;
- ruleset version;
- duration;
- result/error category.

Do not log secrets or hidden-information payloads unnecessarily.

The system should eventually be able to answer:

- who requested the action;
- which command was accepted;
- against which revision;
- which events were produced;
- which revision resulted;
- which state hash resulted.

Analytics should consume events/projections rather than contaminate domain rules.

---

## 38. Deterministic state hash

Consider a canonical deterministic hash per authoritative revision.

Example:

~~~text
Revision: 42
StateHash: 9A7E...
~~~

This gives a fast divergence detector for:

- replay;
- simulations;
- server/client diagnostics;
- regression tests;
- persisted-state verification.

Hashing requires canonical serialization or another stable representation. If adopted, define that contract explicitly and version it.

---

## 39. Suggested module boundaries

Do not perform a mass rename solely to match this document.

The following names are illustrative of boundaries, not mandatory immediate project names:

~~~text
BoardGame.Engine
BoardGame.Application
BoardGame.Protocol
BoardGame.Infrastructure

Mayordomo.Rules
Mayordomo.Content
Mayordomo.Application

Mayordomo.Server
Mayordomo.Web
~~~

The non-negotiable dependency direction is:

~~~text
Reusable engine -> must not reference Mayordomo
Mayordomo       -> may reference reusable engine
Domain          -> must not reference infrastructure
~~~

Existing \`Mayordomo.Core\` may evolve incrementally toward these boundaries through real slices rather than a speculative reorganization.

---

## 40. Architecture tests

Automate dependency boundaries once project structure justifies them.

Examples:

~~~text
Engine must not reference Mayordomo-specific assemblies.
Domain must not reference Infrastructure.
Ruleset must not depend on Web/React/SignalR.
Tests may reference product assemblies, never the reverse.
~~~

Architecture tests are preferred over relying indefinitely on convention.

---

## 41. TDD contract for this architecture

Meaningful behavior is delivered through:

~~~text
RED -> GREEN -> REFACTOR
~~~

Prefer the following evidence pyramid:

1. many deterministic domain tests;
2. application/contract tests;
3. integration tests for boundaries;
4. a small number of end-to-end tests.

Do not make browser/network/database involvement mandatory for domain rules.

Do not change expected results merely to make the implementation pass.

Coverage is supporting evidence, not a target to game.

---

## 42. Golden replay tests

Maintain known deterministic matches as fixtures once the event/command model exists.

Example:

~~~text
Given:
- engine version E
- ruleset version R
- seed S
- command sequence C

Expect:
- event stream H
- final revision N
- result X
- state hash Y
~~~

An unintended rule change should break the golden replay.

Intentional rule changes should create a new version/fixture, not silently rewrite history.

---

## 43. Property-based invariants

Beyond example tests, exercise invariants such as:

- revision never decreases;
- a command id is applied at most once;
- an exclusive asset never has multiple exclusive owners;
- a finished match rejects ordinary gameplay commands;
- a player not allowed to act cannot advance authoritative state;
- a deck cannot invent card identities;
- resource totals remain conserved unless an explicit rule changes supply;
- replay reproduces the same final state;
- every generated legal action is executable against the unchanged revision;
- no accepted command leaves an invalid state.

Use property-based testing where it provides signal without obscuring domain intent.

---

## 44. Invariant validator

During tests/development, support a validator equivalent to:

~~~text
ValidateInvariants(state)
~~~

It should detect impossible states as early as possible.

This validator is especially valuable after each command in simulation/fuzz tests.

Do not necessarily run every expensive invariant in every production request; classify checks by cost and risk.

---

## 45. Monkey/fuzz players

A powerful generic test participant is:

> choose one action at random from the current legal-action set.

Run many deterministic seeds.

The expected invariant is not "every game finishes quickly"; it is:

~~~text
no crash
no impossible state
no illegal accepted transition
no deadlocked state with no defined outcome unless the rules allow it
~~~

If a valid ruleset can produce indefinite games, simulation should enforce a configurable safety horizon and report that fact rather than hiding it.

---

## 46. Rule of Three for abstraction

Do not build the complete engine in isolation before implementing Mayordomo.

When Mayordomo needs capability X, ask:

1. Is X inherently Mayordomo-specific?
2. Can X be accurately named and described generically?
3. Is reuse plausible without weakening Mayordomo correctness?

If yes, place the minimal reusable primitive in the engine.

If not, keep it in Mayordomo.

Promote abstractions only when real repeated behavior justifies them.

The engine should grow **with** Mayordomo, not ahead of it.

---

## 47. Non-goals for the initial engine

Do not implement prematurely:

- a universal rule DSL;
- a visual rule editor;
- a plugin marketplace;
- arbitrary mod scripting;
- microservices;
- Kubernetes-only deployment assumptions;
- blockchain;
- generative AI inside authoritative rule execution;
- distributed actors;
- a commercial game marketplace;
- licensed third-party content before rights are secured.

Prefer a modular monolith with explicit boundaries.

Extract services only when operational evidence justifies extraction.

---

## 48. Recommended walking skeleton

The first architecture increment should prove an end-to-end deterministic path with the smallest possible behavior.

Target capability:

~~~text
1 game definition
2 players
1 explicit turn sequence
1 deterministic random operation
1 command
1 domain event
1 state transition
1 legal-action query
1 replayable history
1 replay equivalence test
~~~

No sophisticated UI is required.

The walking skeleton exists to prove that architectural claims are executable.

---

## 49. Suggested slice sequence

The sequence may adapt to actual repository state, but prefer thin vertical slices.

### Slice 1 — identities and revision

Deliver:

- game id;
- player id;
- state revision;
- minimal initial state.

### Slice 2 — command/event transition

Deliver:

- command envelope;
- command validation;
- domain event;
- controlled reducer/state transition.

### Slice 3 — turn/phase foundation

Deliver:

- explicit current turn;
- explicit phase;
- rule preventing illegal actor/phase transitions.

### Slice 4 — deterministic randomness

Deliver:

- seeded random abstraction;
- deterministic test;
- no uncontrolled entropy in authoritative domain code.

### Slice 5 — legal actions

Deliver:

- authoritative legal-action query;
- test proving illegal commands remain rejected even if a client attempts them directly.

### Slice 6 — replay

Deliver:

- ordered history;
- replay operation;
- deterministic final-state equivalence;
- state hash if its canonical representation is ready.

### Slice 7 — persistence port

Deliver:

- storage abstraction;
- in-memory implementation;
- first real infrastructure adapter only when needed.

### Slice 8 — headless simulation

Deliver:

- simple deterministic bot;
- complete headless match path;
- invariant validation.

### Slice 9 — first real Mayordomo behavior

Deliver an actual canonical Mayordomo rule through the new architecture.

This is the point where architecture begins proving product value rather than remaining theoretical.

### Slice 10 — multiplayer/application boundary

Deliver:

- authoritative application command path;
- viewer projection;
- revision conflict behavior;
- real-time transport only as needed.

Do not implement ten speculative slices if repository reality reveals a smaller coherent path.

---

## 50. Unknown Mayordomo rules

Never invent unresolved game behavior merely to keep engineering moving.

Track each ambiguity explicitly with an identifier such as:

~~~text
RULE-QUESTION-###
~~~

Record:

- the question;
- available canonical evidence;
- plausible interpretations;
- implementation impact;
- current status;
- authorized resolution when available.

A temporary implementation may be used only if it is clearly marked as provisional and cannot be mistaken for canonical behavior.

---

## 51. Content provenance

Every production card, board effect, rule table, and other behavior-driving content must trace to an authorized source.

Development fixture content must be unmistakably labeled as fixture/non-production data.

Do not infer missing production text from another edition unless explicitly authorized.

Once a match begins, its ruleset/content identity must remain immutable.

---

## 52. Client and content licensing boundary

Future licensed products should be able to supply independently:

~~~text
ruleset
content
names
artwork
audio
branding
presentation
commercial packaging
~~~

without embedding licensed material in the reusable engine.

Conceptually:

~~~text
Reusable engine
    +
Game ruleset
    +
Content pack
    +
Brand/game pack
    =
Playable product
~~~

This separation is both an architectural and product-management concern.

---

## 53. Performance posture

Do not optimize prematurely, but preserve the ability to run domain logic cheaply.

Authoritative in-memory execution should not require:

- database round-trips for internal rule steps;
- network calls;
- serialization between every rule function;
- real-time delays.

The simulation path is an early pressure test for unnecessary infrastructure coupling.

---

## 54. Error taxonomy

Distinguish game/application errors from infrastructure failures.

Game/application categories may include:

- InvalidCommand;
- UnauthorizedActor;
- IllegalAction;
- ConcurrencyConflict;
- InvalidPhase;
- RuleViolation;
- GameAlreadyFinished.

Infrastructure categories may include:

- PersistenceUnavailable;
- TransportFailure;
- SerializationFailure.

Do not surface infrastructure exceptions as if they were game-rule outcomes.

---

## 55. ADR candidates

Create ADRs only when a decision materially constrains future implementation.

Likely candidates include:

1. deterministic execution model;
2. command/event/reducer model;
3. replay source of truth;
4. snapshot strategy;
5. random-stream strategy;
6. public protocol versioning;
7. persistence adapter choice;
8. real-time transport;
9. state hashing/canonical serialization;
10. ruleset/content version pinning.

Avoid ADR noise for trivial implementation choices.

---

## 56. CI expectations

As relevant surfaces appear, CI should certify exact candidate SHAs with:

- build;
- deterministic domain tests;
- application/contract tests;
- integration tests;
- architecture dependency tests;
- formatter/linter;
- TypeScript compile/tests when the web client exists;
- replay/golden tests when replay exists.

A documentation-only branch need not manufacture product tests merely to generate a green badge, but it must still respect repository branch/PR policy.

---

## 57. Definition of Done — engine MVP

The reusable engine MVP is not done until the repository can demonstrate, with tests:

1. create a match;
2. register/associate players;
3. initialize a versioned game definition;
4. execute an authorized command;
5. reject an illegal command;
6. produce domain events;
7. advance state through a controlled transition;
8. maintain a monotonic revision;
9. expose legal actions;
10. use deterministic random input;
11. record the seed/context necessary for replay;
12. persist or retain an ordered authoritative history;
13. replay to the same final state;
14. execute headlessly;
15. support a simple bot/agent;
16. detect at least the core invariants.

---

## 58. Definition of Done — Mayordomo implementation

Mayordomo is architecturally healthy when:

- canonical known rules are implemented and tested;
- unknown rules remain explicit;
- no unresolved assumption is presented as official;
- matches are pinned to ruleset/content versions;
- a complete match can execute headlessly;
- authoritative multiplayer state remains consistent;
- replay reconstructs authoritative state;
- a reconnecting client can recover;
- the UI derives allowed choices from authoritative legal actions;
- Mayordomo-specific concepts do not leak into reusable engine code;
- authorized content retains provenance.

---

## 59. Pull-request design questions

Every meaningful PR that touches engine/rules boundaries should answer:

1. What observable behavior is added or changed?
2. What is the RED evidence or preserved contract?
3. Which layer owns the behavior: Engine, Ruleset, Content Pack, Brand/Game Pack, Application, Infrastructure, Client?
4. Does this introduce a Mayordomo-specific concept into a reusable layer?
5. Is deterministic replay preserved?
6. Is historical/version compatibility affected?
7. What invariants protect the new behavior?
8. Is a new abstraction justified by a real need?
9. Can the same capability plausibly support a second game without weakening Mayordomo?
10. What exact SHA did CI certify?

A useful final question is:

> Does this change increase our ability to implement Mayordomo without reducing our ability to implement a second game?

---

## 60. Definition of excellence

The architecture should eventually make these statements true:

> A match is not a loose collection of objects mutating in memory. It is a validated, deterministic, versioned, auditable sequence of decisions and domain events.

> Mayordomo is not the engine. Mayordomo is the first canonical game implemented on the engine.

> Rules + authorized content + authorized presentation assets can produce a playable game without changing the reusable execution core for brand-specific reasons.

The immediate priority remains smaller:

> Build the smallest deterministic executable core, prove it through TDD, and let real Mayordomo slices drive the engine's growth.
