using Xunit;

namespace Mayordomo.Core.Tests;

public sealed class PropertyFuzzTests
{
  private static readonly DeckId MainDeck = DeckId.Create("deck-main");
  private static readonly PhaseId MainPhase = PhaseId.Create("phase-main");

  [Fact]
  public void Seeded_command_sequences_preserve_core_properties()
  {
    const int scenarioCount = 48;
    const int stepsPerScenario = 96;

    for (var scenario = 0; scenario < scenarioCount; scenario++)
    {
      var first = RunScenario(
        scenario,
        stepsPerScenario);
      var second = RunScenario(
        scenario,
        stepsPerScenario);

      Assert.Equal(
        first.FinalHash,
        second.FinalHash);
      Assert.Equal(
        first.FinalRevision,
        second.FinalRevision);
      Assert.Equal(
        first.EventCount,
        second.EventCount);
    }
  }

  private static ScenarioResult RunScenario(
    int scenario,
    int steps)
  {
    var seed = unchecked(
      ((ulong)scenario * 1_000_003UL) +
      0xA5A5A5A5UL);
    var fuzz = DeterministicRandom.Create(seed);
    var initial = MatchState.Create(
      MatchId.Create($"fuzz-{scenario:D3}"),
      seed ^ 0x9E3779B97F4A7C15UL);
    var current = initial;
    var history = new List<MatchEvent>();
    ParticipantId[] participants =
    [
      ParticipantId.Create("participant-a"),
      ParticipantId.Create("participant-b"),
      ParticipantId.Create("participant-c"),
      ParticipantId.Create("participant-d")
    ];

    for (var index = 0; index < participants.Length; index++)
    {
      current = Apply(
        history,
        MatchEngine.Execute(
          current,
          JoinParticipantCommand.Create(
            CommandId.Create($"setup-join-{index}"),
            current.MatchId,
            current.Revision,
            participants[index])));
    }

    for (var index = 0; index < participants.Length; index++)
    {
      current = Apply(
        history,
        MatchEngine.Execute(
          current,
          PlaceParticipantCommand.Create(
            CommandId.Create($"setup-place-{index}"),
            current.MatchId,
            current.Revision,
            participants[index],
            PositionId.Create($"setup-position-{index}"))));
    }

    var cards = Enumerable
      .Range(0, 200)
      .Select(index =>
        DeckItemId.Create($"card-{index:D3}"))
      .ToArray();

    current = Apply(
      history,
      MatchEngine.Execute(
        current,
        CreateDeckCommand.Create(
          CommandId.Create("setup-deck"),
          current.MatchId,
          current.Revision,
          MainDeck,
          cards)));

    current = Apply(
      history,
      MatchEngine.Execute(
        current,
        StartTurnCommand.Create(
          CommandId.Create("setup-turn"),
          current.MatchId,
          current.Revision,
          participants[0],
          MainPhase)));

    AssertHealthy(
      initial,
      current,
      history);

    for (var step = 0; step < steps; step++)
    {
      var before = current;
      var command = CreateFuzzCommand(
        current,
        fuzz,
        scenario,
        step);

      var transition = Execute(
        current,
        command);

      history.AddRange(transition.Events);
      current = transition.State;

      var validation =
        MatchInvariantValidator.Validate(current);

      Assert.True(
        validation.IsValid,
        $"Invariant failure at scenario {scenario}, step {step}: " +
        string.Join(
          " | ",
          validation.Violations.Select(
            violation =>
              $"{violation.Code}:{violation.Subject}")));

      var retry = Execute(
        current,
        command);

      Assert.True(retry.IsDuplicate);
      Assert.Empty(retry.Events);
      Assert.Same(current, retry.State);

      if (step % 12 == 0)
      {
        var conflicting =
          GenerateRandomIntCommand.Create(
            CommandIdOf(command),
            current.MatchId,
            current.Revision,
            minInclusive: 101,
            maxExclusive: 103);

        Assert.Throws<CommandIdConflictException>(
          () => Execute(
            current,
            conflicting));
      }

      if (step % 16 == 0)
      {
        var stale =
          GenerateRandomIntCommand.Create(
            CommandId.Create(
              $"stale-{scenario:D3}-{step:D3}"),
            current.MatchId,
            before.Revision,
            minInclusive: 1,
            maxExclusive: 2);

        Assert.Throws<RevisionConflictException>(
          () => MatchEngine.Execute(
            current,
            stale));
      }

      if (step % 8 == 0)
      {
        AssertHealthy(
          initial,
          current,
          history);
      }
    }

    AssertHealthy(
      initial,
      current,
      history);

    return new ScenarioResult(
      MatchStateHasher.Compute(current),
      current.Revision,
      history.Count);
  }

