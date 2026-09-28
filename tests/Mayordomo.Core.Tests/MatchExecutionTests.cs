using Xunit;

namespace Mayordomo.Core.Tests;

public sealed class MatchExecutionTests
{
  [Fact]
  public void Join_participant_produces_event_advances_revision_and_replays()
  {
    var initial = MatchState.Create(MatchId.Create("match-001"));
    var command = JoinParticipantCommand.Create(
      commandId: CommandId.Create("command-001"),
      matchId: initial.MatchId,
      expectedRevision: initial.Revision,
      participantId: ParticipantId.Create("participant-001"));

    var transition = MatchEngine.Execute(initial, command);

    Assert.Equal(Revision.Zero, initial.Revision);
    Assert.Equal(Revision.Create(1), transition.State.Revision);

    var joined = Assert.IsType<ParticipantJoinedEvent>(
      Assert.Single(transition.Events));

    Assert.Equal(command.CommandId, joined.CommandId);
    Assert.Equal(initial.MatchId, joined.MatchId);
    Assert.Equal(command.ParticipantId, joined.ParticipantId);
    Assert.Equal(Revision.Create(1), joined.Revision);

    Assert.Contains(command.ParticipantId, transition.State.Participants);

    var replayed = MatchEngine.Reduce(initial, transition.Events);

    Assert.Equal(transition.State.Revision, replayed.Revision);
    Assert.Equal(transition.State.Participants, replayed.Participants);
  }

  [Fact]
  public void Same_state_and_command_produce_the_same_event_and_state()
  {
    var initial = MatchState.Create(MatchId.Create("match-001"));
    var command = JoinParticipantCommand.Create(
      commandId: CommandId.Create("command-001"),
      matchId: initial.MatchId,
      expectedRevision: initial.Revision,
      participantId: ParticipantId.Create("participant-001"));

    var first = MatchEngine.Execute(initial, command);
    var second = MatchEngine.Execute(initial, command);

    Assert.Equal(first.Events, second.Events);
    Assert.Equal(first.State.Revision, second.State.Revision);
    Assert.Equal(first.State.Participants, second.State.Participants);
  }

  [Fact]
  public void Command_against_stale_revision_is_rejected()
  {
    var initial = MatchState.Create(MatchId.Create("match-001"));

    var first = MatchEngine.Execute(
      initial,
      JoinParticipantCommand.Create(
        CommandId.Create("command-001"),
        initial.MatchId,
        initial.Revision,
        ParticipantId.Create("participant-001")));

    var stale = JoinParticipantCommand.Create(
      CommandId.Create("command-002"),
      initial.MatchId,
      expectedRevision: initial.Revision,
      ParticipantId.Create("participant-002"));

    var error = Assert.Throws<RevisionConflictException>(
      () => MatchEngine.Execute(first.State, stale));

    Assert.Equal(initial.Revision, error.Expected);
    Assert.Equal(first.State.Revision, error.Actual);
  }
}
