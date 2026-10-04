namespace Mayordomo.Tooling.Tests

open Xunit
open Mayordomo.Tooling

module BranchPolicyTests =
    [<Fact>]
    let accepts_repository_working_branch_convention () =
        Assert.True(
            BranchPolicy.isValid(
                "features/ci-i01-fsharp-tooling"))

        Assert.True(
            BranchPolicy.isValid(
                "hotfixes/release-metadata"))

    [<Fact>]
    let rejects_invalid_branch_convention () =
        Assert.False(
            BranchPolicy.isValid(
                "feature/wrong"))

        Assert.False(
            BranchPolicy.isValid(
                "Features/Uppercase"))

module ReleaseVersionTests =
    [<Fact>]
    let maps_package_version_to_stable_versions () =
        match
            ReleaseVersion.compute
                1
                0
                "20261004"
                1
                "60c539c0e6b2"
        with
        | Error error ->
            Assert.Fail(error)
        | Ok version ->
            Assert.Equal(
                "1.0.20261004.1",
                version.PackageVersion)

            Assert.Equal(
                "1.0.0.0",
                version.AssemblyVersion)

            Assert.Equal(
                "1.0.26277.1",
                version.FileVersion)

            Assert.Equal(
                "1.0.20261004.1+60c539c0e6b2",
                version.InformationalVersion)

    [<Fact>]
    let rejects_invalid_calendar_date () =
        match
            ReleaseVersion.compute
                1
                0
                "20260230"
                1
                "abc123"
        with
        | Ok _ ->
            Assert.Fail(
                "Expected invalid calendar date.")
        | Error error ->
            Assert.Contains(
                "valid calendar date",
                error)

    [<Fact>]
    let rejects_build_outside_assembly_limits () =
        match
            ReleaseVersion.compute
                1
                0
                "20261004"
                65536
                "abc123"
        with
        | Ok _ ->
            Assert.Fail(
                "Expected build limit failure.")
        | Error error ->
            Assert.Contains(
                "65535",
                error)

    [<Fact>]
    let requires_source_sha () =
        match
            ReleaseVersion.compute
                1
                0
                "20261004"
                1
                ""
        with
        | Ok _ ->
            Assert.Fail(
                "Expected source SHA failure.")
        | Error error ->
            Assert.Contains(
                "source SHA",
                error)

    [<Fact>]
    let selects_monotonically_increasing_release_build () =
        Assert.Equal(
            19,
            ReleaseVersion.selectBuild
                19
                17)

        Assert.Equal(
            20,
            ReleaseVersion.selectBuild
                18
                19)

    [<Fact>]
    let finds_latest_published_build_for_release_date () =
        let versions =
            [
                "1.0.20261004.16"
                "1.0.20261004.17"
                "1.0.20261003.99"
                "2.0.20261004.50"
            ]

        Assert.Equal(
            17,
            ReleaseVersion.latestPublishedBuild
                "20261004"
                versions)

module BenchmarkBudgetTests =
    let private budgets =
        [
            {
                BenchmarkBudget.Name =
                    "canonical-state-hash"
                MaxMicrosecondsPerOperation =
                    100.0
                MaxBytesPerOperation =
                    1000.0
            }
            {
                BenchmarkBudget.Name =
                    "event-replay"
                MaxMicrosecondsPerOperation =
                    1000.0
                MaxBytesPerOperation =
                    10000.0
            }
        ]

    [<Fact>]
    let accepts_complete_report_within_hard_ceiling () =
        let json =
            """{"schemaVersion":1,"results":[{"name":"canonical-state-hash","microsecondsPerOperation":20,"bytesPerOperation":500},{"name":"event-replay","microsecondsPerOperation":500,"bytesPerOperation":5000}]}"""

        Assert.Empty(
            BenchmarkBudget.evaluate
                json
                budgets)

    [<Fact>]
    let reports_latency_and_allocation_regressions () =
        let json =
            """{"schemaVersion":1,"results":[{"name":"canonical-state-hash","microsecondsPerOperation":120,"bytesPerOperation":1500},{"name":"event-replay","microsecondsPerOperation":500,"bytesPerOperation":5000}]}"""

        let violations =
            BenchmarkBudget.evaluate
                json
                budgets

        Assert.Equal(
            2,
            violations.Length)

        Assert.Contains(
            "latency",
            violations[0])

        Assert.Contains(
            "allocation",
            violations[1])

    [<Fact>]
    let fails_closed_when_required_benchmark_is_missing () =
        let json =
            """{"schemaVersion":1,"results":[{"name":"canonical-state-hash","microsecondsPerOperation":20,"bytesPerOperation":500}]}"""

        let violations =
            BenchmarkBudget.evaluate
                json
                budgets

        Assert.Single(
            violations)
        |> ignore

        Assert.Contains(
            "event-replay",
            violations[0])

    [<Fact>]
    let rejects_unknown_benchmark_schema () =
        let violations =
            BenchmarkBudget.evaluate
                """{"schemaVersion":2,"results":[]}"""
                budgets

        Assert.Single(
            violations)
        |> ignore

        Assert.Contains(
            "Unsupported benchmark report schema version",
            violations[0])

