namespace Mayordomo.Core;

public readonly record struct ParticipantItemId
{
  private ParticipantItemId(string value)
  {
    Value = value;
  }

  public string Value { get; }

  public static ParticipantItemId Create(string value)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(value);
    return new ParticipantItemId(value);
  }

  public override string ToString() => Value;
}

public sealed record GrantParticipantItemCommand
{
  private GrantParticipantItemCommand(
    CommandId commandId,
    MatchId matchId,
    Revision expectedRevision,
    ParticipantId participantId,
    ParticipantItemId itemId)
  {
    CommandId = commandId;
    MatchId = matchId;
    ExpectedRevision = expectedRevision;
    ParticipantId = participantId;
    ItemId = itemId;
  }

  public CommandId CommandId { get; }

  public MatchId MatchId { get; }

  public Revision ExpectedRevision { get; }

  public ParticipantId ParticipantId { get; }

  public ParticipantItemId ItemId { get; }

  public static GrantParticipantItemCommand Create(
    CommandId commandId,
    MatchId matchId,
    Revision expectedRevision,
    ParticipantId participantId,
    ParticipantItemId itemId)
  {
    return new GrantParticipantItemCommand(
      commandId,
      matchId,
      expectedRevision,
      participantId,
      itemId);
  }
}

public sealed record RevokeParticipantItemCommand
{
  private RevokeParticipantItemCommand(
    CommandId commandId,
    MatchId matchId,
    Revision expectedRevision,
    ParticipantId participantId,
    ParticipantItemId itemId)
  {
    CommandId = commandId;
    MatchId = matchId;
    ExpectedRevision = expectedRevision;
    ParticipantId = participantId;
    ItemId = itemId;
  }

  public CommandId CommandId { get; }

  public MatchId MatchId { get; }

  public Revision ExpectedRevision { get; }

  public ParticipantId ParticipantId { get; }

  public ParticipantItemId ItemId { get; }

  public static RevokeParticipantItemCommand Create(
    CommandId commandId,
    MatchId matchId,
    Revision expectedRevision,
    ParticipantId participantId,
    ParticipantItemId itemId)
  {
    return new RevokeParticipantItemCommand(
      commandId,
      matchId,
      expectedRevision,
      participantId,
      itemId);
  }
}

public sealed record ParticipantItemGrantedEvent(
  CommandId CommandId,
  MatchId MatchId,
  Revision Revision,
  ParticipantId ParticipantId,
  ParticipantItemId ItemId)
  : MatchEvent(CommandId, MatchId, Revision);

public sealed record ParticipantItemRevokedEvent(
  CommandId CommandId,
  MatchId MatchId,
  Revision Revision,
  ParticipantId ParticipantId,
  ParticipantItemId ItemId)
  : MatchEvent(CommandId, MatchId, Revision);
