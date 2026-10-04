import assert from "node:assert/strict";
import test from "node:test";

import { computeReleaseVersion } from "./compute-release-version.mjs";

test("maps package version to stable assembly/file/informational versions", () => {
  const version = computeReleaseVersion({
    major: 1,
    minor: 0,
    date: "20261004",
    build: 1,
    sha: "60c539c0e6b2",
  });

  assert.deepEqual(version, {
    packageVersion: "1.0.20261004.1",
    assemblyVersion: "1.0.0.0",
    fileVersion: "1.0.26277.1",
    informationalVersion: "1.0.20261004.1+60c539c0e6b2",
  });
});

test("rejects invalid calendar dates", () => {
  assert.throws(
    () => computeReleaseVersion({
      major: 1,
      minor: 0,
      date: "20260230",
      build: 1,
      sha: "abc123",
    }),
    /valid calendar date/i);
});

test("rejects file-version components outside assembly limits", () => {
  assert.throws(
    () => computeReleaseVersion({
      major: 1,
      minor: 0,
      date: "20261004",
      build: 65536,
      sha: "abc123",
    }),
    /build.*65535/i);
});

test("requires a source sha for traceable releases", () => {
  assert.throws(
    () => computeReleaseVersion({
      major: 1,
      minor: 0,
      date: "20261004",
      build: 1,
      sha: "",
    }),
    /source sha/i);
});
