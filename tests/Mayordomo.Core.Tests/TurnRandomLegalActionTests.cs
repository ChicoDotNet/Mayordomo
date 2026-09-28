using Xunit;

namespace Mayordomo.Core.Tests;

public sealed class TurnRandomLegalActionTests
{
  [Fact]
  public void Two_participants_can_reach_an_explicit_turn_and_phase()
  {
    var initial = MatchState.Create(MatchId.Create("match-001"));
    var firstId = ParticipantId.Create("participant-001");
    var secondId = ParticipantId.Create("participant-002");

    var firstJoin = MatchEngine.Execute(
      initial,
      JoinParticipantCommand.Create(
        CommandId.Create("command-001"),
        initial.MatchId,
        initial.Revision,
        firstId));

    var secondJoin = MatchEngine.Execute(
      firstJoin.State,
      JoinParticipantCommand.Create(
        CommandId.Create("command-002"),
        initial.MatchId,
        firstJoin.State.Revision,
        secondId));

    var startTurn = MatchEngine.Execute(
      secondJoin.State,
      StartTurnCommand.Create(
        CommandId.Create("command-003"),
        initial.MatchId,
        secondJoin.State.Revision,
        firstId,
        PhaseId.Create("awaiting-action")));

    var started = Assert.IsType<TurnStartedEvent>(
      Assert.Single(startTurn.Events));

    Assert.Equal(firstId, started.ParticipantId);
    Assert.Equal(PhaseId.Create("awaiting-action"), started.PhaseId);
    Assert.Equal(Revision.Create(3), started.Revision);
    Assert.Equal(firstId, startTurn.State.Turn?.ParticipantId);
    Assert.Equal(
      PhaseId.Create("awaiting-action"),
      startTurn.State.Turn?.PhaseId);

    var completeHistory = firstJoin.Events
      .Concat(secondJoin.Events)
      .Concat(startTurn.Events);

    var replayed = MatchEngine.Reduce(initial, completeHistory);

    Assert.Equal(startTurn.State.Revision, replayed.Revision);
    Assert.Equal(startTurn.State.Participants, replayed.Participants);
    Assert.Equal(startTurn.State.Turn, replayed.Turn);
  }

  [Fact]
  public void Legal_actions_are_bound_to_actor_and_state_revision()
  {
    var state = CreateStartedTwoParticipantMatch();
    var current = state.Turn?.ParticipantId
      ?? throw new InvalidOperationException("Expected an active turn.");
    var waiting = state.Participants.Single(id => id != current);
    var roll = ActionId.Create("roll");

    var rules = new CurrentTurnActionRules(roll);

    var currentActions = MatchEngine.QueryLegalActions(
      state,
      current,
      rules);

    var waitingActions = MatchEngine.QueryLegalActions(
      state,
      waiting,
      rules);

    Assert.Equal(state.Revision, currentActions.Revision);
    Assert.Equal(current, currentActions.Actor);
    Assert.Contains(roll, currentActions.Actions);
    Assert.Empty(waitingActions.Actions);

    currentActions.EnsureAllowed(roll);

    var error = Assert.Throws<IllegalActionException>(
      () => waitingActions.EnsureAllowed(roll));

    Assert.Equal(waiting, error.Actor);
    Assert.Equal(roll, error.Action);
    Assert.Equal(state.Revision, error.Revision);
  }

  [Fact]
  public void Same_seed_produces_same_bounded_random_sequence()
  {
    var first = DeterministicRandom.Create(0x5EEDUL);
    var second = DeterministicRandom.Create(0x5EEDUL);

    var firstSequence = Enumerable
      .Range(0, 32)
      .Select(_ => first.NextInt32(1, 7))
      .ToArray();

    var secondSequence = Enumerable
      .Range(0, 32)
      .Select(_ => second.NextInt32(1, 7))
      .ToArray();

    Assert.Equal(firstSequence, secondSequence);
    Assert.All(firstSequence, value => Assert.InRange(value, 1, 6));
  }

  private static MatchState CreateStartedTwoParticipantMatch()
  {
    var initial = MatchState.Create(MatchId.Create("match-001"));
    var firstId = ParticipantId.Create("participant-001");
    var secondId = ParticipantId.Create("participant-002");

    var firstJoin = MatchEngine.Execute(
      initial,
      JoinParticipantCommand.Create(
        CommandId.Create("command-001"),
        initial.MatchId,
        initial.Revision,
        firstId));

    var secondJoin = MatchEngine.Execute(
      firstJoin.State,
      JoinParticipantCommand.Create(
        CommandId.Create("command-002"),
        initial.MatchId,
        firstJoin.State.Revision,
        secondId));

    return MatchEngine.Execute(
      secondJoin.State,
      StartTurnCommand.Create(
        CommandId.Create("command-003"),
        initial.MatchId,
        secondJoin.State.Revision,
        firstId,
        PhaseId.Create("awaiting-action"))).State;
  }

  private sealed class CurrentTurnActionRules(ActionId action)
    : ILegalActionRules
  {
    public IReadOnlyList<ActionId> Query(
      MatchState state,
      ParticipantId actor)
    {
      return state.Turn?.ParticipantId == actor
        ? [action]
        : [];
    }
  }
}
