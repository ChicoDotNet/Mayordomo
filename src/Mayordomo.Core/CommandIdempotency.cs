using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Mayordomo.Core;

public sealed class CommandIdConflictException : InvalidOperationException
{
  public CommandIdConflictException(CommandId commandId)
    : base($"Command id '{commandId}' was already used for a different command intent.")
  {
    CommandId = commandId;
  }

  public CommandId CommandId { get; }
}

internal static class CommandFingerprint
{
  public static string Create(JoinParticipantCommand command) =>
    Hash("join-participant", command.MatchId.Value, command.ParticipantId.Value);

  public static string Create(PlaceParticipantCommand command) =>
    Hash("place-participant", command.MatchId.Value, command.ParticipantId.Value, command.PositionId.Value);

  public static string Create(MoveParticipantCommand command) =>
    Hash(
      "move-participant",
      command.MatchId.Value,
      command.ParticipantId.Value,
      command.ToPositionId.Value,
      command.Distance.ToString(CultureInfo.InvariantCulture));

  public static string Create(RelocateParticipantCommand command) =>
    Hash("relocate-participant", command.MatchId.Value, command.ParticipantId.Value, command.ToPositionId.Value);

  public static string Create(GrantParticipantItemCommand command) =>
    Hash("grant-participant-item", command.MatchId.Value, command.ParticipantId.Value, command.ItemId.Value);

  public static string Create(RevokeParticipantItemCommand command) =>
    Hash("revoke-participant-item", command.MatchId.Value, command.ParticipantId.Value, command.ItemId.Value);

  public static string Create(CreateDeckCommand command)
  {
    var values = new List<string>
    {
      command.MatchId.Value,
      command.DeckId.Value
    };
    values.AddRange(command.Items.Select(item => item.Value));
    return Hash("create-deck", [.. values]);
  }

  public static string Create(DrawDeckItemCommand command) =>
    Hash("draw-deck-item", command.MatchId.Value, command.DeckId.Value);

  public static string Create(StartTurnCommand command) =>
    Hash("start-turn", command.MatchId.Value, command.ParticipantId.Value, command.PhaseId.Value);

  public static string Create(GenerateRandomIntCommand command) =>
    Hash(
      "generate-random-int",
      command.MatchId.Value,
      command.MinInclusive.ToString(CultureInfo.InvariantCulture),
      command.MaxExclusive.ToString(CultureInfo.InvariantCulture));

  public static string Create(EndTurnCommand command) =>
    Hash("end-turn", command.MatchId.Value, command.ParticipantId.Value, command.NextPhaseId.Value);

  public static string Create(ChangeTurnPhaseCommand command) =>
    Hash("change-turn-phase", command.MatchId.Value, command.ParticipantId.Value, command.PhaseId.Value);

  public static string Create(IReadOnlyList<MatchEvent> events)
  {
    if (events.Count == 0)
    {
      throw new ArgumentException("A command event group cannot be empty.", nameof(events));
    }

    var commandId = events[0].CommandId;
    var matchId = events[0].MatchId;

    if (events.Any(candidate =>
          candidate.CommandId != commandId ||
          candidate.MatchId != matchId))
    {
      throw new InvalidOperationException(
        "A command event group must share one command id and match id.");
    }

    if (events.Count == 2 &&
        events[0] is TurnEndedEvent ended &&
        events[1] is TurnStartedEvent started)
    {
      return Hash(
        "end-turn",
        matchId.Value,
        ended.ParticipantId.Value,
        started.PhaseId.Value);
    }

    if (events.Count != 1)
    {
      throw new InvalidOperationException(
        $"Unsupported command event group containing {events.Count} events.");
    }

    return events[0] switch
    {
      ParticipantJoinedEvent joined =>
        Hash("join-participant", matchId.Value, joined.ParticipantId.Value),
      ParticipantPlacedEvent placed =>
        Hash("place-participant", matchId.Value, placed.ParticipantId.Value, placed.PositionId.Value),
      ParticipantMovedEvent moved =>
        Hash(
          "move-participant",
          matchId.Value,
          moved.ParticipantId.Value,
          moved.ToPositionId.Value,
          moved.Distance.ToString(CultureInfo.InvariantCulture)),
      ParticipantRelocatedEvent relocated =>
        Hash("relocate-participant", matchId.Value, relocated.ParticipantId.Value, relocated.ToPositionId.Value),
      ParticipantItemGrantedEvent granted =>
        Hash("grant-participant-item", matchId.Value, granted.ParticipantId.Value, granted.ItemId.Value),
      ParticipantItemRevokedEvent revoked =>
        Hash("revoke-participant-item", matchId.Value, revoked.ParticipantId.Value, revoked.ItemId.Value),
      DeckCreatedEvent created =>
        HashDeckCreated(matchId, created),
      DeckItemDrawnEvent drawn =>
        Hash("draw-deck-item", matchId.Value, drawn.DeckId.Value),
      TurnStartedEvent turnStarted =>
        Hash("start-turn", matchId.Value, turnStarted.ParticipantId.Value, turnStarted.PhaseId.Value),
      TurnPhaseChangedEvent changed =>
        Hash("change-turn-phase", matchId.Value, changed.ParticipantId.Value, changed.PhaseId.Value),
      RandomIntGeneratedEvent generated =>
        Hash(
          "generate-random-int",
          matchId.Value,
          generated.MinInclusive.ToString(CultureInfo.InvariantCulture),
          generated.MaxExclusive.ToString(CultureInfo.InvariantCulture)),
      _ => throw new InvalidOperationException(
        $"Unsupported command event type '{events[0].GetType().Name}'.")
    };
  }

  private static string HashDeckCreated(
    MatchId matchId,
    DeckCreatedEvent created)
  {
    var values = new List<string>
    {
      matchId.Value,
      created.DeckId.Value
    };
    values.AddRange(created.Items.Select(item => item.Value));
    return Hash("create-deck", [.. values]);
  }

  private static string Hash(
    string kind,
    params string[] values)
  {
    var canonical = new StringBuilder();
    Append(canonical, kind);

    foreach (var value in values)
    {
      Append(canonical, value);
    }

    return Convert.ToHexString(
      SHA256.HashData(
        Encoding.UTF8.GetBytes(canonical.ToString())));
  }

  private static void Append(
    StringBuilder builder,
    string value)
  {
    builder
      .Append(value.Length.ToString(CultureInfo.InvariantCulture))
      .Append(':')
      .Append(value)
      .Append(';');
  }
}
