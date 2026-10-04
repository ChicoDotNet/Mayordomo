using Xunit;

namespace Mayordomo.Core.Tests;

public sealed class GoldenReplayCaptureTests
{
  [Fact]
  public void Capture_v1_golden_event_history_and_final_hash()
  {
    var initial = MatchState.Create(
      MatchId.Create("golden-v1"),
      randomSeed: 424242UL);
    var current = initial;
    var history = new List<MatchEvent>();
    var participantA = ParticipantId.Create("participant-a");
    var participantB = ParticipantId.Create("participant-b");
    var mainPhase = PhaseId.Create("phase-main");

    current = Apply(
      history,
      MatchEngine.Execute(
        current,
        JoinParticipantCommand.Create(
          CommandId.Create("join-a"),
          current.MatchId,
          current.Revision,
          participantA)));

    current = Apply(
      history,
      MatchEngine.Execute(
        current,
        JoinParticipantCommand.Create(
          CommandId.Create("join-b"),
          current.MatchId,
          current.Revision,
          participantB)));

    current = Apply(
      history,
      MatchEngine.Execute(
        current,
        PlaceParticipantCommand.Create(
          CommandId.Create("place-a"),
          current.MatchId,
          current.Revision,
          participantA,
          PositionId.Create("position-a"))));

    current = Apply(
      history,
      MatchEngine.Execute(
        current,
        PlaceParticipantCommand.Create(
          CommandId.Create("place-b"),
          current.MatchId,
          current.Revision,
          participantB,
          PositionId.Create("position-b"))));

    current = Apply(
      history,
      MatchEngine.Execute(
        current,
        GrantParticipantItemCommand.Create(
          CommandId.Create("grant-a"),
          current.MatchId,
          current.Revision,
          participantA,
          ParticipantItemId.Create("item-a"))));

    current = Apply(
      history,
      MatchEngine.Execute(
        current,
        CreateDeckCommand.Create(
          CommandId.Create("deck-create"),
          current.MatchId,
          current.Revision,
          DeckId.Create("deck-main"),
          [
            DeckItemId.Create("card-01"),
            DeckItemId.Create("card-02"),
            DeckItemId.Create("card-03"),
            DeckItemId.Create("card-04"),
            DeckItemId.Create("card-05")
          ])));

    current = Apply(
      history,
      MatchEngine.Execute(
        current,
        StartTurnCommand.Create(
          CommandId.Create("turn-start"),
          current.MatchId,
          current.Revision,
          participantA,
          mainPhase)));

    var randomTransition = MatchEngine.Execute(
      current,
      GenerateRandomIntCommand.Create(
        CommandId.Create("random-1"),
        current.MatchId,
        current.Revision,
        minInclusive: 1,
        maxExclusive: 7));
    var randomEvent = Assert.IsType<RandomIntGeneratedEvent>(
      Assert.Single(randomTransition.Events));
    current = Apply(history, randomTransition);

    current = Apply(
      history,
      MatchEngine.Execute(
        current,
        MoveParticipantCommand.Create(
          CommandId.Create("move-a"),
          current.MatchId,
          current.Revision,
          participantA,
          PositionId.Create("position-a-moved"),
          randomEvent.Value)));

    current = Apply(
      history,
      MatchEngine.Execute(
        current,
        DrawDeckItemCommand.Create(
          CommandId.Create("draw-1"),
          current.MatchId,
          current.Revision,
          DeckId.Create("deck-main"))));

    current = Apply(
      history,
      MatchEngine.Execute(
        current,
        ChangeTurnPhaseCommand.Create(
          CommandId.Create("phase-change"),
          current.MatchId,
          current.Revision,
          participantA,
          PhaseId.Create("phase-resolve"))));

    current = Apply(
      history,
      MatchEngine.Execute(
        current,
        EndTurnCommand.Create(
          CommandId.Create("turn-end"),
          current.MatchId,
          current.Revision,
          participantA,
          mainPhase)));

    current = Apply(
      history,
      MatchEngine.Execute(
        current,
        RevokeParticipantItemCommand.Create(
          CommandId.Create("revoke-a"),
          current.MatchId,
          current.Revision,
          participantA,
          ParticipantItemId.Create("item-a"))));

    current = Apply(
      history,
      MatchEngine.Execute(
        current,
        RelocateParticipantCommand.Create(
          CommandId.Create("relocate-b"),
          current.MatchId,
          current.Revision,
          participantB,
          PositionId.Create("position-b-relocated"))));

    var replayed = MatchEngine.Reduce(
      initial,
      history);
    var finalHash = MatchStateHasher.Compute(current);
    var replayHash = MatchStateHasher.Compute(replayed);

    Assert.Equal(finalHash, replayHash);
    Assert.True(
      MatchInvariantValidator.Validate(current).IsValid);
    Assert.True(
      MatchInvariantValidator.Validate(replayed).IsValid);

    var snapshot = string.Join(
      "\n",
      history.Select(Signature));

    Assert.Fail(
      $"GOLDEN_CAPTURE_V1\nHASH={finalHash.Value}\nREVISION={current.Revision.Value}\nEVENTS\n{snapshot}");
  }

