import assert from "node:assert/strict";
import test from "node:test";

import { evaluateBenchmarkReport } from "./check-benchmark-budget.mjs";

const budgets = {
  "canonical-state-hash": {
    maxMicrosecondsPerOperation: 100,
    maxBytesPerOperation: 1000,
  },
  "event-replay": {
    maxMicrosecondsPerOperation: 1000,
    maxBytesPerOperation: 10000,
  },
};

test("accepts a complete report within every hard ceiling", () => {
  const result = evaluateBenchmarkReport({
    schemaVersion: 1,
    results: [
      {
        name: "canonical-state-hash",
        microsecondsPerOperation: 20,
        bytesPerOperation: 500,
      },
      {
        name: "event-replay",
        microsecondsPerOperation: 500,
        bytesPerOperation: 5000,
      },
    ],
  }, budgets);

  assert.equal(result.passed, true);
  assert.deepEqual(result.violations, []);
});

test("reports latency and allocation regressions deterministically", () => {
  const result = evaluateBenchmarkReport({
    schemaVersion: 1,
    results: [
      {
        name: "canonical-state-hash",
        microsecondsPerOperation: 120,
        bytesPerOperation: 1500,
      },
      {
        name: "event-replay",
        microsecondsPerOperation: 500,
        bytesPerOperation: 5000,
      },
    ],
  }, budgets);

  assert.equal(result.passed, false);
  assert.equal(result.violations.length, 2);
  assert.match(result.violations[0], /canonical-state-hash.*latency/i);
  assert.match(result.violations[1], /canonical-state-hash.*allocation/i);
});

test("fails closed when a required benchmark metric is missing", () => {
  const result = evaluateBenchmarkReport({
    schemaVersion: 1,
    results: [
      {
        name: "canonical-state-hash",
        microsecondsPerOperation: 20,
        bytesPerOperation: 500,
      },
    ],
  }, budgets);

  assert.equal(result.passed, false);
  assert.equal(result.violations.length, 1);
  assert.match(result.violations[0], /event-replay.*missing/i);
});

test("rejects unknown benchmark report schemas", () => {
  const result = evaluateBenchmarkReport({
    schemaVersion: 2,
    results: [],
  }, budgets);

  assert.equal(result.passed, false);
  assert.deepEqual(
    result.violations,
    ["Unsupported benchmark report schema version '2'."],
  );
});
