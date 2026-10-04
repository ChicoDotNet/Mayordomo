using System.Collections.ObjectModel;

namespace Mayordomo.Core;

public enum MatchInvariantViolationCode
{
  DuplicateParticipant,
  PositionReferencesUnknownParticipant,
  MissingParticipantInventory,
  InventoryReferencesUnknownParticipant,
  DuplicateParticipantItem,
  DeckKeyMismatch,
  DuplicateDeckItem,
  TurnReferencesUnknownParticipant
}

public sealed record MatchInvariantViolation(
  MatchInvariantViolationCode Code,
  string Subject,
  string Message);

public sealed class MatchInvariantValidationResult
{
  internal MatchInvariantValidationResult(
    IEnumerable<MatchInvariantViolation> violations)
  {
    Violations = new ReadOnlyCollection<MatchInvariantViolation>(
      [.. violations]);
  }

  public bool IsValid => Violations.Count == 0;

  public IReadOnlyList<MatchInvariantViolation> Violations { get; }
}

public static class MatchInvariantValidator
{
  public static MatchInvariantValidationResult Validate(
    MatchState state)
  {
    ArgumentNullException.ThrowIfNull(state);

    var violations = new List<MatchInvariantViolation>();
    var participants = new HashSet<ParticipantId>();

    ValidateParticipants(
      state,
      participants,
      violations);
    ValidatePositions(
      state,
      participants,
      violations);
    ValidateMissingInventories(
      state,
      violations);
    ValidateInventories(
      state,
      participants,
      violations);
    ValidateDecks(
      state,
      violations);
    ValidateTurn(
      state,
      participants,
      violations);

    return new MatchInvariantValidationResult(
      violations);
  }

  private static void ValidateParticipants(
    MatchState state,
    ISet<ParticipantId> participants,
    ICollection<MatchInvariantViolation> violations)
  {
    foreach (var participant in state.Participants)
    {
      if (participants.Add(participant))
      {
        continue;
      }

      violations.Add(
        new MatchInvariantViolation(
          MatchInvariantViolationCode.DuplicateParticipant,
          participant.Value,
          $"Participant '{participant}' occurs more than once."));
    }
  }

  private static void ValidatePositions(
    MatchState state,
    IReadOnlySet<ParticipantId> participants,
    ICollection<MatchInvariantViolation> violations)
  {
    foreach (var position in state.Positions
               .OrderBy(
                 entry => entry.Key.Value,
                 StringComparer.Ordinal))
    {
      if (participants.Contains(position.Key))
      {
        continue;
      }

      violations.Add(
        new MatchInvariantViolation(
          MatchInvariantViolationCode.PositionReferencesUnknownParticipant,
          position.Key.Value,
          $"Position state references unknown participant '{position.Key}'."));
    }
  }

  private static void ValidateMissingInventories(
    MatchState state,
    ICollection<MatchInvariantViolation> violations)
  {
    var inspected = new HashSet<ParticipantId>();

    foreach (var participant in state.Participants)
    {
      if (!inspected.Add(participant) ||
          state.HeldItems.ContainsKey(participant))
      {
        continue;
      }

      violations.Add(
        new MatchInvariantViolation(
          MatchInvariantViolationCode.MissingParticipantInventory,
          participant.Value,
          $"Participant '{participant}' has no held-item inventory."));
    }
  }

  private static void ValidateInventories(
    MatchState state,
    IReadOnlySet<ParticipantId> participants,
    ICollection<MatchInvariantViolation> violations)
  {
    foreach (var inventory in state.HeldItems
               .OrderBy(
                 entry => entry.Key.Value,
                 StringComparer.Ordinal))
    {
      if (!participants.Contains(inventory.Key))
      {
        violations.Add(
          new MatchInvariantViolation(
            MatchInvariantViolationCode.InventoryReferencesUnknownParticipant,
            inventory.Key.Value,
            $"Held-item inventory references unknown participant '{inventory.Key}'."));
      }

      var items = new HashSet<ParticipantItemId>();

      foreach (var item in inventory.Value)
      {
        if (items.Add(item))
        {
          continue;
        }

        violations.Add(
          new MatchInvariantViolation(
            MatchInvariantViolationCode.DuplicateParticipantItem,
            $"{inventory.Key.Value}/{item.Value}",
            $"Participant '{inventory.Key}' holds duplicate item '{item}'."));
      }
    }
  }

  private static void ValidateDecks(
    MatchState state,
    ICollection<MatchInvariantViolation> violations)
  {
    foreach (var deckEntry in state.Decks
               .OrderBy(
                 entry => entry.Key.Value,
                 StringComparer.Ordinal))
    {
      var deck = deckEntry.Value;

      if (deckEntry.Key != deck.DeckId)
      {
        violations.Add(
          new MatchInvariantViolation(
            MatchInvariantViolationCode.DeckKeyMismatch,
            deckEntry.Key.Value,
            $"Deck dictionary key '{deckEntry.Key}' does not match deck state id '{deck.DeckId}'."));
      }

      var items = new HashSet<DeckItemId>();

      foreach (var item in deck.RemainingItems)
      {
        if (items.Add(item))
        {
          continue;
        }

        violations.Add(
          new MatchInvariantViolation(
            MatchInvariantViolationCode.DuplicateDeckItem,
            $"{deckEntry.Key.Value}/{item.Value}",
            $"Deck '{deckEntry.Key}' contains duplicate remaining item '{item}'."));
      }
    }
  }

  private static void ValidateTurn(
    MatchState state,
    IReadOnlySet<ParticipantId> participants,
    ICollection<MatchInvariantViolation> violations)
  {
    if (state.Turn is null ||
        participants.Contains(state.Turn.ParticipantId))
    {
      return;
    }

    violations.Add(
      new MatchInvariantViolation(
        MatchInvariantViolationCode.TurnReferencesUnknownParticipant,
        state.Turn.ParticipantId.Value,
        $"Active turn references unknown participant '{state.Turn.ParticipantId}'."));
  }
}
