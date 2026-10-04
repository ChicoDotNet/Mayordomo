import fs from "node:fs";

const API_BASELINE =
  "20B4806BBD144D2B317E701BB8EF3FED656AE9E731E1D48414D53298F199E70A";

export function validateFinalReleaseContract({ workflow, changelog, releasing }) {
  const errors = [];

  const requiredWorkflowFragments = [
    "source-sha: ${{ steps.version.outputs.source_sha }}",
    'echo "source_sha=$SOURCE_SHA"',
    "finalize-release:",
    "if: github.event_name == 'workflow_dispatch' && inputs.publish",
    "PACKAGE_VERSION: ${{ needs.release-candidate.outputs.package-version }}",
    "SOURCE_SHA: ${{ needs.release-candidate.outputs.source-sha }}",
    "sha256sum -c artifacts/nuget/SHA256SUMS.txt",
    "api.nuget.org/v3-flatcontainer/mayordomo.core/$PACKAGE_VERSION",
    'tag="v$PACKAGE_VERSION"',
    "git/ref/tags/$tag",
    '-f sha="$SOURCE_SHA"',
    'gh release upload "$tag" "${assets[@]}" --clobber',
    'gh release create "$tag" "${assets[@]}"',
    "--verify-tag",
    "release-manifest.json",
    "stateHashFormatVersion: 2",
    "packageSha256: $packageSha256",
    "symbolPackageSha256: $symbolPackageSha256",
    API_BASELINE,
    'test "$tag_sha" = "$SOURCE_SHA"',
    '"Mayordomo.Core.$PACKAGE_VERSION.nupkg"',
    '"Mayordomo.Core.$PACKAGE_VERSION.snupkg"',
    '"SHA256SUMS.txt"',
    '"core-benchmark.json"',
  ];

  for (const fragment of requiredWorkflowFragments) {
    if (!workflow.includes(fragment)) {
      errors.push("Missing final-release workflow contract: " + fragment);
    }
  }

  const publishIndex = workflow.indexOf("\n  publish:");
  const finalizeIndex = workflow.indexOf("\n  finalize-release:");

  if (publishIndex < 0 || finalizeIndex < 0 || finalizeIndex <= publishIndex) {
    errors.push("Finalization job must exist after the NuGet publish job.");
  } else {
    const publishBlock = workflow.slice(publishIndex, finalizeIndex);
    const finalizeBlock = workflow.slice(finalizeIndex);

    if (!publishBlock.includes("contents: read") ||
        !publishBlock.includes("id-token: write")) {
      errors.push("NuGet publish job must retain contents: read and id-token: write.");
    }

    if (publishBlock.includes("contents: write")) {
      errors.push("NuGet publish job must not receive repository contents write permission.");
    }

    if (!finalizeBlock.includes("contents: write")) {
      errors.push("Finalization job requires contents: write to create tag/release metadata.");
    }

    if (finalizeBlock.includes("id-token: write")) {
      errors.push("Finalization job must not receive OIDC permission.");
    }

    if (!finalizeBlock.includes("- release-candidate") ||
        !finalizeBlock.includes("- publish")) {
      errors.push("Finalization job must depend on candidate certification and NuGet publish.");
    }

    if (!finalizeBlock.includes("--clobber")) {
      errors.push("GitHub Release asset upload must be repairable on a job-only rerun.");
    }
  }

  if (/pre-alpha|no supported packaged release/i.test(changelog)) {
    errors.push("CHANGELOG still claims the project has no supported package release.");
  }

  for (const marker of [
    "## 1.0 release train",
    "Mayordomo.Core",
    "Trusted Publishing",
    "canonical state-hash format v2",
    API_BASELINE,
  ]) {
    if (!changelog.includes(marker)) {
      errors.push("CHANGELOG is missing release marker: " + marker);
    }
  }

  for (const marker of [
    "finalize-release",
    "same workflow run",
    "Do not dispatch a new publish run",
    "v{packageVersion}",
    "release-manifest.json",
  ]) {
    if (!releasing.includes(marker)) {
      errors.push("Release documentation is missing recovery marker: " + marker);
    }
  }

  return errors;
}

if (import.meta.url === "file://" + process.argv[1]) {
  const errors = validateFinalReleaseContract({
    workflow: fs.readFileSync(".github/workflows/publish-nuget.yml", "utf8"),
    changelog: fs.readFileSync("CHANGELOG.md", "utf8"),
    releasing: fs.readFileSync("docs/RELEASING.md", "utf8"),
  });

  if (errors.length > 0) {
    console.error("FINAL RELEASE CONTRACT CHECK FAILED");
    for (const error of errors) {
      console.error("- " + error);
    }
    process.exit(1);
  }

  console.log("FINAL RELEASE CONTRACT CHECK PASSED");
}
