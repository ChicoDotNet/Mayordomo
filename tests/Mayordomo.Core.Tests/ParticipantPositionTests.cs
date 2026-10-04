using Xunit;

namespace Mayordomo.Core.Tests;

public sealed class ParticipantPositionTests
{
  [Fact]
  public void Participant_can_be_placed_moved_and_replayed()
  {
    var initial = MatchState.Create(MatchId.Create("match-001"));
    var participant = ParticipantId.Create("participant-001");

    var joined = MatchEngine.Execute(
      initial,
      JoinParticipantCommand.Create(
        CommandId.Create("join-001"),
        initial.MatchId,
        initial.Revision,
        participant));

    var placed = MatchEngine.Execute(
      joined.State,
      PlaceParticipantCommand.Create(
        CommandId.Create("place-001"),
        initial.MatchId,
        joined.State.Revision,
        participant,
        PositionId.Create("position-start")));

    var placement = Assert.IsType<ParticipantPlacedEvent>(
      Assert.Single(placed.Events));

    Assert.Equal(PositionId.Create("position-start"), placement.PositionId);
    Assert.Equal(Revision.Create(2), placement.Revision);
    Assert.Equal(
      PositionId.Create("position-start"),
      placed.State.Positions[participant]);

    var moved = MatchEngine.Execute(
      placed.State,
      MoveParticipantCommand.Create(
        CommandId.Create("move-001"),
        initial.MatchId,
        placed.State.Revision,
        participant,
        PositionId.Create("position-004"),
        distance: 4));

    var movement = Assert.IsType<ParticipantMovedEvent>(
      Assert.Single(moved.Events));

    Assert.Equal(PositionId.Create("position-start"), movement.FromPositionId);
    Assert.Equal(PositionId.Create("position-004"), movement.ToPositionId);
    Assert.Equal(4, movement.Distance);
    Assert.Equal(Revision.Create(3), movement.Revision);
    Assert.Equal(
      PositionId.Create("position-004"),
      moved.State.Positions[participant]);

    var replayed = MatchEngine.Reduce(
      initial,
      joined.Events
        .Concat(placed.Events)
        .Concat(moved.Events));

    Assert.Equal(moved.State.Revision, replayed.Revision);
    Assert.Equal(moved.State.Positions, replayed.Positions);
  }

  [Fact]
  public void Participant_cannot_be_moved_before_placement()
  {
    var initial = MatchState.Create(MatchId.Create("match-001"));
    var participant = ParticipantId.Create("participant-001");

    var joined = MatchEngine.Execute(
      initial,
      JoinParticipantCommand.Create(
        CommandId.Create("join-001"),
        initial.MatchId,
        initial.Revision,
        participant));

    Assert.Throws<InvalidOperationException>(
      () => MatchEngine.Execute(
        joined.State,
        MoveParticipantCommand.Create(
          CommandId.Create("move-001"),
          initial.MatchId,
          joined.State.Revision,
          participant,
          PositionId.Create("position-004"),
          distance: 4)));
  }

  [Fact]
  public void Participant_cannot_be_placed_twice()
  {
    var initial = MatchState.Create(MatchId.Create("match-001"));
    var participant = ParticipantId.Create("participant-001");

    var joined = MatchEngine.Execute(
      initial,
      JoinParticipantCommand.Create(
        CommandId.Create("join-001"),
        initial.MatchId,
        initial.Revision,
        participant));

    var placed = MatchEngine.Execute(
      joined.State,
      PlaceParticipantCommand.Create(
        CommandId.Create("place-001"),
        initial.MatchId,
        joined.State.Revision,
        participant,
        PositionId.Create("position-start")));

    Assert.Throws<InvalidOperationException>(
      () => MatchEngine.Execute(
        placed.State,
        PlaceParticipantCommand.Create(
          CommandId.Create("place-002"),
          initial.MatchId,
          placed.State.Revision,
          participant,
          PositionId.Create("position-other"))));
  }

  [Fact]
  public void Replay_rejects_movement_from_a_position_that_does_not_match_state()
  {
    var initial = MatchState.Create(MatchId.Create("match-001"));
    var participant = ParticipantId.Create("participant-001");

    var joined = MatchEngine.Execute(
      initial,
      JoinParticipantCommand.Create(
        CommandId.Create("join-001"),
        initial.MatchId,
        initial.Revision,
        participant));

    var placed = MatchEngine.Execute(
      joined.State,
      PlaceParticipantCommand.Create(
        CommandId.Create("place-001"),
        initial.MatchId,
        joined.State.Revision,
        participant,
        PositionId.Create("position-start")));

    MatchEvent[] invalid =
    [
      new ParticipantMovedEvent(
        CommandId.Create("move-001"),
        initial.MatchId,
        Revision.Create(3),
        participant,
        PositionId.Create("position-wrong"),
        PositionId.Create("position-004"),
        Distance: 4)
    ];

    Assert.Throws<InvalidOperationException>(
      () => MatchEngine.Reduce(placed.State, invalid));
  }

  [Fact]
  public void Movement_distance_must_be_positive()
  {
    Assert.Throws<ArgumentOutOfRangeException>(
      () => MoveParticipantCommand.Create(
        CommandId.Create("move-001"),
        MatchId.Create("match-001"),
        Revision.Zero,
        ParticipantId.Create("participant-001"),
        PositionId.Create("position-001"),
        distance: 0));
  }
}
