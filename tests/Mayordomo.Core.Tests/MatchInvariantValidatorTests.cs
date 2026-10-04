using System.Reflection;
using Xunit;

namespace Mayordomo.Core.Tests;

public sealed class MatchInvariantValidatorTests
{
  [Fact]
  public void Valid_complex_state_has_no_invariant_violations()
  {
    var initial = MatchState.Create(
      MatchId.Create("match-001"),
      randomSeed: 91UL);
    var participant = ParticipantId.Create("participant-001");

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
        PositionId.Create("position-001")));

    var granted = MatchEngine.Execute(
      placed.State,
      GrantParticipantItemCommand.Create(
        CommandId.Create("grant-001"),
        initial.MatchId,
        placed.State.Revision,
        participant,
        ParticipantItemId.Create("item-001")));

    var deckCreated = MatchEngine.Execute(
      granted.State,
      CreateDeckCommand.Create(
        CommandId.Create("deck-001"),
        initial.MatchId,
        granted.State.Revision,
        DeckId.Create("deck-001"),
        [
          DeckItemId.Create("card-a"),
          DeckItemId.Create("card-b")
        ]));

    var turnStarted = MatchEngine.Execute(
      deckCreated.State,
      StartTurnCommand.Create(
        CommandId.Create("turn-001"),
        initial.MatchId,
        deckCreated.State.Revision,
        participant,
        PhaseId.Create("phase-001")));

    var result = MatchInvariantValidator.Validate(
      turnStarted.State);

    Assert.True(result.IsValid);
    Assert.Empty(result.Violations);
  }

  [Fact]
  public void Structural_corruption_is_reported_with_typed_deterministic_violations()
  {
    var participant =
      ParticipantId.Create("participant-a");
    var ghost =
      ParticipantId.Create("participant-ghost");
    var duplicateItem =
      ParticipantItemId.Create("held-item");
    var deckKey =
      DeckId.Create("deck-key");
    var deckState = CreateMalformedDeckState(
      DeckId.Create("deck-state"),
      [
        DeckItemId.Create("duplicate-card"),
        DeckItemId.Create("duplicate-card")
      ]);

    var state = CreateMalformedMatchState(
      participants:
      [
        participant,
        participant
      ],
      positions:
      [
        new KeyValuePair<ParticipantId, PositionId>(
          ghost,
          PositionId.Create("position-ghost"))
      ],
      heldItems:
      [
        new KeyValuePair<ParticipantId, IReadOnlyList<ParticipantItemId>>(
          ghost,
          [duplicateItem, duplicateItem])
      ],
      decks:
      [
        new KeyValuePair<DeckId, DeckState>(
          deckKey,
          deckState)
      ],
      turn: new TurnState(
        ghost,
        PhaseId.Create("phase-ghost")));

    var result = MatchInvariantValidator.Validate(state);

    Assert.False(result.IsValid);
    Assert.Equal(
      [
        MatchInvariantViolationCode.DuplicateParticipant,
        MatchInvariantViolationCode.PositionReferencesUnknownParticipant,
        MatchInvariantViolationCode.MissingParticipantInventory,
        MatchInvariantViolationCode.InventoryReferencesUnknownParticipant,
        MatchInvariantViolationCode.DuplicateParticipantItem,
        MatchInvariantViolationCode.DeckKeyMismatch,
        MatchInvariantViolationCode.DuplicateDeckItem,
        MatchInvariantViolationCode.TurnReferencesUnknownParticipant
      ],
      result.Violations
        .Select(violation => violation.Code)
        .ToArray());
  }

  private static MatchState CreateMalformedMatchState(
    IEnumerable<ParticipantId> participants,
    IEnumerable<KeyValuePair<ParticipantId, PositionId>> positions,
    IEnumerable<KeyValuePair<ParticipantId, IReadOnlyList<ParticipantItemId>>> heldItems,
    IEnumerable<KeyValuePair<DeckId, DeckState>> decks,
    TurnState? turn)
  {
    var constructor = typeof(MatchState)
      .GetConstructors(
        BindingFlags.Instance |
        BindingFlags.NonPublic)
      .Single();

    return (MatchState)constructor.Invoke(
      [
        MatchId.Create("malformed-match"),
        Revision.Create(9),
        participants,
        positions,
        heldItems,
        decks,
        turn,
        new RandomState(17UL)
      ]);
  }

  private static DeckState CreateMalformedDeckState(
    DeckId deckId,
    IEnumerable<DeckItemId> items)
  {
    var constructor = typeof(DeckState)
      .GetConstructors(
        BindingFlags.Instance |
        BindingFlags.NonPublic)
      .Single();

    return (DeckState)constructor.Invoke(
      [
        deckId,
        items
      ]);
  }
}
