# Performance and footprint

Mayordomo Engine treats size and speed as product capabilities.

## Current budgets

| Metric | Budget |
| --- | ---: |
| Core DLL design target | ≤ 256 KiB |
| Core DLL hard CI ceiling | ≤ 512 KiB |
| Mandatory third-party runtime package dependencies | 0 |
| Headless execution | Required |

The hard ceiling is intentionally generous during foundation; the design target is the stronger everyday constraint.

## Benchmark policy

Do not publish marketing numbers without reproducible evidence.

The first meaningful walking skeleton will establish BenchmarkDotNet scenarios for:

- legal-action query;
- deterministic command execution;
- event reduction/state transition;
- deterministic random operation;
- replay;
- headless simulation throughput.

Each published result must identify the source SHA and environment.

## Regression policy

Once a benchmark becomes stable enough to act as a contract, material regressions require explicit explanation.

Absolute CI timings are noisy, so initial enforcement should prefer:

- deterministic correctness;
- assembly/package footprint;
- allocations;
- relative regression against a controlled baseline;
- repeatable benchmark artifacts.

## Philosophy

The engine should disappear into the consuming game.

A developer should not need to accept a large framework, a cloud service, a UI stack, or dozens of transitive dependencies simply to use deterministic board-game behavior.
