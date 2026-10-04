using Xunit;

namespace Mayordomo.Core.Tests;

public sealed class CommandIdempotencyTests
{
  [Fact]
  public void Retrying_the_same_command_after_success_is_a_no_op()
  {
    var initial = MatchState.Create(MatchId.Create("match-001"));
    var command = JoinParticipantCommand.Create(
      CommandId.Create("command-001"),
      initial.MatchId,
      initial.Revision,
      ParticipantId.Create("participant-001"));

    var first = MatchEngine.Execute(initial, command);
    var retry = MatchEngine.Execute(first.State, command);

    Assert.False(first.IsDuplicate);
    Assert.True(retry.IsDuplicate);
    Assert.Empty(retry.Events);
    Assert.Same(first.State, retry.State);
    Assert.Equal(Revision.Create(1), retry.State.Revision);
    Assert.Single(retry.State.Participants);
  }

  [Fact]
  public void Retry_remains_idempotent_after_later_commands_advance_revision()
  {
    var initial = MatchState.Create(MatchId.Create("match-001"));
    var participant = ParticipantId.Create("participant-001");

    var join = JoinParticipantCommand.Create(
      CommandId.Create("command-001"),
      initial.MatchId,
      initial.Revision,
      participant);

    var joined = MatchEngine.Execute(initial, join);

    var placed = MatchEngine.Execute(
      joined.State,
      PlaceParticipantCommand.Create(
        CommandId.Create("command-002"),
        initial.MatchId,
        joined.State.Revision,
        participant,
        PositionId.Create("position-001")));

    var retry = MatchEngine.Execute(placed.State, join);

    Assert.True(retry.IsDuplicate);
    Assert.Empty(retry.Events);
    Assert.Same(placed.State, retry.State);
    Assert.Equal(Revision.Create(2), retry.State.Revision);
  }

  [Fact]
  public void Reusing_a_command_id_for_different_intent_is_rejected()
  {
    var initial = MatchState.Create(MatchId.Create("match-001"));

    var first = MatchEngine.Execute(
      initial,
      JoinParticipantCommand.Create(
        CommandId.Create("command-001"),
        initial.MatchId,
        initial.Revision,
        ParticipantId.Create("participant-001")));

    var conflicting = JoinParticipantCommand.Create(
      CommandId.Create("command-001"),
      initial.MatchId,
      first.State.Revision,
      ParticipantId.Create("participant-002"));

    var error = Assert.Throws<CommandIdConflictException>(
      () => MatchEngine.Execute(first.State, conflicting));

    Assert.Equal(CommandId.Create("command-001"), error.CommandId);
  }

  [Fact]
  public void Replay_reconstructs_the_idempotency_ledger()
  {
    var initial = MatchState.Create(MatchId.Create("match-001"));
    var participant = ParticipantId.Create("participant-001");

    var join = JoinParticipantCommand.Create(
      CommandId.Create("command-001"),
      initial.MatchId,
      initial.Revision,
      participant);

    var first = MatchEngine.Execute(initial, join);

    var place = PlaceParticipantCommand.Create(
      CommandId.Create("command-002"),
      initial.MatchId,
      first.State.Revision,
      participant,
      PositionId.Create("position-001"));

    var second = MatchEngine.Execute(first.State, place);

    var replayed = MatchEngine.Reduce(
      initial,
      first.Events.Concat(second.Events));

    var retry = MatchEngine.Execute(replayed, join);

    Assert.True(retry.IsDuplicate);
    Assert.Empty(retry.Events);
    Assert.Same(replayed, retry.State);
    Assert.Equal(second.State.Revision, replayed.Revision);
  }

  [Fact]
  public void Duplicate_random_command_does_not_advance_random_state()
  {
    var initial = MatchState.Create(
      MatchId.Create("match-001"),
      randomSeed: 123456UL);

    var command = GenerateRandomIntCommand.Create(
      CommandId.Create("command-random"),
      initial.MatchId,
      initial.Revision,
      minInclusive: 1,
      maxExclusive: 7);

    var first = MatchEngine.Execute(initial, command);
    var retry = MatchEngine.Execute(first.State, command);

    Assert.True(retry.IsDuplicate);
    Assert.Empty(retry.Events);
    Assert.Equal(first.State.RandomState, retry.State.RandomState);
    Assert.Equal(first.State.Revision, retry.State.Revision);
  }

  [Fact]
  public void Multi_event_command_is_idempotent_after_replay()
  {
    var initial = MatchState.Create(MatchId.Create("match-001"));
    var participantOne = ParticipantId.Create("participant-001");
    var participantTwo = ParticipantId.Create("participant-002");
    var phase = PhaseId.Create("phase-main");

    var joinOne = MatchEngine.Execute(
      initial,
      JoinParticipantCommand.Create(
        CommandId.Create("join-001"),
        initial.MatchId,
        initial.Revision,
        participantOne));

    var joinTwo = MatchEngine.Execute(
      joinOne.State,
      JoinParticipantCommand.Create(
        CommandId.Create("join-002"),
        initial.MatchId,
        joinOne.State.Revision,
        participantTwo));

    var start = MatchEngine.Execute(
      joinTwo.State,
      StartTurnCommand.Create(
        CommandId.Create("start-001"),
        initial.MatchId,
        joinTwo.State.Revision,
        participantOne,
        phase));

    var endCommand = EndTurnCommand.Create(
      CommandId.Create("end-001"),
      initial.MatchId,
      start.State.Revision,
      participantOne,
      phase);

    var ended = MatchEngine.Execute(start.State, endCommand);

    var history = joinOne.Events
      .Concat(joinTwo.Events)
      .Concat(start.Events)
      .Concat(ended.Events);

    var replayed = MatchEngine.Reduce(initial, history);
    var retry = MatchEngine.Execute(replayed, endCommand);

    Assert.True(retry.IsDuplicate);
    Assert.Empty(retry.Events);
    Assert.Same(replayed, retry.State);
    Assert.Equal(ended.State.Revision, retry.State.Revision);
    Assert.Equal(ended.State.Turn, retry.State.Turn);
  }
}
