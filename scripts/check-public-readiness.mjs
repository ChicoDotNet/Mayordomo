import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const errors = [];

function exists(relativePath) {
  return fs.existsSync(path.join(root, relativePath));
}

function read(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

function walk(relativePath) {
  const absolute = path.join(root, relativePath);
  if (!fs.existsSync(absolute)) return [];

  const output = [];
  for (const entry of fs.readdirSync(absolute, { withFileTypes: true })) {
    const child = path.join(relativePath, entry.name);
    if (entry.isDirectory()) {
      output.push(...walk(child));
    } else {
      output.push(child);
    }
  }
  return output;
}

const required = [
  "LICENSE",
  "NOTICE.md",
  "README.md",
  "README.es-MX.md",
  "CONTRIBUTING.md",
  "CODE_OF_CONDUCT.md",
  "SECURITY.md",
  "SUPPORT.md",
  "GOVERNANCE.md",
  "DCO.md",
  "CHANGELOG.md",
  "AGENTS.md",
  "docs/DEVELOPMENT.md",
  "docs/RELEASING.md",
  "docs/architecture/clean-room-board-game-engine.md",
  "docs/architecture/0001-public-engine-private-game-boundary.md",
  "docs/roadmap/README.md",
  ".github/CODEOWNERS",
  ".github/pull_request_template.md",
];

for (const requiredPath of required) {
  if (!exists(requiredPath)) {
    errors.push(`Missing public-readiness file: ${requiredPath}`);
  }
}

const forbiddenPaths = [
  "src/Mayordomo.Game",
  "src/Mayordomo.Studio",
  "Mayordomo.Game",
  "Mayordomo.Studio",
  "studio",
  "apps/studio",
  "clients/web",
  "clients/unity",
  "clients/avalonia",
  "content/mayordomo",
  "assets/mayordomo",
];

for (const forbiddenPath of forbiddenPaths) {
  if (exists(forbiddenPath)) {
    errors.push(`Private product path must not exist in engine repository: ${forbiddenPath}`);
  }
}

if (exists("LICENSE") && !read("LICENSE").startsWith("MIT License")) {
  errors.push("LICENSE is not the expected MIT license text.");
}

if (exists("Directory.Build.props") &&
    !read("Directory.Build.props").includes("<PackageLicenseExpression>MIT</PackageLicenseExpression>")) {
  errors.push("Directory.Build.props must declare MIT package licensing.");
}

if (exists("README.md")) {
  const readme = read("README.md");
  if (!readme.includes("ChicoDotNet/Mayordomo.Game")) {
    errors.push("README must document the private product repository boundary.");
  }
  if (!readme.includes("Mayordomo Engine → Mayordomo.Game") &&
      !readme.includes("Mayordomo Engine -> Mayordomo.Game")) {
    errors.push("README must explicitly document the forbidden reverse dependency.");
  }
}

if (exists("NOTICE.md") && !read("NOTICE.md").includes("public-safe")) {
  errors.push("NOTICE.md must document the public-safe history rule.");
}

if (exists(".gitignore") && !read(".gitignore").includes(".env")) {
  errors.push(".gitignore must ignore environment-secret files.");
}

const sourceFiles = walk("src");
for (const sourcePath of sourceFiles) {
  if (!/\.(cs|csproj|props|targets)$/i.test(sourcePath)) continue;

  const source = read(sourcePath);

  if (/Mayordomo\.Game|Mayordomo\.Studio/i.test(source)) {
    errors.push(`Engine source references private product identity: ${sourcePath}`);
  }

  if (/\b(Alfol[ií]|Devorador|Diezmo|Tithe|Tentaci[oó]n)\b/i.test(source)) {
    errors.push(`Engine source contains Mayordomo-specific game vocabulary: ${sourcePath}`);
  }
}

if (errors.length > 0) {
  console.error("PUBLIC-READINESS CHECK FAILED");
  for (const error of errors) {
    console.error(`- ${error}`);
  }
  process.exit(1);
}

console.log("PUBLIC-READINESS STRUCTURAL GATE PASSED");
console.log("This gate supplements, but does not replace, human history/provenance/security review.");
