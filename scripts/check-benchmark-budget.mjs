import fs from "node:fs";
import { pathToFileURL } from "node:url";

export const DEFAULT_BUDGETS = Object.freeze({
  "canonical-state-hash": Object.freeze({
    maxMicrosecondsPerOperation: 250,
    maxBytesPerOperation: 64 * 1024,
  }),
  "invariant-validation": Object.freeze({
    maxMicrosecondsPerOperation: 250,
    maxBytesPerOperation: 64 * 1024,
  }),
  "event-replay": Object.freeze({
    maxMicrosecondsPerOperation: 10_000,
    maxBytesPerOperation: 2 * 1024 * 1024,
  }),
});

export function evaluateBenchmarkReport(
  report,
  budgets = DEFAULT_BUDGETS,
) {
  const violations = [];

  if (report?.schemaVersion !== 1) {
    return {
      passed: false,
      violations: [
        `Unsupported benchmark report schema version '${report?.schemaVersion}'.`,
      ],
    };
  }

  if (!Array.isArray(report.results)) {
    return {
      passed: false,
      violations: ["Benchmark report results must be an array."],
    };
  }

  const resultsByName = new Map(
    report.results.map((result) => [result.name, result]),
  );

  for (const [name, budget] of Object.entries(budgets)) {
    const result = resultsByName.get(name);

    if (!result) {
      violations.push(
        `Benchmark '${name}' is missing from the report.`,
      );
      continue;
    }

    const latency = result.microsecondsPerOperation;
    const allocation = result.bytesPerOperation;

    if (!Number.isFinite(latency) || latency < 0) {
      violations.push(
        `Benchmark '${name}' has invalid latency '${latency}'.`,
      );
    } else if (
      latency > budget.maxMicrosecondsPerOperation
    ) {
      violations.push(
        `Benchmark '${name}' latency ${latency} us/op exceeds hard ceiling ${budget.maxMicrosecondsPerOperation} us/op.`,
      );
    }

    if (!Number.isFinite(allocation) || allocation < 0) {
      violations.push(
        `Benchmark '${name}' has invalid allocation '${allocation}'.`,
      );
    } else if (
      allocation > budget.maxBytesPerOperation
    ) {
      violations.push(
        `Benchmark '${name}' allocation ${allocation} B/op exceeds hard ceiling ${budget.maxBytesPerOperation} B/op.`,
      );
    }
  }

  return {
    passed: violations.length === 0,
    violations,
  };
}

function runCli() {
  const reportPath = process.argv[2];

  if (!reportPath) {
    console.error(
      "Usage: node scripts/check-benchmark-budget.mjs <benchmark-report.json>",
    );
    process.exitCode = 2;
    return;
  }

  let report;

  try {
    report = JSON.parse(
      fs.readFileSync(reportPath, "utf8"),
    );
  } catch (error) {
    console.error(
      `Unable to read benchmark report '${reportPath}': ${error.message}`,
    );
    process.exitCode = 2;
    return;
  }

  const evaluation =
    evaluateBenchmarkReport(report);

  if (evaluation.passed) {
    console.log(
      "Benchmark hard ceilings satisfied.",
    );
    return;
  }

  console.error(
    "Benchmark hard ceilings failed:",
  );

  for (const violation of evaluation.violations) {
    console.error(`- ${violation}`);
  }

  process.exitCode = 1;
}

if (
  process.argv[1] &&
  import.meta.url === pathToFileURL(process.argv[1]).href
) {
  runCli();
}
