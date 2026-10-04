import assert from "node:assert/strict";
import test from "node:test";

import { validateFinalReleaseContract } from "./check-release-finalization.mjs";

const API_BASELINE =
  "20B4806BBD144D2B317E701BB8EF3FED656AE9E731E1D48414D53298F199E70A";

function validContract() {
  const workflow = [
    "jobs:",
    "  release-candidate:",
    "    outputs:",
    "      package-version: ${{ steps.version.outputs.package_version }}",
    "      artifact-name: ${{ steps.version.outputs.artifact_name }}",
    "      source-sha: ${{ steps.version.outputs.source_sha }}",
    '    echo "source_sha=$SOURCE_SHA"',
    "  publish:",
    "    permissions:",
    "      contents: read",
    "      id-token: write",
    "    steps:",
    "      - uses: actions/checkout@v7",
    "        with:",
    "          ref: ${{ needs.release-candidate.outputs.source-sha }}",
    "          persist-credentials: false",
    "      - uses: actions/setup-dotnet@v6",
    "        with:",
    "          global-json-file: global.json",
    "  finalize-release:",
    "    if: github.event_name == 'workflow_dispatch' && inputs.publish",
    "    needs:",
    "      - release-candidate",
    "      - publish",
    "    permissions:",
    "      contents: write",
    "    env:",
    "      PACKAGE_VERSION: ${{ needs.release-candidate.outputs.package-version }}",
    "      SOURCE_SHA: ${{ needs.release-candidate.outputs.source-sha }}",
    "    steps:",
    "      - run: |",
    "          sha256sum -c artifacts/nuget/SHA256SUMS.txt",
    '          url="https://api.nuget.org/v3-flatcontainer/mayordomo.core/$PACKAGE_VERSION"',
    '          tag="v$PACKAGE_VERSION"',
    '          gh api "repos/$GITHUB_REPOSITORY/git/ref/tags/$tag"',
    '          gh api -f sha="$SOURCE_SHA"',
    '          gh release upload "$tag" "${assets[@]}" --clobber',
    '          gh release create "$tag" "${assets[@]}" --verify-tag',
    "          echo release-manifest.json",
    "          echo stateHashFormatVersion: 2",
    "          echo packageSha256: $packageSha256",
    "          echo symbolPackageSha256: $symbolPackageSha256",
    "          echo " + API_BASELINE,
    '          test "$tag_sha" = "$SOURCE_SHA"',
    '          echo "Mayordomo.Core.$PACKAGE_VERSION.nupkg"',
    '          echo "Mayordomo.Core.$PACKAGE_VERSION.snupkg"',
    '          echo "SHA256SUMS.txt"',
    '          echo "core-benchmark.json"',
  ].join("\n");

  return {
    workflow,
    changelog: [
      "## 1.0 release train",
      "Mayordomo.Core uses Trusted Publishing.",
      "canonical state-hash format v2",
      API_BASELINE,
    ].join("\n"),
    releasing: [
      "finalize-release",
      "same workflow run",
      "Do not dispatch a new publish run",
      "v{packageVersion}",
      "release-manifest.json",
    ].join("\n"),
  };
}

test("accepts the complete separated publication/finalization contract", () => {
  assert.deepEqual(validateFinalReleaseContract(validContract()), []);
});

test("rejects repository write permission in the OIDC publish job", () => {
  const contract = validContract();
  contract.workflow = contract.workflow.replace("contents: read", "contents: write");
  assert.match(
    validateFinalReleaseContract(contract).join("\n"),
    /publish job must not receive repository contents write/i);
});

test("requires certified checkout before publish setup-dotnet", () => {
  const contract = validContract();
  contract.workflow = contract.workflow.replace(
    "      - uses: actions/checkout@v7\n        with:\n          ref: ${{ needs.release-candidate.outputs.source-sha }}\n          persist-credentials: false\n",
    "");

  assert.match(
    validateFinalReleaseContract(contract).join("\n"),
    /checkout the certified source before setup-dotnet/i);
});

test("rejects stale pre-release changelog language", () => {
  const contract = validContract();
  contract.changelog += "\nThe project is currently pre-alpha and has no supported packaged release.";
  assert.match(
    validateFinalReleaseContract(contract).join("\n"),
    /changelog still claims/i);
});

test("requires a repairable GitHub Release path", () => {
  const contract = validContract();
  contract.workflow = contract.workflow.replace(" --clobber", "");
  assert.match(
    validateFinalReleaseContract(contract).join("\n"),
    /repairable/i);
});