module ReleaseContractTests =
    let private validWorkflow =
        """
jobs:
  release-candidate:
    outputs:
      package-version: ${{ steps.version.outputs.package_version }}
      source-sha: ${{ steps.version.outputs.source_sha }}
    steps:
      - run: dotnet "$TOOLING_DLL" release-version 1 0 "$release_date" "$GITHUB_RUN_NUMBER" "$SOURCE_SHA"
      - run: dotnet "$TOOLING_DLL" release-contract
      - run: dotnet "$TOOLING_DLL" public-readiness
      - run: dotnet "$TOOLING_DLL" engine-budget
      - run: dotnet "$TOOLING_DLL" package-audit
      - run: dotnet "$TOOLING_DLL" consumer-smoke
      - run: dotnet "$TOOLING_DLL" benchmark-budget
  publish:
    permissions:
      contents: read
      id-token: write
    steps:
      - uses: actions/checkout@v7
        with:
          ref: ${{ needs.release-candidate.outputs.source-sha }}
          persist-credentials: false
      - uses: actions/setup-dotnet@v6
  finalize-release:
    if: github.event_name == 'workflow_dispatch' && inputs.publish
    needs:
      - release-candidate
      - publish
    permissions:
      contents: write
    env:
      PACKAGE_VERSION: ${{ needs.release-candidate.outputs.package-version }}
      SOURCE_SHA: ${{ needs.release-candidate.outputs.source-sha }}
    steps:
      - uses: actions/checkout@v7
        with:
          ref: ${{ needs.release-candidate.outputs.source-sha }}
          fetch-depth: 1
          persist-credentials: false
      - run: |
          sha256sum -c artifacts/nuget/SHA256SUMS.txt
          url="https://api.nuget.org/v3-flatcontainer/mayordomo.core/$PACKAGE_VERSION"
          for attempt in {1..180}; do
            true
          done
          tag="v$PACKAGE_VERSION"
          gh api "repos/$GITHUB_REPOSITORY/git/ref/tags/$tag"
          gh api -f sha="$SOURCE_SHA"
          gh release view "$tag"
          gh release upload "$tag" "${assets[@]}" --clobber
          gh release create "$tag" "${assets[@]}" --verify-tag
          echo release-manifest.json
          echo stateHashFormatVersion: 2
          echo packageSha256: $packageSha256
          echo symbolPackageSha256: $symbolPackageSha256
          echo 20B4806BBD144D2B317E701BB8EF3FED656AE9E731E1D48414D53298F199E70A
          test "$tag_sha" = "$SOURCE_SHA"
          echo "Mayordomo.Core.$PACKAGE_VERSION.nupkg"
          echo "Mayordomo.Core.$PACKAGE_VERSION.snupkg"
          echo "SHA256SUMS.txt"
          echo "core-benchmark.json"
          required_assets=(
          for expected in "${required_assets[@]}"; do
            true
          done
"""

    let private changelog =
        """## 1.0 release train
Mayordomo.Core uses Trusted Publishing.
canonical state-hash format v2
20B4806BBD144D2B317E701BB8EF3FED656AE9E731E1D48414D53298F199E70A"""

    let private releasing =
        """finalize-release
same workflow run
Do not dispatch a new publish run
v{packageVersion}
release-manifest.json"""

    [<Fact>]
    let accepts_complete_fsharp_release_contract () =
        Assert.Empty(
            ReleaseContract.validate
                validWorkflow
                changelog
                releasing)

    [<Fact>]
    let rejects_javascript_release_tooling () =
        let workflow =
            validWorkflow
            + "\nnode scripts/check-release-finalization.mjs"

        let errors =
            ReleaseContract.validate
                workflow
                changelog
                releasing

        Assert.True(
            errors
            |> List.exists (fun error ->
                error.Contains(
                    "F# Mayordomo.Tooling")))

    [<Fact>]
    let rejects_manual_build_override () =
        let workflow =
            validWorkflow.Replace(
                "jobs:",
                "on:\n  workflow_dispatch:\n    inputs:\n      build_number:\n        type: string\njobs:")

        let errors =
            ReleaseContract.validate
                workflow
                changelog
                releasing

        Assert.True(
            errors
            |> List.exists (fun error ->
                error.Contains(
                    "manual build_number")))

    [<Fact>]
    let requires_repairable_release_path () =
        let workflow =
            validWorkflow.Replace(
                " --clobber",
                "")

        let errors =
            ReleaseContract.validate
                workflow
                changelog
                releasing

        Assert.True(
            errors
            |> List.exists (fun error ->
                error.Contains(
                    "repairable")))
