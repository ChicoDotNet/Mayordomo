using System.Collections.ObjectModel;

namespace Mayordomo.Core;

public readonly record struct DeckId
{
  private DeckId(string value)
  {
    Value = value;
  }

  public string Value { get; }

  public static DeckId Create(string value)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(value);
    return new DeckId(value);
  }

  public override string ToString() => Value;
}

public readonly record struct DeckItemId
{
  private DeckItemId(string value)
  {
    Value = value;
  }

  public string Value { get; }

  public static DeckItemId Create(string value)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(value);
    return new DeckItemId(value);
  }

  public override string ToString() => Value;
}

public sealed class DeckState
{
  private DeckState(
    DeckId deckId,
    IEnumerable<DeckItemId> remainingItems)
  {
    DeckId = deckId;
    RemainingItems = new ReadOnlyCollection<DeckItemId>(
      [.. remainingItems]);
  }

  public DeckId DeckId { get; }

  public IReadOnlyList<DeckItemId> RemainingItems { get; }

  internal static DeckState Create(
    DeckId deckId,
    IEnumerable<DeckItemId> items)
  {
    return new DeckState(deckId, items);
  }

  internal DeckState RemoveAt(int index)
  {
    var remaining = RemainingItems.ToList();
    remaining.RemoveAt(index);

    return new DeckState(DeckId, remaining);
  }
}

public sealed record CreateDeckCommand
{
  private CreateDeckCommand(
    CommandId commandId,
    MatchId matchId,
    Revision expectedRevision,
    DeckId deckId,
    IReadOnlyList<DeckItemId> items)
  {
    CommandId = commandId;
    MatchId = matchId;
    ExpectedRevision = expectedRevision;
    DeckId = deckId;
    Items = new ReadOnlyCollection<DeckItemId>([.. items]);
  }

  public CommandId CommandId { get; }

  public MatchId MatchId { get; }

  public Revision ExpectedRevision { get; }

  public DeckId DeckId { get; }

  public IReadOnlyList<DeckItemId> Items { get; }

  public static CreateDeckCommand Create(
    CommandId commandId,
    MatchId matchId,
    Revision expectedRevision,
    DeckId deckId,
    IReadOnlyList<DeckItemId> items)
  {
    ArgumentNullException.ThrowIfNull(items);

    if (items.Count == 0)
    {
      throw new ArgumentException(
        "A deck must contain at least one item.",
        nameof(items));
    }

    if (items.Distinct().Count() != items.Count)
    {
      throw new ArgumentException(
        "A deck cannot contain duplicate item identifiers.",
        nameof(items));
    }

    return new CreateDeckCommand(
      commandId,
      matchId,
      expectedRevision,
      deckId,
      items);
  }
}

public sealed record DrawDeckItemCommand
{
  private DrawDeckItemCommand(
    CommandId commandId,
    MatchId matchId,
    Revision expectedRevision,
    DeckId deckId)
  {
    CommandId = commandId;
    MatchId = matchId;
    ExpectedRevision = expectedRevision;
    DeckId = deckId;
  }

  public CommandId CommandId { get; }

  public MatchId MatchId { get; }

  public Revision ExpectedRevision { get; }

  public DeckId DeckId { get; }

  public static DrawDeckItemCommand Create(
    CommandId commandId,
    MatchId matchId,
    Revision expectedRevision,
    DeckId deckId)
  {
    return new DrawDeckItemCommand(
      commandId,
      matchId,
      expectedRevision,
      deckId);
  }
}

public sealed record DeckCreatedEvent(
  CommandId CommandId,
  MatchId MatchId,
  Revision Revision,
  DeckId DeckId,
  IReadOnlyList<DeckItemId> Items)
  : MatchEvent(CommandId, MatchId, Revision);

public sealed record DeckItemDrawnEvent(
  CommandId CommandId,
  MatchId MatchId,
  Revision Revision,
  DeckId DeckId,
  DeckItemId ItemId,
  RandomState RandomStateAfter)
  : MatchEvent(CommandId, MatchId, Revision);
