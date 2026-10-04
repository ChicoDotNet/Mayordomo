using Xunit;

namespace Mayordomo.Core.Tests;

public sealed class GoldenReplayTests
{
  private const string ExpectedHash =
    "C42C907ADF327C4B48616106B3220B179C5182F10E4243D8CCB4C6361378522F";

  [Fact]
  public void V1_persisted_history_replays_to_the_frozen_canonical_state()
  {
    var matchId = MatchId.Create("golden-v1");
    var participantA = ParticipantId.Create("participant-a");
    var participantB = ParticipantId.Create("participant-b");
    var mainPhase = PhaseId.Create("phase-main");
    var initial = MatchState.Create(
      matchId,
      randomSeed: 424242UL);

    MatchEvent[] history =
    [
      new ParticipantJoinedEvent(
        CommandId.Create("join-a"),
        matchId,
        Revision.Create(1),
        participantA),
      new ParticipantJoinedEvent(
        CommandId.Create("join-b"),
        matchId,
        Revision.Create(2),
        participantB),
      new ParticipantPlacedEvent(
        CommandId.Create("place-a"),
        matchId,
        Revision.Create(3),
        participantA,
        PositionId.Create("position-a")),
      new ParticipantPlacedEvent(
        CommandId.Create("place-b"),
        matchId,
        Revision.Create(4),
        participantB,
        PositionId.Create("position-b")),
      new ParticipantItemGrantedEvent(
        CommandId.Create("grant-a"),
        matchId,
        Revision.Create(5),
        participantA,
        ParticipantItemId.Create("item-a")),
      new DeckCreatedEvent(
        CommandId.Create("deck-create"),
        matchId,
        Revision.Create(6),
        DeckId.Create("deck-main"),
        [
          DeckItemId.Create("card-01"),
          DeckItemId.Create("card-02"),
          DeckItemId.Create("card-03"),
          DeckItemId.Create("card-04"),
          DeckItemId.Create("card-05")
        ]),
      new TurnStartedEvent(
        CommandId.Create("turn-start"),
        matchId,
        Revision.Create(7),
        participantA,
        mainPhase),
      new RandomIntGeneratedEvent(
        CommandId.Create("random-1"),
        matchId,
        Revision.Create(8),
        MinInclusive: 1,
        MaxExclusive: 7,
        Value: 2,
        RandomStateAfter: new RandomState(11400714819323622727UL)),
      new ParticipantMovedEvent(
        CommandId.Create("move-a"),
        matchId,
        Revision.Create(9),
        participantA,
        PositionId.Create("position-a"),
        PositionId.Create("position-a-moved"),
        Distance: 2),
      new DeckItemDrawnEvent(
        CommandId.Create("draw-1"),
        matchId,
        Revision.Create(10),
        DeckId.Create("deck-main"),
        DeckItemId.Create("card-01"),
        new RandomState(4354685564937269596UL)),
      new TurnPhaseChangedEvent(
        CommandId.Create("phase-change"),
        matchId,
        Revision.Create(11),
        participantA,
        mainPhase,
        PhaseId.Create("phase-resolve")),
      new TurnEndedEvent(
        CommandId.Create("turn-end"),
        matchId,
        Revision.Create(12),
        participantA),
      new TurnStartedEvent(
        CommandId.Create("turn-end"),
        matchId,
        Revision.Create(13),
        participantB,
        mainPhase),
      new ParticipantItemRevokedEvent(
        CommandId.Create("revoke-a"),
        matchId,
        Revision.Create(14),
        participantA,
        ParticipantItemId.Create("item-a")),
      new ParticipantRelocatedEvent(
        CommandId.Create("relocate-b"),
        matchId,
        Revision.Create(15),
        participantB,
        PositionId.Create("position-b"),
        PositionId.Create("position-b-relocated"))
    ];

    var replayed = MatchEngine.Reduce(
      initial,
      history);

    Assert.Equal(
      Revision.Create(15),
      replayed.Revision);
    Assert.Equal(
      ExpectedHash,
      MatchStateHasher.Compute(replayed).Value);

    var validation =
      MatchInvariantValidator.Validate(replayed);

    Assert.True(
      validation.IsValid,
      string.Join(
        " | ",
        validation.Violations.Select(
          violation =>
            $"{violation.Code}:{violation.Subject}")));
  }

  [Fact]
  public void V1_golden_history_rejects_deterministic_random_tampering()
  {
    var matchId = MatchId.Create("golden-v1");
    var initial = MatchState.Create(
      matchId,
      randomSeed: 424242UL);

    MatchEvent[] prefix =
    [
      new ParticipantJoinedEvent(
        CommandId.Create("join-a"),
        matchId,
        Revision.Create(1),
        ParticipantId.Create("participant-a")),
      new ParticipantJoinedEvent(
        CommandId.Create("join-b"),
        matchId,
        Revision.Create(2),
        ParticipantId.Create("participant-b")),
      new ParticipantPlacedEvent(
        CommandId.Create("place-a"),
        matchId,
        Revision.Create(3),
        ParticipantId.Create("participant-a"),
        PositionId.Create("position-a")),
      new ParticipantPlacedEvent(
        CommandId.Create("place-b"),
        matchId,
        Revision.Create(4),
        ParticipantId.Create("participant-b"),
        PositionId.Create("position-b")),
      new ParticipantItemGrantedEvent(
        CommandId.Create("grant-a"),
        matchId,
        Revision.Create(5),
        ParticipantId.Create("participant-a"),
        ParticipantItemId.Create("item-a")),
      new DeckCreatedEvent(
        CommandId.Create("deck-create"),
        matchId,
        Revision.Create(6),
        DeckId.Create("deck-main"),
        [
          DeckItemId.Create("card-01"),
          DeckItemId.Create("card-02"),
          DeckItemId.Create("card-03"),
          DeckItemId.Create("card-04"),
          DeckItemId.Create("card-05")
        ]),
      new TurnStartedEvent(
        CommandId.Create("turn-start"),
        matchId,
        Revision.Create(7),
        ParticipantId.Create("participant-a"),
        PhaseId.Create("phase-main")),
      new RandomIntGeneratedEvent(
        CommandId.Create("random-1"),
        matchId,
        Revision.Create(8),
        MinInclusive: 1,
        MaxExclusive: 7,
        Value: 3,
        RandomStateAfter: new RandomState(11400714819323622727UL))
    ];

    Assert.Throws<InvalidOperationException>(
      () => MatchEngine.Reduce(
        initial,
        prefix));
  }
}
