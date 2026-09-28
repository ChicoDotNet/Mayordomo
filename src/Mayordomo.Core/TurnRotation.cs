namespace Mayordomo.Core;

public sealed record EndTurnCommand
{
  private EndTurnCommand(
    CommandId commandId,
    MatchId matchId,
    Revision expectedRevision,
    ParticipantId participantId,
    PhaseId nextPhaseId)
  {
    CommandId = commandId;
    MatchId = matchId;
    ExpectedRevision = expectedRevision;
    ParticipantId = participantId;
    NextPhaseId = nextPhaseId;
  }

  public CommandId CommandId { get; }

  public MatchId MatchId { get; }

  public Revision ExpectedRevision { get; }

  public ParticipantId ParticipantId { get; }

  public PhaseId NextPhaseId { get; }

  public static EndTurnCommand Create(
    CommandId commandId,
    MatchId matchId,
    Revision expectedRevision,
    ParticipantId participantId,
    PhaseId nextPhaseId)
  {
    return new EndTurnCommand(
      commandId,
      matchId,
      expectedRevision,
      participantId,
      nextPhaseId);
  }
}

public sealed record TurnEndedEvent(
  CommandId CommandId,
  MatchId MatchId,
  Revision Revision,
  ParticipantId ParticipantId)
  : MatchEvent(CommandId, MatchId, Revision);
