using Xunit;

namespace Mayordomo.Core.Tests;

public sealed class TurnPhaseTransitionTests
{
  [Fact]
  public void Current_participant_can_change_phase_and_replay_the_transition()
  {
    var fixture = CreateStartedTwoParticipantMatch();

    var transitioned = MatchEngine.Execute(
      fixture.State,
      ChangeTurnPhaseCommand.Create(
        CommandId.Create("command-004"),
        fixture.State.MatchId,
        fixture.State.Revision,
        fixture.First,
        PhaseId.Create("resolving-action")));

    var changed = Assert.IsType<TurnPhaseChangedEvent>(
      Assert.Single(transitioned.Events));

    Assert.Equal(fixture.First, changed.ParticipantId);
    Assert.Equal(PhaseId.Create("awaiting-action"), changed.PreviousPhaseId);
    Assert.Equal(PhaseId.Create("resolving-action"), changed.PhaseId);
    Assert.Equal(Revision.Create(4), changed.Revision);
    Assert.Equal(
      PhaseId.Create("resolving-action"),
      transitioned.State.Turn?.PhaseId);

    var replayed = MatchEngine.Reduce(
      fixture.Initial,
      fixture.History.Concat(transitioned.Events));

    Assert.Equal(transitioned.State.Revision, replayed.Revision);
    Assert.Equal(transitioned.State.Participants, replayed.Participants);
    Assert.Equal(transitioned.State.Turn, replayed.Turn);
  }

  [Fact]
  public void Waiting_participant_cannot_change_the_active_turn_phase()
  {
    var fixture = CreateStartedTwoParticipantMatch();

    var error = Assert.Throws<InvalidOperationException>(
      () => MatchEngine.Execute(
        fixture.State,
        ChangeTurnPhaseCommand.Create(
          CommandId.Create("command-004"),
          fixture.State.MatchId,
          fixture.State.Revision,
          fixture.Second,
          PhaseId.Create("resolving-action"))));

    Assert.Contains(
      fixture.First.ToString(),
      error.Message,
      StringComparison.Ordinal);
  }

  private static StartedMatchFixture CreateStartedTwoParticipantMatch()
  {
    var initial = MatchState.Create(MatchId.Create("match-001"));
    var first = ParticipantId.Create("participant-001");
    var second = ParticipantId.Create("participant-002");

    var firstJoin = MatchEngine.Execute(
      initial,
      JoinParticipantCommand.Create(
        CommandId.Create("command-001"),
        initial.MatchId,
        initial.Revision,
        first));

    var secondJoin = MatchEngine.Execute(
      firstJoin.State,
      JoinParticipantCommand.Create(
        CommandId.Create("command-002"),
        initial.MatchId,
        firstJoin.State.Revision,
        second));

    var started = MatchEngine.Execute(
      secondJoin.State,
      StartTurnCommand.Create(
        CommandId.Create("command-003"),
        initial.MatchId,
        secondJoin.State.Revision,
        first,
        PhaseId.Create("awaiting-action")));

    return new StartedMatchFixture(
      initial,
      started.State,
      first,
      second,
      firstJoin.Events
        .Concat(secondJoin.Events)
        .Concat(started.Events)
        .ToArray());
  }

  private sealed record StartedMatchFixture(
    MatchState Initial,
    MatchState State,
    ParticipantId First,
    ParticipantId Second,
    IReadOnlyList<MatchEvent> History);
}
