using Xunit;

namespace Mayordomo.Core.Tests;

public sealed class DeckHardeningTests
{
  [Fact]
  public void Same_seed_and_command_history_produce_same_draw_sequence()
  {
    var first = RunDrawSequence(seed: 17UL);
    var second = RunDrawSequence(seed: 17UL);

    Assert.Equal(first, second);
  }

  [Fact]
  public void Different_seeds_can_produce_different_draw_sequences()
  {
    var first = RunDrawSequence(seed: 1UL);
    var second = RunDrawSequence(seed: 2UL);

    Assert.False(first.SequenceEqual(second));
  }

  [Fact]
  public void Interleaved_draws_keep_each_decks_remaining_state_isolated_and_replay_exactly()
  {
    var initial = MatchState.Create(
      MatchId.Create("multi-deck-match"),
      randomSeed: 236UL);
    var deckA = DeckId.Create("deck-a");
    var deckB = DeckId.Create("deck-b");
    var history = new List<MatchEvent>();

    var createdA = MatchEngine.Execute(
      initial,
      CreateDeckCommand.Create(
        CommandId.Create("create-a"),
        initial.MatchId,
        initial.Revision,
        deckA,
        [
          DeckItemId.Create("a-1"),
          DeckItemId.Create("a-2"),
          DeckItemId.Create("a-3")
        ]));
    history.AddRange(createdA.Events);

    var createdB = MatchEngine.Execute(
      createdA.State,
      CreateDeckCommand.Create(
        CommandId.Create("create-b"),
        initial.MatchId,
        createdA.State.Revision,
        deckB,
        [
          DeckItemId.Create("b-1"),
          DeckItemId.Create("b-2"),
          DeckItemId.Create("b-3")
        ]));
    history.AddRange(createdB.Events);

    var deckBBeforeDrawA =
      createdB.State.Decks[deckB].RemainingItems.ToArray();

    var drawnA = MatchEngine.Execute(
      createdB.State,
      DrawDeckItemCommand.Create(
        CommandId.Create("draw-a"),
        initial.MatchId,
        createdB.State.Revision,
        deckA));
    history.AddRange(drawnA.Events);

    Assert.Equal(
      deckBBeforeDrawA,
      drawnA.State.Decks[deckB].RemainingItems);

    var deckAAfterDrawA =
      drawnA.State.Decks[deckA].RemainingItems.ToArray();

    var drawnB = MatchEngine.Execute(
      drawnA.State,
      DrawDeckItemCommand.Create(
        CommandId.Create("draw-b"),
        initial.MatchId,
        drawnA.State.Revision,
        deckB));
    history.AddRange(drawnB.Events);

    Assert.Equal(
      deckAAfterDrawA,
      drawnB.State.Decks[deckA].RemainingItems);

    var replayed = MatchEngine.Reduce(
      initial,
      history);

    Assert.Equal(
      MatchStateHasher.Compute(drawnB.State),
      MatchStateHasher.Compute(replayed));
    Assert.Equal(
      drawnB.State.Decks[deckA].RemainingItems,
      replayed.Decks[deckA].RemainingItems);
    Assert.Equal(
      drawnB.State.Decks[deckB].RemainingItems,
      replayed.Decks[deckB].RemainingItems);
  }

  [Fact]
  public void Retrying_the_same_draw_command_does_not_draw_twice_or_advance_random_state()
  {
    var initial = MatchState.Create(
      MatchId.Create("idempotent-draw-match"),
      randomSeed: 31UL);
    var deckId = DeckId.Create("deck-main");

    var created = MatchEngine.Execute(
      initial,
      CreateDeckCommand.Create(
        CommandId.Create("create-deck"),
        initial.MatchId,
        initial.Revision,
        deckId,
        [
          DeckItemId.Create("card-a"),
          DeckItemId.Create("card-b"),
          DeckItemId.Create("card-c")
        ]));

    var command = DrawDeckItemCommand.Create(
      CommandId.Create("draw-once"),
      initial.MatchId,
      created.State.Revision,
      deckId);

    var first = MatchEngine.Execute(
      created.State,
      command);
    var retry = MatchEngine.Execute(
      first.State,
      command);

    Assert.True(retry.IsDuplicate);
    Assert.Empty(retry.Events);
    Assert.Equal(2, retry.State.Decks[deckId].RemainingItems.Count);
    Assert.Equal(first.State.Revision, retry.State.Revision);
    Assert.Equal(first.State.RandomState, retry.State.RandomState);
    Assert.Equal(
      MatchStateHasher.Compute(first.State),
      MatchStateHasher.Compute(retry.State));
  }

  [Fact]
  public void Concurrent_draw_with_stale_revision_is_rejected()
  {
    var initial = MatchState.Create(
      MatchId.Create("concurrent-draw-match"),
      randomSeed: 47UL);
    var deckId = DeckId.Create("deck-main");

    var created = MatchEngine.Execute(
      initial,
      CreateDeckCommand.Create(
        CommandId.Create("create-deck"),
        initial.MatchId,
        initial.Revision,
        deckId,
        [
          DeckItemId.Create("card-a"),
          DeckItemId.Create("card-b"),
          DeckItemId.Create("card-c")
        ]));

    var firstCommand = DrawDeckItemCommand.Create(
      CommandId.Create("draw-first"),
      initial.MatchId,
      created.State.Revision,
      deckId);
    var competingCommand = DrawDeckItemCommand.Create(
      CommandId.Create("draw-competing"),
      initial.MatchId,
      created.State.Revision,
      deckId);

    var first = MatchEngine.Execute(
      created.State,
      firstCommand);

    Assert.Throws<RevisionConflictException>(
      () => MatchEngine.Execute(
        first.State,
        competingCommand));
    Assert.Equal(
      2,
      first.State.Decks[deckId].RemainingItems.Count);
  }

  private static DeckItemId[] RunDrawSequence(ulong seed)
  {
    var initial = MatchState.Create(
      MatchId.Create("deterministic-draw-match"),
      seed);
    var deckId = DeckId.Create("deck-main");
    var created = MatchEngine.Execute(
      initial,
      CreateDeckCommand.Create(
        CommandId.Create("create-deck"),
        initial.MatchId,
        initial.Revision,
        deckId,
        [
          DeckItemId.Create("card-0"),
          DeckItemId.Create("card-1"),
          DeckItemId.Create("card-2"),
          DeckItemId.Create("card-3"),
          DeckItemId.Create("card-4"),
          DeckItemId.Create("card-5")
        ]));

    var current = created.State;
    var sequence = new List<DeckItemId>();

    for (var index = 0; index < 6; index++)
    {
      var transition = MatchEngine.Execute(
        current,
        DrawDeckItemCommand.Create(
          CommandId.Create($"draw-{index:D2}"),
          initial.MatchId,
          current.Revision,
          deckId));
      var domainEvent = Assert.IsType<DeckItemDrawnEvent>(
        Assert.Single(transition.Events));

      sequence.Add(domainEvent.ItemId);
      current = transition.State;
    }

    return [.. sequence];
  }
}
