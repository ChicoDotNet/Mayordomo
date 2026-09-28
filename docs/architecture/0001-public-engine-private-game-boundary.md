# 0001 — Public engine / private game boundary

**Status:** Accepted

## Context

The first commercial consumer of the engine is Mayordomo.

The reusable engine may later become public MIT-licensed software. The commercial product contains private implementation, official/authorized content, Studio capabilities, branded clients, and potentially licensed third-party material that must not automatically become open source.

The previously accepted engine architecture already establishes the non-negotiable dependency direction:

> the engine must not know that Mayordomo exists.

## Decision

Use two repositories.

### Publicable engine

`ChicoDotNet/Mayordomo`

Owns reusable, product-agnostic engine code and redistribution-safe engineering assets.

### Private product

`ChicoDotNet/Mayordomo.Game`

Owns:

- Mayordomo executable rules;
- official/authorized content;
- product configuration;
- Mayordomo Studio;
- branded Web client;
- Unity client;
- Avalonia desktop client;
- proprietary/licensed assets;
- commercial/store integrations;
- private end-to-end product tests.

## Dependency direction

```text
Mayordomo.Game.Rules ─────────────┐
Mayordomo.Game.Application ───────┤
Mayordomo.Studio ─────────────────┤
Mayordomo.Web ────────────────────┤
Mayordomo.Unity ──────────────────┼──> Mayordomo Engine
Mayordomo.Desktop ────────────────┘

Mayordomo Engine -X-> any of the above
```

The private layers may know engine contracts.

The engine may not reference, reflect over, discover, load by hard-coded identity, or compile against Mayordomo-specific assemblies merely to support the product.

Generic plugin/registration mechanisms may exist only when justified generically and must not encode a Mayordomo back-reference.

## Studio

Studio is intentionally private.

Studio may use public engine contracts to understand valid generic structures, but authoring UX, workflow, validation orchestration, product schema extensions, preview integration, and commercial content-management behavior remain in `Mayordomo.Game`.

The engine must not depend on Studio.

## History rule

This engine repository is public-safe from inception.

Do not commit private product material here temporarily and delete it later. Reachable Git history and retained branches must be assumed publishable.

## Consequences

### Positive

- engine can be open-sourced independently;
- Rafael/product IP remains isolated;
- Studio remains a competitive capability;
- private licensed game packs can reuse the engine;
- architecture tests can enforce one-way dependencies;
- a second game becomes a consumer, not a fork of the engine.

### Cost

- complete product development spans two repositories;
- package/source-reference versioning must be deliberate;
- some end-to-end tests remain private;
- cross-repository changes require a coordinated compatibility handoff.

## Enforcement

Repository policy CI checks structural public readiness.

Once module structure justifies it, architecture tests must also prove that engine assemblies do not reference product assemblies.