  private static MatchState Apply(
    List<MatchEvent> history,
    MatchTransition transition)
  {
    history.AddRange(transition.Events);
    return transition.State;
  }

  private static string Signature(MatchEvent domainEvent)
  {
    return domainEvent switch
    {
      ParticipantJoinedEvent joined =>
        $"ParticipantJoined|{joined.CommandId.Value}|{joined.Revision.Value}|{joined.ParticipantId.Value}",
      ParticipantPlacedEvent placed =>
        $"ParticipantPlaced|{placed.CommandId.Value}|{placed.Revision.Value}|{placed.ParticipantId.Value}|{placed.PositionId.Value}",
      ParticipantMovedEvent moved =>
        $"ParticipantMoved|{moved.CommandId.Value}|{moved.Revision.Value}|{moved.ParticipantId.Value}|{moved.FromPositionId.Value}|{moved.ToPositionId.Value}|{moved.Distance}",
      ParticipantRelocatedEvent relocated =>
        $"ParticipantRelocated|{relocated.CommandId.Value}|{relocated.Revision.Value}|{relocated.ParticipantId.Value}|{relocated.FromPositionId.Value}|{relocated.ToPositionId.Value}",
      ParticipantItemGrantedEvent granted =>
        $"ParticipantItemGranted|{granted.CommandId.Value}|{granted.Revision.Value}|{granted.ParticipantId.Value}|{granted.ItemId.Value}",
      ParticipantItemRevokedEvent revoked =>
        $"ParticipantItemRevoked|{revoked.CommandId.Value}|{revoked.Revision.Value}|{revoked.ParticipantId.Value}|{revoked.ItemId.Value}",
      DeckCreatedEvent created =>
        $"DeckCreated|{created.CommandId.Value}|{created.Revision.Value}|{created.DeckId.Value}|{string.Join(',', created.Items.Select(item => item.Value))}",
      DeckItemDrawnEvent drawn =>
        $"DeckItemDrawn|{drawn.CommandId.Value}|{drawn.Revision.Value}|{drawn.DeckId.Value}|{drawn.ItemId.Value}|{drawn.RandomStateAfter.Value}",
      TurnStartedEvent started =>
        $"TurnStarted|{started.CommandId.Value}|{started.Revision.Value}|{started.ParticipantId.Value}|{started.PhaseId.Value}",
      TurnEndedEvent ended =>
        $"TurnEnded|{ended.CommandId.Value}|{ended.Revision.Value}|{ended.ParticipantId.Value}",
      TurnPhaseChangedEvent changed =>
        $"TurnPhaseChanged|{changed.CommandId.Value}|{changed.Revision.Value}|{changed.ParticipantId.Value}|{changed.PreviousPhaseId.Value}|{changed.PhaseId.Value}",
      RandomIntGeneratedEvent generated =>
        $"RandomIntGenerated|{generated.CommandId.Value}|{generated.Revision.Value}|{generated.MinInclusive}|{generated.MaxExclusive}|{generated.Value}|{generated.RandomStateAfter.Value}",
      _ => throw new InvalidOperationException(
        $"Unsupported golden event '{domainEvent.GetType().Name}'.")
    };
  }
}
