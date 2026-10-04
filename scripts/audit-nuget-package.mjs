import { spawnSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";

const [packageDirectoryArg, version, sourceShaArg] =
  process.argv.slice(2);

if (!packageDirectoryArg || !version || !sourceShaArg) {
  fail(
    "Usage: node scripts/audit-nuget-package.mjs " +
    "<package-directory> <version> <source-sha>");
}

const packageDirectory = path.resolve(packageDirectoryArg);
const sourceSha = sourceShaArg.trim().toLowerCase();

if (!/^[0-9a-f]{40}$/.test(sourceSha)) {
  fail("Source SHA must be a full 40-character hexadecimal commit SHA.");
}

const nupkg = path.join(
  packageDirectory,
  `Mayordomo.Core.${version}.nupkg`);
const snupkg = path.join(
  packageDirectory,
  `Mayordomo.Core.${version}.snupkg`);

for (const artifact of [nupkg, snupkg]) {
  if (!fs.existsSync(artifact)) {
    fail(`Expected package artifact does not exist: ${artifact}`);
  }
}

const packageEntries = listEntries(nupkg);
const symbolEntries = listEntries(snupkg);

assertIncludes(
  packageEntries,
  "Mayordomo.Core.nuspec",
  "Primary package must include Mayordomo.Core.nuspec.");
assertIncludes(
  packageEntries,
  "lib/net10.0/Mayordomo.Core.dll",
  "Primary package must include the net10.0 engine assembly.");
assertIncludes(
  packageEntries,
  "README.md",
  "Primary package must include README.md.");

if (packageEntries.some(entry =>
      entry.toLowerCase().endsWith(".pdb"))) {
  fail("Primary .nupkg must not contain PDB files; symbols belong in .snupkg.");
}

if (packageEntries.some(entry =>
      /mayordomo\.game|mayordomo\.studio/i.test(entry))) {
  fail("Primary package contains private product identity in an archive path.");
}

const nuspec = readTextEntry(
  nupkg,
  "Mayordomo.Core.nuspec");

requireXmlValue(nuspec, "id", "Mayordomo.Core");
requireXmlValue(nuspec, "version", version);
requireXmlValue(nuspec, "license", "MIT");
requireXmlValue(nuspec, "readme", "README.md");

if (!/<description>[^<\r\n]+<\/description>/i.test(nuspec)) {
  fail("Package description must be non-empty.");
}

if (!/<projectUrl>https:\/\/github\.com\/ChicoDotNet\/Mayordomo<\/projectUrl>/i.test(nuspec)) {
  fail("Package projectUrl must point to the public Mayordomo engine repository.");
}

const repositoryMatch = nuspec.match(
  /<repository\s+([^>]+?)\s*\/?\s*>/i);

if (!repositoryMatch) {
  fail("Package nuspec must contain repository metadata.");
}

const repositoryAttributes = repositoryMatch[1];

requireAttribute(
  repositoryAttributes,
  "type",
  "git",
  "Repository type");
requireAttribute(
  repositoryAttributes,
  "url",
  "https://github.com/ChicoDotNet/Mayordomo",
  "Repository URL");
requireAttribute(
  repositoryAttributes,
  "commit",
  sourceSha,
  "Repository commit",
  { ignoreCase: true });

if (/<dependency\b/i.test(nuspec)) {
  fail("Mayordomo.Core 1.0 must not contain runtime NuGet dependencies.");
}

if (/Mayordomo\.Game|Mayordomo\.Studio/i.test(nuspec)) {
  fail("Package nuspec must not reference private product identities.");
}

assertIncludes(
  symbolEntries,
  "Mayordomo.Core.nuspec",
  "Symbol package must include its nuspec.");
assertIncludes(
  symbolEntries,
  "lib/net10.0/Mayordomo.Core.pdb",
  "Symbol package must include the portable net10.0 PDB.");

if (symbolEntries.some(entry =>
      entry.toLowerCase().endsWith(".dll"))) {
  fail("Symbol package must not contain engine DLLs.");
}

const pdb = readBinaryEntry(
  snupkg,
  "lib/net10.0/Mayordomo.Core.pdb");

if (pdb.subarray(0, 4).toString("ascii") !== "BSJB") {
  fail("Mayordomo.Core.pdb is not a Portable PDB.");
}

const sourceLinkRepository =
  Buffer.from(
    "raw.githubusercontent.com/ChicoDotNet/Mayordomo",
    "utf8");
const sourceLinkSha =
  Buffer.from(
    sourceSha,
    "utf8");

if (!pdb.includes(sourceLinkRepository)) {
  fail(
    "Portable PDB does not contain Source Link metadata for " +
    "ChicoDotNet/Mayordomo.");
}

if (!pdb.includes(sourceLinkSha)) {
  fail(
    "Portable PDB Source Link metadata does not contain the certified source SHA.");
}

console.log("NUGET PACKAGE AUDIT PASSED");
console.log(`- package: Mayordomo.Core ${version}`);
console.log(`- source: ${sourceSha}`);
console.log("- runtime NuGet dependencies: 0");
console.log("- primary symbols: excluded");
console.log("- symbol package: Portable PDB present");
console.log("- Source Link: repository + exact source SHA present");

function listEntries(archive) {
  const result = runUnzip(
    ["-Z1", archive],
    { encoding: "utf8" });

  return result.stdout
    .split(/\r?\n/)
    .map(value => value.trim())
    .filter(Boolean);
}

function readTextEntry(archive, entry) {
  return runUnzip(
    ["-p", archive, entry],
    { encoding: "utf8" }).stdout;
}

function readBinaryEntry(archive, entry) {
  return runUnzip(
    ["-p", archive, entry],
    {}).stdout;
}

function runUnzip(args, options) {
  const result = spawnSync(
    "unzip",
    args,
    {
      maxBuffer: 16 * 1024 * 1024,
      ...options
    });

  if (result.error) {
    fail(
      "Unable to execute unzip. GitHub Ubuntu release runners " +
      "must provide the unzip utility.");
  }

  if (result.status !== 0) {
    const stderr =
      Buffer.isBuffer(result.stderr)
        ? result.stderr.toString("utf8")
        : (result.stderr ?? "");
    fail(
      `unzip failed for ${args.join(" ")}: ${stderr.trim()}`);
  }

  return result;
}

function assertIncludes(entries, expected, message) {
  if (!entries.includes(expected)) {
    fail(
      `${message} Archive entries: ${entries.join(", ")}`);
  }
}

function requireXmlValue(xml, element, expected) {
  const escaped =
    expected.replace(
      /[.*+?^$\{\}()|[\]\\]/g,
      "\\$&");
  const pattern = new RegExp(
    `<${element}(?:\\s+[^>]*)?>\\s*${escaped}\\s*</${element}>`,
    "i");

  if (!pattern.test(xml)) {
    fail(
      `Expected <${element}> value '${expected}' in package nuspec.`);
  }
}

function requireAttribute(
  attributes,
  name,
  expected,
  label,
  options = {}) {
  const match = attributes.match(
    new RegExp(
      `\\b${name}\\s*=\\s*["']([^"']+)["']`,
      "i"));

  if (!match) {
    fail(`${label} attribute '${name}' is missing.`);
  }

  const actual = match[1];
  const equal = options.ignoreCase
    ? actual.toLowerCase() === expected.toLowerCase()
    : actual === expected;

  if (!equal) {
    fail(
      `${label} mismatch. Expected '${expected}', got '${actual}'.`);
  }
}

function fail(message) {
  console.error("NUGET PACKAGE AUDIT FAILED");
  console.error(`- ${message}`);
  process.exit(1);
}
