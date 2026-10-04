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
11. audit the actual `.nupkg/.snupkg` metadata and archive layout;
12. verify the final-release workflow contract;
13. confirm the target NuGet version is not already published.

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

After NuGet publication succeeds, `finalize-release` automatically creates the matching immutable source tag and GitHub Release.

The workflow itself rejects publication from any ref other than `refs/heads/main`.

Do not use `--skip-duplicate`. A version collision is a release error because NuGet versions are immutable.

## Git tag and GitHub Release

Final source metadata is deliberately a separate `finalize-release` job that runs only after both release-candidate certification and successful NuGet publication.

For package version `v{packageVersion}` semantics, finalization uses tag `v{packageVersion}` and:

1. downloads the exact certified artifact from the same workflow run;
2. verifies the recorded package SHA-256 checksums;
3. confirms the package is visible on NuGet.org;
4. creates or verifies immutable tag `v{packageVersion}` at the certified source SHA;
5. creates or repairs the GitHub Release idempotently;
6. attaches the `.nupkg`, `.snupkg`, `SHA256SUMS.txt`, benchmark result and `release-manifest.json`;
7. audits the final tag target, release state and required asset names.

The finalization job has `contents: write` but no OIDC permission. The NuGet publish job has OIDC permission but retains `contents: read`. This keeps package credentials and repository-release mutation separated.

### Recovery after NuGet publication

If NuGet publication succeeds but `finalize-release` fails, rerun only the failed finalization job from the **same workflow run**. The finalizer is repairable: it accepts an already-correct tag and uses release asset upload with clobber semantics.

**Do not dispatch a new publish run** merely to repair GitHub release metadata. A new dispatch receives a new build number and therefore a different immutable package version.

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
