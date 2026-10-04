# Releasing Mayordomo Engine

Mayordomo.Core releases are stable releases. The project does not use NuGet alpha/beta publication as a staging environment.

## Release identity

The public package id is `Mayordomo.Core`.

Stable versions use:

```text
{major}.{minor}.{YYYYMMDD}.{build}
```

For the 1.0 release train:

- `major = 1`;
- `minor = 0`;
- `YYYYMMDD` is evaluated in `America/Mexico_City`;
- `build` is the GitHub Actions `GITHUB_RUN_NUMBER` for the publish workflow.

The executable mapping contract lives in `scripts/compute-release-version.mjs`.

## Trusted Publishing policy

NuGet.org must have a Trusted Publishing policy matching the workflow exactly:

- repository owner: `ChicoDotNet`;
- repository: `Mayordomo`;
- workflow file: `publish-nuget.yml`;
- GitHub environment: `release`.

The workflow never stores a long-lived NuGet API key. The publish job receives `id-token: write` only after the release-candidate job succeeds, then `NuGet/login@v1` exchanges the GitHub OIDC token for a short-lived NuGet API key.

The exact NuGet.org profile name is supplied as the non-secret `nuget_user` workflow input.

## Candidate gate

Every publish invocation rebuilds and certifies the exact source SHA before requesting an OIDC credential.

The release-candidate job must:

1. derive `PackageVersion`, `AssemblyVersion`, `FileVersion`, and `InformationalVersion`;
2. pass the executable release-version tests;
3. pass the structural public-readiness gate;
4. restore and build the solution in Release;
5. pass the engine footprint budget;
6. pass all tests;
7. produce `.nupkg` and `.snupkg` artifacts;
8. install the generated `.nupkg` into a clean temporary consumer and execute a deterministic replay smoke test;
9. execute the measured benchmark suite and pass hard performance ceilings;
10. record SHA-256 checksums for package artifacts;
11. confirm the target NuGet version is not already published.

Only then may the `publish` job enter the protected `release` environment and request OIDC credentials.

## Dry run

Pull requests that touch the release/package surface automatically run the full release-candidate job without any publish capability.

After this workflow exists on stable `main`, it can also be invoked manually with:

- `publish = false`

This performs the same release-candidate audit and uploads the candidate artifacts to GitHub Actions, but does not enter the `release` environment and does not request an OIDC token.

## Stable publication

Publication is deliberately manual.

From GitHub Actions:

1. select **Publish Mayordomo.Core**;
2. choose the stable `main` branch;
3. set `publish = true`;
4. enter the exact NuGet.org profile name in `nuget_user`;
5. approve the protected `release` environment if its rules require approval.

The workflow itself rejects publication from any ref other than `refs/heads/main`.

Do not use `--skip-duplicate`. A version collision is a release error because NuGet versions are immutable.

## Git tag and GitHub Release

NuGet publication and source-release metadata are intentionally separate until the final 1.0 release-audit pack.

The final release train must create an immutable source tag and GitHub Release for the exact successfully published package version, attach the certified package/checksum artifacts, and record the source SHA.

Separating this from the first OIDC publishing implementation prevents a GitHub Release side-effect from weakening or complicating the NuGet Trusted Publishing gate.

## Stable release checklist

A final 1.0 release must:

1. originate from a certified stable `main` state;
2. pass all build/test/repository-policy/release-candidate gates;
3. preserve deterministic/replay compatibility or explicitly version breaking changes;
4. contain no private Mayordomo.Game material;
5. have dependency/license provenance reviewed;
6. update CHANGELOG;
7. produce reproducible package artifacts;
8. record exact source SHA, package version, and artifact checksums;
9. pass public API compatibility and golden replay gates;
10. publish through Trusted Publishing/OIDC only;
11. complete the final tag/GitHub Release audit.
