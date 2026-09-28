import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const dllPath = path.join(root, "src", "Mayordomo.Core", "bin", "Release", "net10.0", "Mayordomo.Core.dll");
const projectPath = path.join(root, "src", "Mayordomo.Core", "Mayordomo.Core.csproj");

const designTargetBytes = 256 * 1024;
const hardCeilingBytes = 512 * 1024;

if (!fs.existsSync(dllPath)) {
  console.error(`ENGINE BUDGET CHECK FAILED: missing Release DLL at ${path.relative(root, dllPath)}`);
  process.exit(1);
}

if (!fs.existsSync(projectPath)) {
  console.error("ENGINE BUDGET CHECK FAILED: core project file is missing.");
  process.exit(1);
}

const bytes = fs.statSync(dllPath).size;
const project = fs.readFileSync(projectPath, "utf8");
const packageReferences = [...project.matchAll(/<PackageReference\b[^>]*Include=["']([^"']+)["']/gi)]
  .map(match => match[1]);

const metrics = {
  assembly: "Mayordomo.Core.dll",
  bytes,
  kib: Number((bytes / 1024).toFixed(2)),
  designTargetBytes,
  designTargetKib: designTargetBytes / 1024,
  hardCeilingBytes,
  hardCeilingKib: hardCeilingBytes / 1024,
  mandatoryThirdPartyRuntimePackageDependencies: packageReferences.length,
  packageReferences,
  withinDesignTarget: bytes <= designTargetBytes,
  withinHardCeiling: bytes <= hardCeilingBytes,
};

console.log(`Core assembly: ${metrics.kib} KiB`);
console.log(`Design target: ≤ ${metrics.designTargetKib} KiB`);
console.log(`Hard ceiling: ≤ ${metrics.hardCeilingKib} KiB`);
console.log(`Core PackageReference count: ${packageReferences.length}`);

if (packageReferences.length > 0) {
  console.error(`ENGINE BUDGET CHECK FAILED: Mayordomo.Core must have zero mandatory third-party runtime PackageReferences. Found: ${packageReferences.join(", ")}`);
  process.exit(1);
}

if (bytes > hardCeilingBytes) {
  console.error("ENGINE BUDGET CHECK FAILED: Core DLL exceeds the hard footprint ceiling.");
  process.exit(1);
}

if (bytes > designTargetBytes) {
  console.warn("ENGINE BUDGET WARNING: Core DLL exceeds the design target but remains below the hard ceiling.");
}

const jsonArgIndex = process.argv.indexOf("--json");
if (jsonArgIndex >= 0) {
  const outputPath = process.argv[jsonArgIndex + 1];
  if (!outputPath) {
    console.error("ENGINE BUDGET CHECK FAILED: --json requires an output path.");
    process.exit(2);
  }
  const absoluteOutput = path.resolve(root, outputPath);
  fs.mkdirSync(path.dirname(absoluteOutput), { recursive: true });
  fs.writeFileSync(absoluteOutput, JSON.stringify(metrics, null, 2) + "\n", "utf8");
  console.log(`Metrics written to ${path.relative(root, absoluteOutput)}`);
}

console.log("ENGINE BUDGET CHECK PASSED");
