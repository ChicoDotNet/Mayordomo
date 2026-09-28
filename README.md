# Mayordomo

Digital platform for **Mayordomo — El juego que no es un juego**.

This repository is the engineering home for the authoritative game engine and its client surfaces. The first objective is to preserve the official Mayordomo rules as versioned, testable contracts while keeping presentation technology replaceable.

## Current status

Repository bootstrap.

The canonical functional specification, complete rule matrix, card corpus, board assets, and publishing/licensing decisions will be documented in later increments. No rule or card text should be invented to fill missing source material.

## Engineering direction

- Authoritative game behavior: .NET / C#.
- First validation client: React + TypeScript web UI.
- Rich game client: Unity + C#.
- Desktop XAML client: Avalonia.
- Rust/.NET experimentation: FerrumWeave, outside the initial critical path until certified for the required surface.
- Server-authoritative multiplayer and versioned rulesets.
- Content-driven cards/rules so Mayordomo Studio can evolve toward future editions such as 6.0+.

## Delivery model

After this empty-repository bootstrap, product work flows through:

`working branch → dev → main → ancestry sync main → dev`

See `AGENTS.md` once the bootstrap increment lands.
