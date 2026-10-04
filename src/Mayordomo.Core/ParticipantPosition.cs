namespace Mayordomo.Core;

public readonly record struct PositionId
{
  private PositionId(string value)
  {
    Value = value;
  }

  public string Value { get; }

  public static PositionId Create(string value)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(value);
    return new PositionId(value);
  }

  public override string ToString() => Value;
}

public sealed record PlaceParticipantCommand
{
  private PlaceParticipantCommand(
    CommandId commandId,
    MatchId matchId,
    Revision expectedRevision,
    ParticipantId participantId,
    PositionId positionId)
  {
    CommandId = commandId;
    MatchId = matchId;
    ExpectedRevision = expectedRevision;
    ParticipantId = participantId;
    PositionId = positionId;
  }

  public CommandId CommandId { get; }

  public MatchId MatchId { get; }

  public Revision ExpectedRevision { get; }

  public ParticipantId ParticipantId { get; }

  public PositionId PositionId { get; }

  public static PlaceParticipantCommand Create(
    CommandId commandId,
    MatchId matchId,
    Revision expectedRevision,
    ParticipantId participantId,
    PositionId positionId)
  {
    return new PlaceParticipantCommand(
      commandId,
      matchId,
      expectedRevision,
      participantId,
      positionId);
  }
}

public sealed record MoveParticipantCommand
{
  private MoveParticipantCommand(
    CommandId commandId,
    MatchId matchId,
    Revision expectedRevision,
    ParticipantId participantId,
    PositionId toPositionId,
    int distance)
  {
    CommandId = commandId;
    MatchId = matchId;
    ExpectedRevision = expectedRevision;
    ParticipantId = participantId;
    ToPositionId = toPositionId;
    Distance = distance;
  }

  public CommandId CommandId { get; }

  public MatchId MatchId { get; }

  public Revision ExpectedRevision { get; }

  public ParticipantId ParticipantId { get; }

  public PositionId ToPositionId { get; }

  public int Distance { get; }

  public static MoveParticipantCommand Create(
    CommandId commandId,
    MatchId matchId,
    Revision expectedRevision,
    ParticipantId participantId,
    PositionId toPositionId,
    int distance)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(distance);

    return new MoveParticipantCommand(
      commandId,
      matchId,
      expectedRevision,
      participantId,
      toPositionId,
      distance);
  }
}

public sealed record ParticipantPlacedEvent(
  CommandId CommandId,
  MatchId MatchId,
  Revision Revision,
  ParticipantId ParticipantId,
  PositionId PositionId)
  : MatchEvent(CommandId, MatchId, Revision);

public sealed record ParticipantMovedEvent(
  CommandId CommandId,
  MatchId MatchId,
  Revision Revision,
  ParticipantId ParticipantId,
  PositionId FromPositionId,
  PositionId ToPositionId,
  int Distance)
  : MatchEvent(CommandId, MatchId, Revision);