  private static object CreateFuzzCommand(
    MatchState state,
    DeterministicRandom fuzz,
    int scenario,
    int step)
  {
    var active = state.Turn
      ?? throw new InvalidOperationException(
        "Fuzz scenario lost its active turn.");
    var commandId = CommandId.Create(
      $"fuzz-{scenario:D3}-{step:D3}");
    var choice = fuzz.NextInt32(0, 8);

    return choice switch
    {
      0 => GenerateRandomIntCommand.Create(
        commandId,
        state.MatchId,
        state.Revision,
        minInclusive: 1,
        maxExclusive: 13),

      1 => MoveParticipantCommand.Create(
        commandId,
        state.MatchId,
        state.Revision,
        active.ParticipantId,
        PositionId.Create(
          $"move-{scenario:D3}-{step:D3}"),
        distance: fuzz.NextInt32(1, 13)),

      2 => ChangeTurnPhaseCommand.Create(
        commandId,
        state.MatchId,
        state.Revision,
        active.ParticipantId,
        PhaseId.Create(
          fuzz.NextInt32(0, 2) == 0
            ? "phase-a"
            : "phase-b")),

      3 => EndTurnCommand.Create(
        commandId,
        state.MatchId,
        state.Revision,
        active.ParticipantId,
        MainPhase),

      4 => GrantParticipantItemCommand.Create(
        commandId,
        state.MatchId,
        state.Revision,
        active.ParticipantId,
        ParticipantItemId.Create(
          $"item-{scenario:D3}-{step:D3}")),

      5 => CreateInventoryCommand(
        state,
        fuzz,
        commandId,
        active.ParticipantId,
        scenario,
        step),

      6 => RelocateParticipantCommand.Create(
        commandId,
        state.MatchId,
        state.Revision,
        active.ParticipantId,
        PositionId.Create(
          $"relocate-{scenario:D3}-{step:D3}")),

      _ => CreateDrawOrRandomCommand(
        state,
        commandId)
    };
  }

  private static object CreateInventoryCommand(
    MatchState state,
    DeterministicRandom fuzz,
    CommandId commandId,
    ParticipantId participantId,
    int scenario,
    int step)
  {
    var inventory = state.HeldItems[participantId];

    if (inventory.Count == 0)
    {
      return GrantParticipantItemCommand.Create(
        commandId,
        state.MatchId,
        state.Revision,
        participantId,
        ParticipantItemId.Create(
          $"fallback-item-{scenario:D3}-{step:D3}"));
    }

    var item = inventory[
      fuzz.NextInt32(
        0,
        inventory.Count)];

    return RevokeParticipantItemCommand.Create(
      commandId,
      state.MatchId,
      state.Revision,
      participantId,
      item);
  }

  private static object CreateDrawOrRandomCommand(
    MatchState state,
    CommandId commandId)
  {
    if (state.Decks.TryGetValue(
          MainDeck,
          out var deck) &&
        deck.RemainingItems.Count > 0)
    {
      return DrawDeckItemCommand.Create(
        commandId,
        state.MatchId,
        state.Revision,
        MainDeck);
    }

    return GenerateRandomIntCommand.Create(
      commandId,
      state.MatchId,
      state.Revision,
      minInclusive: 1,
      maxExclusive: 7);
  }

  private static MatchTransition Execute(
    MatchState state,
    object command)
  {
    return command switch
    {
      GenerateRandomIntCommand typed =>
        MatchEngine.Execute(state, typed),
      MoveParticipantCommand typed =>
        MatchEngine.Execute(state, typed),
      ChangeTurnPhaseCommand typed =>
        MatchEngine.Execute(state, typed),
      EndTurnCommand typed =>
        MatchEngine.Execute(state, typed),
      GrantParticipantItemCommand typed =>
        MatchEngine.Execute(state, typed),
      RevokeParticipantItemCommand typed =>
        MatchEngine.Execute(state, typed),
      RelocateParticipantCommand typed =>
        MatchEngine.Execute(state, typed),
      DrawDeckItemCommand typed =>
        MatchEngine.Execute(state, typed),
      _ => throw new InvalidOperationException(
        $"Unsupported fuzz command '{command.GetType().Name}'.")
    };
  }

  private static CommandId CommandIdOf(
    object command)
  {
    return command switch
    {
      GenerateRandomIntCommand typed =>
        typed.CommandId,
      MoveParticipantCommand typed =>
        typed.CommandId,
      ChangeTurnPhaseCommand typed =>
        typed.CommandId,
      EndTurnCommand typed =>
        typed.CommandId,
      GrantParticipantItemCommand typed =>
        typed.CommandId,
      RevokeParticipantItemCommand typed =>
        typed.CommandId,
      RelocateParticipantCommand typed =>
        typed.CommandId,
      DrawDeckItemCommand typed =>
        typed.CommandId,
      _ => throw new InvalidOperationException(
        $"Unsupported fuzz command '{command.GetType().Name}'.")
    };
  }

  private static MatchState Apply(
    List<MatchEvent> history,
    MatchTransition transition)
  {
    history.AddRange(transition.Events);
    return transition.State;
  }

  private static void AssertHealthy(
    MatchState initial,
    MatchState current,
    IReadOnlyList<MatchEvent> history)
  {
    var replayed = MatchEngine.Reduce(
      initial,
      history);
    var validation =
      MatchInvariantValidator.Validate(replayed);

    Assert.True(validation.IsValid);
    Assert.Equal(
      MatchStateHasher.Compute(current),
      MatchStateHasher.Compute(replayed));
    Assert.Equal(
      current.Revision,
      replayed.Revision);
  }

  private sealed record ScenarioResult(
    StateHash FinalHash,
    Revision FinalRevision,
    int EventCount);
}
