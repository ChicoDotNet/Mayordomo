using Xunit;

namespace Mayordomo.Core.Tests;

public sealed class CanonicalStateHashTests
{
  [Fact]
  public void Initial_state_has_stable_v2_sha256_golden_hash()
  {
    var state = MatchState.Create(
      MatchId.Create("match-001"),
      randomSeed: 42UL);

    var hash = MatchStateHasher.Compute(state);

    Assert.Equal(
      "D48AF929A9C2667990C53569ED6D9FC9F9E2841047741AFB8008979ADB40A169",
      hash.Value);
    Assert.Equal("SHA-256", MatchStateHasher.Algorithm);
    Assert.Equal(2, MatchStateHasher.FormatVersion);
  }

  [Fact]
  public void Exact_replay_produces_the_same_canonical_hash()
  {
    var initial = MatchState.Create(
      MatchId.Create("match-001"),
      randomSeed: 12345UL);
    var participant = ParticipantId.Create("participant-001");
    var position = PositionId.Create("position-001");
    var item = ParticipantItemId.Create("item-001");
    var deck = DeckId.Create("deck-001");

    var joined = MatchEngine.Execute(
      initial,
      JoinParticipantCommand.Create(
        CommandId.Create("join-001"),
        initial.MatchId,
        initial.Revision,
        participant));

    var placed = MatchEngine.Execute(
      joined.State,
      PlaceParticipantCommand.Create(
        CommandId.Create("place-001"),
        initial.MatchId,
        joined.State.Revision,
        participant,
        position));

    var granted = MatchEngine.Execute(
      placed.State,
      GrantParticipantItemCommand.Create(
        CommandId.Create("grant-001"),
        initial.MatchId,
        placed.State.Revision,
        participant,
        item));

    var created = MatchEngine.Execute(
      granted.State,
      CreateDeckCommand.Create(
        CommandId.Create("deck-001"),
        initial.MatchId,
        granted.State.Revision,
        deck,
        [
          DeckItemId.Create("card-a"),
          DeckItemId.Create("card-b"),
          DeckItemId.Create("card-c")
        ]));

    var drawn = MatchEngine.Execute(
      created.State,
      DrawDeckItemCommand.Create(
        CommandId.Create("draw-001"),
        initial.MatchId,
        created.State.Revision,
        deck));

    var replayed = MatchEngine.Reduce(
      initial,
      joined.Events
        .Concat(placed.Events)
        .Concat(granted.Events)
        .Concat(created.Events)
        .Concat(drawn.Events));

    Assert.Equal(
      MatchStateHasher.Compute(drawn.State),
      MatchStateHasher.Compute(replayed));
  }

  [Fact]
  public void Dictionary_insertion_order_does_not_change_canonical_hash()
  {
    var first = CreateSameSemanticPositionState(
      reversePlacementOrder: false);
    var second = CreateSameSemanticPositionState(
      reversePlacementOrder: true);

    Assert.Equal(first.Revision, second.Revision);
    Assert.Equal(
      MatchStateHasher.Compute(first),
      MatchStateHasher.Compute(second));
  }

  [Fact]
  public void Semantic_state_changes_change_the_hash()
  {
    var first = MatchState.Create(
      MatchId.Create("match-001"),
      randomSeed: 1UL);
    var second = MatchState.Create(
      MatchId.Create("match-001"),
      randomSeed: 2UL);

    Assert.NotEqual(
      MatchStateHasher.Compute(first),
      MatchStateHasher.Compute(second));
  }

  private static MatchState CreateSameSemanticPositionState(
    bool reversePlacementOrder)
  {
    var initial = MatchState.Create(
      MatchId.Create("match-001"),
      randomSeed: 7UL);
    var firstParticipant =
      ParticipantId.Create("participant-a");
    var secondParticipant =
      ParticipantId.Create("participant-b");

    var firstJoin = MatchEngine.Execute(
      initial,
      JoinParticipantCommand.Create(
        CommandId.Create("join-a"),
        initial.MatchId,
        initial.Revision,
        firstParticipant));

    var secondJoin = MatchEngine.Execute(
      firstJoin.State,
      JoinParticipantCommand.Create(
        CommandId.Create("join-b"),
        initial.MatchId,
        firstJoin.State.Revision,
        secondParticipant));

    var placementOrder = reversePlacementOrder
      ? new[]
      {
        (secondParticipant, PositionId.Create("position-b")),
        (firstParticipant, PositionId.Create("position-a"))
      }
      : new[]
      {
        (firstParticipant, PositionId.Create("position-a")),
        (secondParticipant, PositionId.Create("position-b"))
      };

    var current = secondJoin.State;

    foreach (var placement in placementOrder)
    {
      current = MatchEngine.Execute(
        current,
        PlaceParticipantCommand.Create(
          CommandId.Create(
            $"place-{placement.Item1.Value}"),
          initial.MatchId,
          current.Revision,
          placement.Item1,
          placement.Item2)).State;

    }

    return current;
  }
}
