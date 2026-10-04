using Xunit;

namespace Mayordomo.Core.Tests;

public sealed class DeterminismSoakTests
{
  [Fact]
  public void Hundreds_of_headless_scenarios_are_repeatable_replayable_and_valid()
  {
    const int scenarioCount = 128;

    for (var scenario = 0; scenario < scenarioCount; scenario++)
    {
      var seed = unchecked(
        ((ulong)scenario * 104729UL) + 17UL);

      var first = RunScenario(
        scenario,
        seed);
      var second = RunScenario(
        scenario,
        seed);
      var replayed = MatchEngine.Reduce(
        first.InitialState,
        first.Events);

      Assert.Equal(
        first.Events
          .Select(EventSignature)
          .ToArray(),
        second.Events
          .Select(EventSignature)
          .ToArray());

      var firstHash = MatchStateHasher.Compute(
        first.FinalState);
      var secondHash = MatchStateHasher.Compute(
        second.FinalState);
      var replayHash = MatchStateHasher.Compute(
        replayed);

      Assert.Equal(firstHash, secondHash);
      Assert.Equal(firstHash, replayHash);

      Assert.True(
        MatchInvariantValidator.Validate(
          first.FinalState).IsValid);
      Assert.True(
        MatchInvariantValidator.Validate(
          replayed).IsValid);
    }
  }

  private static ScenarioResult RunScenario(
    int scenario,
    ulong seed)
  {
    var initial = MatchState.Create(
      MatchId.Create($"soak-{scenario:D3}"),
      seed);
    var current = initial;
    var events = new List<MatchEvent>();
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
        events,
        MatchEngine.Execute(
          current,
          JoinParticipantCommand.Create(
            CommandId.Create($"join-{index}"),
            current.MatchId,
            current.Revision,
            participants[index])));
    }

    for (var index = 0; index < participants.Length; index++)
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
            PositionId.Create($"position-{index}-0"))));
    }

    for (var index = 0; index < participants.Length; index++)
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
            ParticipantItemId.Create($"item-{index}"))));
    }

    var deckItems = Enumerable
      .Range(0, 40)
      .Select(index =>
        DeckItemId.Create($"deck-item-{index:D2}"))
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

    for (var turn = 0; turn < 24; turn++)
    {
      var activeTurn = current.Turn
        ?? throw new InvalidOperationException(
          "Synthetic soak scenario lost its active turn.");

      var randomTransition = MatchEngine.Execute(
        current,
        GenerateRandomIntCommand.Create(
          CommandId.Create($"random-{turn:D2}"),
          current.MatchId,
          current.Revision,
          minInclusive: 1,
          maxExclusive: 7));
      var randomEvent = Assert.IsType<RandomIntGeneratedEvent>(
        Assert.Single(randomTransition.Events));

      current = Apply(
        events,
        randomTransition);

      current = Apply(
        events,
        MatchEngine.Execute(
          current,
          MoveParticipantCommand.Create(
            CommandId.Create($"move-{turn:D2}"),
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
              CommandId.Create($"draw-{turn:D2}"),
              current.MatchId,
              current.Revision,
              DeckId.Create("deck-main"))));
      }

      current = Apply(
        events,
        MatchEngine.Execute(
          current,
          ChangeTurnPhaseCommand.Create(
            CommandId.Create($"phase-{turn:D2}"),
            current.MatchId,
            current.Revision,
            activeTurn.ParticipantId,
            PhaseId.Create("phase-resolve"))));

      current = Apply(
        events,
        MatchEngine.Execute(
          current,
          EndTurnCommand.Create(
            CommandId.Create($"end-{turn:D2}"),
            current.MatchId,
            current.Revision,
            activeTurn.ParticipantId,
            PhaseId.Create("phase-main"))));
    }

    return new ScenarioResult(
      initial,
      current,
      events.ToArray());
  }

  private static MatchState Apply(
    List<MatchEvent> events,
    MatchTransition transition)
  {
    events.AddRange(transition.Events);
    return transition.State;
  }

  private static string EventSignature(
    MatchEvent domainEvent)
  {
    return domainEvent switch
    {
      ParticipantJoinedEvent joined =>
        $"join|{joined.CommandId}|{joined.MatchId}|{joined.Revision}|{joined.ParticipantId}",
      ParticipantPlacedEvent placed =>
        $"place|{placed.CommandId}|{placed.MatchId}|{placed.Revision}|{placed.ParticipantId}|{placed.PositionId}",
      ParticipantMovedEvent moved =>
        $"move|{moved.CommandId}|{moved.MatchId}|{moved.Revision}|{moved.ParticipantId}|{moved.FromPositionId}|{moved.ToPositionId}|{moved.Distance}",
      ParticipantItemGrantedEvent granted =>
        $"grant|{granted.CommandId}|{granted.MatchId}|{granted.Revision}|{granted.ParticipantId}|{granted.ItemId}",
      DeckCreatedEvent created =>
        $"deck-created|{created.CommandId}|{created.MatchId}|{created.Revision}|{created.DeckId}|{string.Join(',', created.Items.Select(item => item.Value))}",
      DeckItemDrawnEvent drawn =>
        $"deck-drawn|{drawn.CommandId}|{drawn.MatchId}|{drawn.Revision}|{drawn.DeckId}|{drawn.ItemId}|{drawn.RandomStateAfter.Value}",
      TurnStartedEvent started =>
        $"turn-started|{started.CommandId}|{started.MatchId}|{started.Revision}|{started.ParticipantId}|{started.PhaseId}",
      RandomIntGeneratedEvent generated =>
        $"random|{generated.CommandId}|{generated.MatchId}|{generated.Revision}|{generated.MinInclusive}|{generated.MaxExclusive}|{generated.Value}|{generated.RandomStateAfter.Value}",
      TurnPhaseChangedEvent changed =>
        $"phase|{changed.CommandId}|{changed.MatchId}|{changed.Revision}|{changed.ParticipantId}|{changed.PreviousPhaseId}|{changed.PhaseId}",
      TurnEndedEvent ended =>
        $"turn-ended|{ended.CommandId}|{ended.MatchId}|{ended.Revision}|{ended.ParticipantId}",
      _ => throw new InvalidOperationException(
        $"Unexpected soak event type '{domainEvent.GetType().Name}'.")
    };
  }

  private sealed record ScenarioResult(
    MatchState InitialState,
    MatchState FinalState,
    IReadOnlyList<MatchEvent> Events);
}
