async function loadMetrics() {
  try {
    const response = await fetch("./engine-metrics.json", { cache: "no-store" });
    if (!response.ok) return;

    const metrics = await response.json();
    const size = document.querySelector("#actual-size");
    const deps = document.querySelector("#runtime-deps");
    const note = document.querySelector("#metrics-note");

    if (size) size.textContent = `${metrics.kib} KiB`;
    if (deps) deps.textContent = String(metrics.mandatoryThirdPartyRuntimePackageDependencies);

    if (note) {
      const targetState = metrics.withinDesignTarget ? "inside the design target" : "inside the hard ceiling";
      note.textContent =
        `Built from the deployed source: ${metrics.kib} KiB Core DLL, ${targetState}. Runtime throughput benchmarks will appear after the first meaningful walking skeleton.`;
    }
  } catch {
    // The source tree can be opened without generated deployment metrics.
  }
}

loadMetrics();
