using Xunit;

namespace Mayordomo.Core.Tests;

public sealed class DeckDrawTests
{
  [Fact]
  public void Deck_can_be_created_drawn_without_replacement_and_replayed()
  {
    var initial = MatchState.Create(
      MatchId.Create("match-001"),
      randomSeed: 236UL);
    var deckId = DeckId.Create("deck-001");
    DeckItemId[] items =
    [
      DeckItemId.Create("item-a"),
      DeckItemId.Create("item-b"),
      DeckItemId.Create("item-c")
    ];

    var created = MatchEngine.Execute(
      initial,
      CreateDeckCommand.Create(
        CommandId.Create("deck-create"),
        initial.MatchId,
        initial.Revision,
        deckId,
        items));

    Assert.Equal(3, created.State.Decks[deckId].RemainingItems.Count);

    var first = MatchEngine.Execute(
      created.State,
      DrawDeckItemCommand.Create(
        CommandId.Create("draw-001"),
        initial.MatchId,
        created.State.Revision,
        deckId));

    var firstEvent = Assert.IsType<DeckItemDrawnEvent>(
      Assert.Single(first.Events));

    Assert.Equal(deckId, firstEvent.DeckId);
    Assert.Contains(firstEvent.ItemId, items);
    Assert.Equal(2, first.State.Decks[deckId].RemainingItems.Count);
    Assert.DoesNotContain(
      firstEvent.ItemId,
      first.State.Decks[deckId].RemainingItems);

    var second = MatchEngine.Execute(
      first.State,
      DrawDeckItemCommand.Create(
        CommandId.Create("draw-002"),
        initial.MatchId,
        first.State.Revision,
        deckId));

    var secondEvent = Assert.IsType<DeckItemDrawnEvent>(
      Assert.Single(second.Events));

    Assert.NotEqual(firstEvent.ItemId, secondEvent.ItemId);
    Assert.Single(second.State.Decks[deckId].RemainingItems);

    var replayed = MatchEngine.Reduce(
      initial,
      created.Events
        .Concat(first.Events)
        .Concat(second.Events));

    Assert.Equal(second.State.Revision, replayed.Revision);
    Assert.Equal(second.State.RandomState, replayed.RandomState);
    Assert.Equal(
      second.State.Decks[deckId].RemainingItems,
      replayed.Decks[deckId].RemainingItems);
  }

  [Fact]
  public void Deck_creation_rejects_empty_or_duplicate_items()
  {
    var matchId = MatchId.Create("match-001");
    var deckId = DeckId.Create("deck-001");

    Assert.Throws<ArgumentException>(
      () => CreateDeckCommand.Create(
        CommandId.Create("deck-create"),
        matchId,
        Revision.Zero,
        deckId,
        []));

    Assert.Throws<ArgumentException>(
      () => CreateDeckCommand.Create(
        CommandId.Create("deck-create"),
        matchId,
        Revision.Zero,
        deckId,
        [
          DeckItemId.Create("duplicate"),
          DeckItemId.Create("duplicate")
        ]));
  }

  [Fact]
  public void Empty_deck_cannot_be_drawn()
  {
    var initial = MatchState.Create(
      MatchId.Create("match-001"),
      randomSeed: 236UL);
    var deckId = DeckId.Create("deck-001");

    var created = MatchEngine.Execute(
      initial,
      CreateDeckCommand.Create(
        CommandId.Create("deck-create"),
        initial.MatchId,
        initial.Revision,
        deckId,
        [DeckItemId.Create("only-item")]));

    var drawn = MatchEngine.Execute(
      created.State,
      DrawDeckItemCommand.Create(
        CommandId.Create("draw-001"),
        initial.MatchId,
        created.State.Revision,
        deckId));

    Assert.Throws<InvalidOperationException>(
      () => MatchEngine.Execute(
        drawn.State,
        DrawDeckItemCommand.Create(
          CommandId.Create("draw-002"),
          initial.MatchId,
          drawn.State.Revision,
          deckId)));
  }

  [Fact]
  public void Replay_rejects_tampered_draw_item()
  {
    var initial = MatchState.Create(
      MatchId.Create("match-001"),
      randomSeed: 236UL);
    var deckId = DeckId.Create("deck-001");

    var created = MatchEngine.Execute(
      initial,
      CreateDeckCommand.Create(
        CommandId.Create("deck-create"),
        initial.MatchId,
        initial.Revision,
        deckId,
        [
          DeckItemId.Create("item-a"),
          DeckItemId.Create("item-b")
        ]));

    var random = DeterministicRandom.Restore(
      created.State.RandomState);
    _ = random.NextInt32(0, 2);

    MatchEvent[] tampered =
    [
      new DeckItemDrawnEvent(
        CommandId.Create("draw-001"),
        initial.MatchId,
        created.State.Revision.NextForTest(),
        deckId,
        DeckItemId.Create("not-in-deck"),
        random.State)
    ];

    Assert.Throws<InvalidOperationException>(
      () => MatchEngine.Reduce(
        created.State,
        tampered));
  }
}

internal static class RevisionTestExtensions
{
  public static Revision NextForTest(this Revision revision)
  {
    return Revision.Create(revision.Value + 1);
  }
}
