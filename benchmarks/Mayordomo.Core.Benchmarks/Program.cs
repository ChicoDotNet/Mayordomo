using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using Mayordomo.Core;

namespace Mayordomo.Core.Benchmarks;

internal static class Program
{
  private const int DefaultIterations = 10000;
  private const int DefaultWarmupIterations = 1000;

  private static readonly JsonSerializerOptions SerializerOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
  };

  public static void Main()
  {
    var iterations = ReadPositiveInt(
      "MAYORDOMO_BENCHMARK_ITERATIONS",
      DefaultIterations);
    var warmupIterations = ReadPositiveInt(
      "MAYORDOMO_BENCHMARK_WARMUP",
      DefaultWarmupIterations);
    var commitSha =
      Environment.GetEnvironmentVariable(
        "MAYORDOMO_BENCHMARK_SHA")
      ?? "local";

    var fixture = CreateFixture();

    var invariantResult =
      MatchInvariantValidator.Validate(
        fixture.FinalState);

    if (!invariantResult.IsValid)
    {
      throw new InvalidOperationException(
        "Benchmark fixture violates engine invariants.");
    }

    var replayIterations = Math.Max(
      100,
      iterations / 20);

    BenchmarkResult[] results =
    [
      Measure(
        "canonical-state-hash",
        iterations,
        warmupIterations,
        () =>
        {
          var hash = MatchStateHasher.Compute(
            fixture.FinalState);
          GC.KeepAlive(hash.Value);
        }),
      Measure(
        "invariant-validation",
        iterations,
        warmupIterations,
        () =>
        {
          var result =
            MatchInvariantValidator.Validate(
              fixture.FinalState);
          GC.KeepAlive(result);
        }),
      Measure(
        "event-replay",
        replayIterations,
        Math.Max(10, warmupIterations / 20),
        () =>
        {
          var replayed = MatchEngine.Reduce(
            fixture.InitialState,
            fixture.Events);
          GC.KeepAlive(replayed);
        })
    ];

    var report = new BenchmarkReport(
      SchemaVersion: 1,
      CommitSha: commitSha,
      TimestampUtc: DateTimeOffset.UtcNow,
      Runtime: RuntimeInformation.FrameworkDescription,
      OperatingSystem: RuntimeInformation.OSDescription,
      ProcessArchitecture:
        RuntimeInformation.ProcessArchitecture.ToString(),
      ProcessorCount: Environment.ProcessorCount,
      ServerGc: GCSettings.IsServerGC,
      MachineName: Environment.MachineName,
      RunnerName:
        Environment.GetEnvironmentVariable("RUNNER_NAME"),
      RunnerOs:
        Environment.GetEnvironmentVariable("RUNNER_OS"),
      RunnerArchitecture:
        Environment.GetEnvironmentVariable("RUNNER_ARCH"),
      Scenario:
        "4 participants; positions; held items; deck; deterministic RNG; movement; phases; turn rotation",
      EventCount: fixture.Events.Count,
      Results: results);

    var json = JsonSerializer.Serialize(
      report,
      SerializerOptions);

    Console.WriteLine(json);
  }

  private static BenchmarkResult Measure(
    string name,
    int iterations,
    int warmupIterations,
    Action operation)
  {
    for (var index = 0;
         index < warmupIterations;
         index++)
    {
      operation();
    }

    ForceGc();

    var allocatedBefore =
      GC.GetAllocatedBytesForCurrentThread();
    var stopwatch = Stopwatch.StartNew();

    for (var index = 0;
         index < iterations;
         index++)
    {
      operation();
    }

    stopwatch.Stop();

    var allocatedBytes =
      GC.GetAllocatedBytesForCurrentThread()
      - allocatedBefore;
    var elapsedSeconds =
      stopwatch.Elapsed.TotalSeconds;
    var operationsPerSecond =
      iterations / elapsedSeconds;
    var microsecondsPerOperation =
      stopwatch.Elapsed.TotalMilliseconds
      * 1000d
      / iterations;
    var bytesPerOperation =
      (double)allocatedBytes
      / iterations;

    return new BenchmarkResult(
      name,
      iterations,
      stopwatch.Elapsed.TotalMilliseconds,
      operationsPerSecond,
      microsecondsPerOperation,
      allocatedBytes,
      bytesPerOperation);
  }

  private static void ForceGc()
  {
    GC.Collect(
      generation: 2,
      GCCollectionMode.Forced,
      blocking: true,
      compacting: true);
    GC.WaitForPendingFinalizers();
    GC.Collect(
      generation: 2,
      GCCollectionMode.Forced,
      blocking: true,
      compacting: true);
  }

  private static int ReadPositiveInt(
    string variableName,
    int fallback)
  {
    var raw =
      Environment.GetEnvironmentVariable(
        variableName);

    if (string.IsNullOrWhiteSpace(raw))
    {
      return fallback;
    }

    if (!int.TryParse(
          raw,
          NumberStyles.Integer,
          CultureInfo.InvariantCulture,
          out var parsed) ||
        parsed <= 0)
    {
      throw new InvalidOperationException(
        $"Environment variable '{variableName}' must contain a positive integer.");
    }

    return parsed;
  }

  private static BenchmarkFixture CreateFixture()
  {
    var initial = MatchState.Create(
      MatchId.Create("benchmark-match"),
      randomSeed: 987654321UL);
    var current = initial;
    var events = new List<MatchEvent>();
    ParticipantId[] participants =
    [
      ParticipantId.Create("participant-a"),
      ParticipantId.Create("participant-b"),
      ParticipantId.Create("participant-c"),
      ParticipantId.Create("participant-d")
    ];

    for (var index = 0;
         index < participants.Length;
         index++)
    {
      current = Apply(
        events,
        MatchEngine.Execute(
          current,
          JoinParticipantCommand.Create(
            CommandId.Create($"join-{index}"),
            current.MatchId,
            current.Revision,
            participants[index])));
    }

    for (var index = 0;
         index < participants.Length;
         index++)
    {
      current = Apply(
        events,
        MatchEngine.Execute(
          current,
          PlaceParticipantCommand.Create(
            CommandId.Create($"place-{index}"),
            current.MatchId,
            current.Revision,
            participants[index],
            PositionId.Create(
              $"position-{index}-0"))));
    }

    for (var index = 0;
         index < participants.Length;
         index++)
    {
      current = Apply(
        events,
        MatchEngine.Execute(
          current,
          GrantParticipantItemCommand.Create(
            CommandId.Create($"grant-{index}"),
            current.MatchId,
            current.Revision,
            participants[index],
            ParticipantItemId.Create(
              $"item-{index}"))));
    }

    var deckItems = Enumerable
      .Range(0, 24)
      .Select(index =>
        DeckItemId.Create(
          $"deck-item-{index:D2}"))
      .ToArray();

    current = Apply(
      events,
      MatchEngine.Execute(
        current,
        CreateDeckCommand.Create(
          CommandId.Create("create-deck"),
          current.MatchId,
          current.Revision,
          DeckId.Create("deck-main"),
          deckItems)));

    current = Apply(
      events,
      MatchEngine.Execute(
        current,
        StartTurnCommand.Create(
          CommandId.Create("start-turn"),
          current.MatchId,
          current.Revision,
          participants[0],
          PhaseId.Create("phase-main"))));

    for (var turn = 0;
         turn < 12;
         turn++)
    {
      var activeTurn = current.Turn
        ?? throw new InvalidOperationException(
          "Benchmark fixture lost its active turn.");

      var randomTransition =
        MatchEngine.Execute(
          current,
          GenerateRandomIntCommand.Create(
            CommandId.Create(
              $"random-{turn:D2}"),
            current.MatchId,
            current.Revision,
            minInclusive: 1,
            maxExclusive: 7));

      var randomEvent =
        randomTransition.Events[0]
          as RandomIntGeneratedEvent
        ?? throw new InvalidOperationException(
          "Benchmark fixture expected a random event.");

      current = Apply(
        events,
        randomTransition);

      current = Apply(
        events,
        MatchEngine.Execute(
          current,
          MoveParticipantCommand.Create(
            CommandId.Create(
              $"move-{turn:D2}"),
            current.MatchId,
            current.Revision,
            activeTurn.ParticipantId,
            PositionId.Create(
              $"position-{activeTurn.ParticipantId.Value}-{turn:D2}-{randomEvent.Value}"),
            randomEvent.Value)));

      if (turn % 2 == 0)
      {
        current = Apply(
          events,
          MatchEngine.Execute(
            current,
            DrawDeckItemCommand.Create(
              CommandId.Create(
                $"draw-{turn:D2}"),
              current.MatchId,
              current.Revision,
              DeckId.Create("deck-main"))));
      }

      current = Apply(
        events,
        MatchEngine.Execute(
          current,
          ChangeTurnPhaseCommand.Create(
            CommandId.Create(
              $"phase-{turn:D2}"),
            current.MatchId,
            current.Revision,
            activeTurn.ParticipantId,
            PhaseId.Create("phase-resolve"))));

      current = Apply(
        events,
        MatchEngine.Execute(
          current,
          EndTurnCommand.Create(
            CommandId.Create(
              $"end-{turn:D2}"),
            current.MatchId,
            current.Revision,
            activeTurn.ParticipantId,
            PhaseId.Create("phase-main"))));
    }

    return new BenchmarkFixture(
      initial,
      current,
      events.ToArray());
  }

  private static MatchState Apply(
    List<MatchEvent> events,
    MatchTransition transition)
  {
    events.AddRange(
      transition.Events);

    return transition.State;
  }

  private sealed record BenchmarkFixture(
    MatchState InitialState,
    MatchState FinalState,
    IReadOnlyList<MatchEvent> Events);

  private sealed record BenchmarkReport(
    int SchemaVersion,
    string CommitSha,
    DateTimeOffset TimestampUtc,
    string Runtime,
    string OperatingSystem,
    string ProcessArchitecture,
    int ProcessorCount,
    bool ServerGc,
    string MachineName,
    string? RunnerName,
    string? RunnerOs,
    string? RunnerArchitecture,
    string Scenario,
    int EventCount,
    IReadOnlyList<BenchmarkResult> Results);

  private sealed record BenchmarkResult(
    string Name,
    int Operations,
    double ElapsedMilliseconds,
    double OperationsPerSecond,
    double MicrosecondsPerOperation,
    long AllocatedBytes,
    double BytesPerOperation);
}
