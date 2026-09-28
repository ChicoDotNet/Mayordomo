namespace Mayordomo.Core;

public sealed record ChangeTurnPhaseCommand
{
  private ChangeTurnPhaseCommand(
    CommandId commandId,
    MatchId matchId,
    Revision expectedRevision,
    ParticipantId participantId,
    PhaseId phaseId)
  {
    CommandId = commandId;
    MatchId = matchId;
    ExpectedRevision = expectedRevision;
    ParticipantId = participantId;
    PhaseId = phaseId;
  }

  public CommandId CommandId { get; }

  public MatchId MatchId { get; }

  public Revision ExpectedRevision { get; }

  public ParticipantId ParticipantId { get; }

  public PhaseId PhaseId { get; }

  public static ChangeTurnPhaseCommand Create(
    CommandId commandId,
    MatchId matchId,
    Revision expectedRevision,
    ParticipantId participantId,
    PhaseId phaseId)
  {
    return new ChangeTurnPhaseCommand(
      commandId,
      matchId,
      expectedRevision,
      participantId,
      phaseId);
  }
}

public sealed record TurnPhaseChangedEvent(
  CommandId CommandId,
  MatchId MatchId,
  Revision Revision,
  ParticipantId ParticipantId,
  PhaseId PreviousPhaseId,
  PhaseId PhaseId)
  : MatchEvent(CommandId, MatchId, Revision);
