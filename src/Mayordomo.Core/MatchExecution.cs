using System.Collections.ObjectModel;

namespace Mayordomo.Core;

public readonly record struct MatchId
{
  private MatchId(string value)
  {
    Value = value;
  }

  public string Value { get; }

  public static MatchId Create(string value)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(value);
    return new MatchId(value);
  }

  public override string ToString() => Value;
}

public readonly record struct CommandId
{
  private CommandId(string value)
  {
    Value = value;
  }

  public string Value { get; }

  public static CommandId Create(string value)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(value);
    return new CommandId(value);
  }

  public override string ToString() => Value;
}

public readonly record struct ParticipantId
{
  private ParticipantId(string value)
  {
    Value = value;
  }

  public string Value { get; }

  public static ParticipantId Create(string value)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(value);
    return new ParticipantId(value);
  }

  public override string ToString() => Value;
}

public readonly record struct Revision
{
  private Revision(long value)
  {
    Value = value;
  }

  public long Value { get; }

  public static Revision Zero { get; } = new(0);

  public static Revision Create(long value)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(value);
    return new Revision(value);
  }

  internal Revision Next()
  {
    return new Revision(checked(Value + 1));
  }

  public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public sealed class MatchState
{
  private MatchState(
    MatchId matchId,
    Revision revision,
    IEnumerable<ParticipantId> participants)
  {
    MatchId = matchId;
    Revision = revision;
    Participants = new ReadOnlyCollection<ParticipantId>([.. participants]);
  }

  public MatchId MatchId { get; }

  public Revision Revision { get; }

  public IReadOnlyList<ParticipantId> Participants { get; }

  public static MatchState Create(MatchId matchId)
  {
    return new MatchState(matchId, Revision.Zero, []);
  }

  internal MatchState AddParticipant(
    ParticipantId participantId,
    Revision revision)
  {
    if (Participants.Contains(participantId))
    {
      throw new InvalidOperationException(
        $"Participant '{participantId}' is already part of match '{MatchId}'.");
    }

    return new MatchState(
      MatchId,
      revision,
      Participants.Append(participantId));
  }
}

public sealed record JoinParticipantCommand
{
  private JoinParticipantCommand(
    CommandId commandId,
    MatchId matchId,
    Revision expectedRevision,
    ParticipantId participantId)
  {
    CommandId = commandId;
    MatchId = matchId;
    ExpectedRevision = expectedRevision;
    ParticipantId = participantId;
  }

  public CommandId CommandId { get; }

  public MatchId MatchId { get; }

  public Revision ExpectedRevision { get; }

  public ParticipantId ParticipantId { get; }

  public static JoinParticipantCommand Create(
    CommandId commandId,
    MatchId matchId,
    Revision expectedRevision,
    ParticipantId participantId)
  {
    return new JoinParticipantCommand(
      commandId,
      matchId,
      expectedRevision,
      participantId);
  }
}

public abstract record MatchEvent(
  CommandId CommandId,
  MatchId MatchId,
  Revision Revision);

public sealed record ParticipantJoinedEvent(
  CommandId CommandId,
  MatchId MatchId,
  Revision Revision,
  ParticipantId ParticipantId)
  : MatchEvent(CommandId, MatchId, Revision);

public sealed record MatchTransition(
  MatchState State,
  IReadOnlyList<MatchEvent> Events);

public sealed class RevisionConflictException : InvalidOperationException
{
  public RevisionConflictException(
    Revision expected,
    Revision actual)
    : base($"Expected revision {expected}, but current revision is {actual}.")
  {
    Expected = expected;
    Actual = actual;
  }

  public Revision Expected { get; }

  public Revision Actual { get; }
}

public static class MatchEngine
{
  public static MatchTransition Execute(
    MatchState state,
    JoinParticipantCommand command)
  {
    ArgumentNullException.ThrowIfNull(state);
    ArgumentNullException.ThrowIfNull(command);

    if (state.MatchId != command.MatchId)
    {
      throw new InvalidOperationException(
        $"Command targets match '{command.MatchId}', but state belongs to '{state.MatchId}'.");
    }

    if (state.Revision != command.ExpectedRevision)
    {
      throw new RevisionConflictException(
        command.ExpectedRevision,
        state.Revision);
    }

    var nextRevision = state.Revision.Next();

    MatchEvent[] events =
    [
      new ParticipantJoinedEvent(
        command.CommandId,
        command.MatchId,
        nextRevision,
        command.ParticipantId)
    ];

    var reduced = Reduce(state, events);

    return new MatchTransition(
      reduced,
      new ReadOnlyCollection<MatchEvent>(events));
  }

  public static MatchState Reduce(
    MatchState state,
    IEnumerable<MatchEvent> events)
  {
    ArgumentNullException.ThrowIfNull(state);
    ArgumentNullException.ThrowIfNull(events);

    var current = state;

    foreach (var domainEvent in events)
    {
      if (domainEvent.MatchId != current.MatchId)
      {
        throw new InvalidOperationException(
          $"Event targets match '{domainEvent.MatchId}', but state belongs to '{current.MatchId}'.");
      }

      var expectedRevision = current.Revision.Next();

      if (domainEvent.Revision != expectedRevision)
      {
        throw new RevisionConflictException(
          expectedRevision,
          domainEvent.Revision);
      }

      current = domainEvent switch
      {
        ParticipantJoinedEvent joined =>
          current.AddParticipant(joined.ParticipantId, joined.Revision),
        _ => throw new InvalidOperationException(
          $"Unsupported event type '{domainEvent.GetType().Name}'.")
      };
    }

    return current;
  }
}
