using Xunit;

namespace Mayordomo.Core.Tests;

public sealed class RandomEventTurnRotationTests
{
  [Fact]
  public void Random_command_records_value_and_advances_persisted_random_state()
  {
    var initial = MatchState.Create(
      MatchId.Create("match-001"),
      randomSeed: 0x5EEDUL);

    var first = MatchEngine.Execute(
      initial,
      GenerateRandomIntCommand.Create(
        CommandId.Create("command-001"),
        initial.MatchId,
        initial.Revision,
        minInclusive: 1,
        maxExclusive: 7));

    var generated = Assert.IsType<RandomIntGeneratedEvent>(
      Assert.Single(first.Events));

    Assert.Equal(5, generated.Value);
    Assert.Equal(1, generated.MinInclusive);
    Assert.Equal(7, generated.MaxExclusive);
    Assert.Equal(first.State.RandomState, generated.RandomStateAfter);
    Assert.Equal(Revision.Create(1), first.State.Revision);

    var replayed = MatchEngine.Reduce(initial, first.Events);

    Assert.Equal(first.State.Revision, replayed.Revision);
    Assert.Equal(first.State.RandomState, replayed.RandomState);

    var second = MatchEngine.Execute(
      replayed,
      GenerateRandomIntCommand.Create(
        CommandId.Create("command-002"),
        replayed.MatchId,
        replayed.Revision,
        minInclusive: 1,
        maxExclusive: 7));

    var secondGenerated = Assert.IsType<RandomIntGeneratedEvent>(
      Assert.Single(second.Events));

    Assert.Equal(6, secondGenerated.Value);
  }

  [Fact]
  public void Replay_rejects_a_tampered_random_result()
  {
    var initial = MatchState.Create(
      MatchId.Create("match-001"),
      randomSeed: 0x5EEDUL);

    var transition = MatchEngine.Execute(
      initial,
      GenerateRandomIntCommand.Create(
        CommandId.Create("command-001"),
        initial.MatchId,
        initial.Revision,
        minInclusive: 1,
        maxExclusive: 7));

    var generated = Assert.IsType<RandomIntGeneratedEvent>(
      Assert.Single(transition.Events));

    var tampered = generated with
    {
      Value = generated.Value == 6
        ? 5
        : 6
    };

    var error = Assert.Throws<InvalidOperationException>(
      () => MatchEngine.Reduce(
        initial,
        [tampered]));

    Assert.Contains(
      "deterministic random state",
      error.Message,
      StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void End_turn_rotates_to_the_next_participant_and_replays()
  {
    var fixture = CreateStartedTwoParticipantMatch();

    var ended = MatchEngine.Execute(
      fixture.State,
      EndTurnCommand.Create(
        CommandId.Create("command-004"),
        fixture.State.MatchId,
        fixture.State.Revision,
        fixture.First,
        nextPhaseId: PhaseId.Create("awaiting-action")));

    Assert.Collection(
      ended.Events,
      domainEvent =>
      {
        var turnEnded = Assert.IsType<TurnEndedEvent>(domainEvent);
        Assert.Equal(fixture.First, turnEnded.ParticipantId);
        Assert.Equal(Revision.Create(4), turnEnded.Revision);
      },
      domainEvent =>
      {
        var turnStarted = Assert.IsType<TurnStartedEvent>(domainEvent);
        Assert.Equal(fixture.Second, turnStarted.ParticipantId);
        Assert.Equal(PhaseId.Create("awaiting-action"), turnStarted.PhaseId);
        Assert.Equal(Revision.Create(5), turnStarted.Revision);
      });

    Assert.Equal(fixture.Second, ended.State.Turn?.ParticipantId);
    Assert.Equal(
      PhaseId.Create("awaiting-action"),
      ended.State.Turn?.PhaseId);

    var replayed = MatchEngine.Reduce(
      fixture.Initial,
      fixture.History.Concat(ended.Events));

    Assert.Equal(ended.State.Revision, replayed.Revision);
    Assert.Equal(ended.State.Participants, replayed.Participants);
    Assert.Equal(ended.State.Turn, replayed.Turn);
  }

  [Fact]
  public void Non_current_participant_cannot_end_the_active_turn()
  {
    var fixture = CreateStartedTwoParticipantMatch();

    var error = Assert.Throws<InvalidOperationException>(
      () => MatchEngine.Execute(
        fixture.State,
        EndTurnCommand.Create(
          CommandId.Create("command-004"),
          fixture.State.MatchId,
          fixture.State.Revision,
          fixture.Second,
          nextPhaseId: PhaseId.Create("awaiting-action"))));

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
