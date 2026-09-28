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
    IEnumerable<ParticipantId> participants,
    TurnState? turn,
    RandomState randomState)
  {
    MatchId = matchId;
    Revision = revision;
    Participants = new ReadOnlyCollection<ParticipantId>([.. participants]);
    Turn = turn;
    RandomState = randomState;
  }

  public MatchId MatchId { get; }

  public Revision Revision { get; }

  public IReadOnlyList<ParticipantId> Participants { get; }

  public TurnState? Turn { get; }

  public RandomState RandomState { get; }

  public static MatchState Create(MatchId matchId)
  {
    return Create(matchId, randomSeed: 0UL);
  }

  public static MatchState Create(
    MatchId matchId,
    ulong randomSeed)
  {
    return new MatchState(
      matchId,
      Revision.Zero,
      [],
      null,
      new RandomState(randomSeed));
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
      Participants.Append(participantId),
      Turn,
      RandomState);
  }

  internal MatchState StartTurn(
    ParticipantId participantId,
    PhaseId phaseId,
    Revision revision)
  {
    if (!Participants.Contains(participantId))
    {
      throw new InvalidOperationException(
        $"Participant '{participantId}' is not part of match '{MatchId}'.");
    }

    if (Turn is not null)
    {
      throw new InvalidOperationException(
        $"Match '{MatchId}' already has an active turn.");
    }

    return new MatchState(
      MatchId,
      revision,
      Participants,
      new TurnState(participantId, phaseId),
      RandomState);
  }

  internal MatchState EndTurn(
    ParticipantId participantId,
    Revision revision)
  {
    if (Turn is null)
    {
      throw new InvalidOperationException(
        $"Match '{MatchId}' does not have an active turn.");
    }

    if (Turn.ParticipantId != participantId)
    {
      throw new InvalidOperationException(
        $"Participant '{participantId}' cannot end the active turn owned by '{Turn.ParticipantId}'.");
    }

    return new MatchState(
      MatchId,
      revision,
      Participants,
      null,
      RandomState);
  }

  internal MatchState ChangeTurnPhase(
    ParticipantId participantId,
    PhaseId previousPhaseId,
    PhaseId phaseId,
    Revision revision)
  {
    if (Turn is null)
    {
      throw new InvalidOperationException(
        $"Match '{MatchId}' does not have an active turn.");
    }

    if (Turn.ParticipantId != participantId)
    {
      throw new InvalidOperationException(
        $"Participant '{participantId}' cannot change the active turn owned by '{Turn.ParticipantId}'.");
    }

    if (Turn.PhaseId != previousPhaseId)
    {
      throw new InvalidOperationException(
        $"Turn phase '{previousPhaseId}' does not match current phase '{Turn.PhaseId}'.");
    }

    return new MatchState(
      MatchId,
      revision,
      Participants,
      new TurnState(participantId, phaseId),
      RandomState);
  }

  internal MatchState ApplyRandom(
    int minInclusive,
    int maxExclusive,
    int value,
    RandomState randomStateAfter,
    Revision revision)
  {
    var random = DeterministicRandom.Restore(RandomState);
    var expectedValue = random.NextInt32(
      minInclusive,
      maxExclusive);

    if (expectedValue != value ||
        random.State != randomStateAfter)
    {
      throw new InvalidOperationException(
        "Random event does not match the deterministic random state.");
    }

    return new MatchState(
      MatchId,
      revision,
      Participants,
      Turn,
      randomStateAfter);
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

    ValidateCommandTarget(
      state,
      command.MatchId,
      command.ExpectedRevision);

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

  public static MatchTransition Execute(
    MatchState state,
    StartTurnCommand command)
  {
    ArgumentNullException.ThrowIfNull(state);
    ArgumentNullException.ThrowIfNull(command);

    ValidateCommandTarget(
      state,
      command.MatchId,
      command.ExpectedRevision);

    var nextRevision = state.Revision.Next();

    MatchEvent[] events =
    [
      new TurnStartedEvent(
        command.CommandId,
        command.MatchId,
        nextRevision,
        command.ParticipantId,
        command.PhaseId)
    ];

    var reduced = Reduce(state, events);

    return new MatchTransition(
      reduced,
      new ReadOnlyCollection<MatchEvent>(events));
  }

  public static MatchTransition Execute(
    MatchState state,
    GenerateRandomIntCommand command)
  {
    ArgumentNullException.ThrowIfNull(state);
    ArgumentNullException.ThrowIfNull(command);

    ValidateCommandTarget(
      state,
      command.MatchId,
      command.ExpectedRevision);

    var random = DeterministicRandom.Restore(
      state.RandomState);
    var value = random.NextInt32(
      command.MinInclusive,
      command.MaxExclusive);
    var nextRevision = state.Revision.Next();

    MatchEvent[] events =
    [
      new RandomIntGeneratedEvent(
        command.CommandId,
        command.MatchId,
        nextRevision,
        command.MinInclusive,
        command.MaxExclusive,
        value,
        random.State)
    ];

    return new MatchTransition(
      Reduce(state, events),
      new ReadOnlyCollection<MatchEvent>(events));
  }

  public static MatchTransition Execute(
    MatchState state,
    EndTurnCommand command)
  {
    ArgumentNullException.ThrowIfNull(state);
    ArgumentNullException.ThrowIfNull(command);

    ValidateCommandTarget(
      state,
      command.MatchId,
      command.ExpectedRevision);

    var activeTurn = state.Turn
      ?? throw new InvalidOperationException(
        $"Match '{state.MatchId}' does not have an active turn.");

    if (activeTurn.ParticipantId != command.ParticipantId)
    {
      throw new InvalidOperationException(
        $"Participant '{command.ParticipantId}' cannot end the active turn owned by '{activeTurn.ParticipantId}'.");
    }

    var currentIndex = FindParticipantIndex(
      state.Participants,
      command.ParticipantId);

    var nextParticipant = state.Participants[
      (currentIndex + 1) % state.Participants.Count];

    var endedRevision = state.Revision.Next();
    var startedRevision = endedRevision.Next();

    MatchEvent[] events =
    [
      new TurnEndedEvent(
        command.CommandId,
        command.MatchId,
        endedRevision,
        command.ParticipantId),
      new TurnStartedEvent(
        command.CommandId,
        command.MatchId,
        startedRevision,
        nextParticipant,
        command.NextPhaseId)
    ];

    return new MatchTransition(
      Reduce(state, events),
      new ReadOnlyCollection<MatchEvent>(events));
  }

  public static MatchTransition Execute(
    MatchState state,
    ChangeTurnPhaseCommand command)
  {
    ArgumentNullException.ThrowIfNull(state);
    ArgumentNullException.ThrowIfNull(command);

    ValidateCommandTarget(
      state,
      command.MatchId,
      command.ExpectedRevision);

    var activeTurn = state.Turn
      ?? throw new InvalidOperationException(
        $"Match '{state.MatchId}' does not have an active turn.");

    if (activeTurn.ParticipantId != command.ParticipantId)
    {
      throw new InvalidOperationException(
        $"Participant '{command.ParticipantId}' cannot change the active turn owned by '{activeTurn.ParticipantId}'.");
    }

    var nextRevision = state.Revision.Next();

    MatchEvent[] events =
    [
      new TurnPhaseChangedEvent(
        command.CommandId,
        command.MatchId,
        nextRevision,
        command.ParticipantId,
        activeTurn.PhaseId,
        command.PhaseId)
    ];

    return new MatchTransition(
      Reduce(state, events),
      new ReadOnlyCollection<MatchEvent>(events));
  }

  public static LegalActionSet QueryLegalActions(
    MatchState state,
    ParticipantId actor,
    ILegalActionRules rules)
  {
    ArgumentNullException.ThrowIfNull(state);
    ArgumentNullException.ThrowIfNull(rules);

    if (!state.Participants.Contains(actor))
    {
      throw new InvalidOperationException(
        $"Participant '{actor}' is not part of match '{state.MatchId}'.");
    }

    var actions = rules.Query(state, actor)
      ?? throw new InvalidOperationException(
        "Legal-action rules returned null.");

    return new LegalActionSet(
      actor,
      state.Revision,
      actions);
  }

  private static int FindParticipantIndex(
    IReadOnlyList<ParticipantId> participants,
    ParticipantId participantId)
  {
    for (var index = 0; index < participants.Count; index++)
    {
      if (participants[index] == participantId)
      {
        return index;
      }
    }

    throw new InvalidOperationException(
      $"Participant '{participantId}' is not part of the match.");
  }

  private static void ValidateCommandTarget(
    MatchState state,
    MatchId matchId,
    Revision expectedRevision)
  {
    if (state.MatchId != matchId)
    {
      throw new InvalidOperationException(
        $"Command targets match '{matchId}', but state belongs to '{state.MatchId}'.");
    }

    if (state.Revision != expectedRevision)
    {
      throw new RevisionConflictException(
        expectedRevision,
        state.Revision);
    }
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
        TurnStartedEvent started =>
          current.StartTurn(
            started.ParticipantId,
            started.PhaseId,
            started.Revision),
        TurnEndedEvent ended =>
          current.EndTurn(
            ended.ParticipantId,
            ended.Revision),
        TurnPhaseChangedEvent changed =>
          current.ChangeTurnPhase(
            changed.ParticipantId,
            changed.PreviousPhaseId,
            changed.PhaseId,
            changed.Revision),
        RandomIntGeneratedEvent generated =>
          current.ApplyRandom(
            generated.MinInclusive,
            generated.MaxExclusive,
            generated.Value,
            generated.RandomStateAfter,
            generated.Revision),
        _ => throw new InvalidOperationException(
          $"Unsupported event type '{domainEvent.GetType().Name}'.")
      };
    }

    return current;
  }
}
