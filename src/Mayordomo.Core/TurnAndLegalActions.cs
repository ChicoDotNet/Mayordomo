using System.Collections.ObjectModel;

namespace Mayordomo.Core;

public readonly record struct PhaseId
{
  private PhaseId(string value)
  {
    Value = value;
  }

  public string Value { get; }

  public static PhaseId Create(string value)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(value);
    return new PhaseId(value);
  }

  public override string ToString() => Value;
}

public readonly record struct ActionId
{
  private ActionId(string value)
  {
    Value = value;
  }

  public string Value { get; }

  public static ActionId Create(string value)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(value);
    return new ActionId(value);
  }

  public override string ToString() => Value;
}

public sealed record TurnState(
  ParticipantId ParticipantId,
  PhaseId PhaseId);

public sealed record StartTurnCommand
{
  private StartTurnCommand(
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

  public static StartTurnCommand Create(
    CommandId commandId,
    MatchId matchId,
    Revision expectedRevision,
    ParticipantId participantId,
    PhaseId phaseId)
  {
    return new StartTurnCommand(
      commandId,
      matchId,
      expectedRevision,
      participantId,
      phaseId);
  }
}

public sealed record TurnStartedEvent(
  CommandId CommandId,
  MatchId MatchId,
  Revision Revision,
  ParticipantId ParticipantId,
  PhaseId PhaseId)
  : MatchEvent(CommandId, MatchId, Revision);

public interface ILegalActionRules
{
  IReadOnlyList<ActionId> Query(
    MatchState state,
    ParticipantId actor);
}

public sealed class LegalActionSet
{
  internal LegalActionSet(
    ParticipantId actor,
    Revision revision,
    IEnumerable<ActionId> actions)
  {
    Actor = actor;
    Revision = revision;
    Actions = new ReadOnlyCollection<ActionId>(
      [.. actions.Distinct()]);
  }

  public ParticipantId Actor { get; }

  public Revision Revision { get; }

  public IReadOnlyList<ActionId> Actions { get; }

  public void EnsureAllowed(ActionId action)
  {
    if (!Actions.Contains(action))
    {
      throw new IllegalActionException(
        Actor,
        action,
        Revision);
    }
  }
}

public sealed class IllegalActionException : InvalidOperationException
{
  public IllegalActionException(
    ParticipantId actor,
    ActionId action,
    Revision revision)
    : base(
      $"Action '{action}' is not legal for participant '{actor}' at revision {revision}.")
  {
    Actor = actor;
    Action = action;
    Revision = revision;
  }

  public ParticipantId Actor { get; }

  public ActionId Action { get; }

  public Revision Revision { get; }
}
