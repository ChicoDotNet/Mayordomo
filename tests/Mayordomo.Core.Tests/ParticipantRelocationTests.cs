using Xunit;

namespace Mayordomo.Core.Tests;

public sealed class ParticipantRelocationTests
{
  [Fact]
  public void Participant_can_be_relocated_without_distance_and_replayed()
  {
    var initial = MatchState.Create(
      MatchId.Create("match-001"));
    var participant =
      ParticipantId.Create("participant-001");

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
        PositionId.Create("position-004")));

    var relocated = MatchEngine.Execute(
      placed.State,
      RelocateParticipantCommand.Create(
        CommandId.Create("relocate-001"),
        initial.MatchId,
        placed.State.Revision,
        participant,
        PositionId.Create("position-special")));

    var domainEvent =
      Assert.IsType<ParticipantRelocatedEvent>(
        Assert.Single(relocated.Events));

    Assert.Equal(
      PositionId.Create("position-004"),
      domainEvent.FromPositionId);
    Assert.Equal(
      PositionId.Create("position-special"),
      domainEvent.ToPositionId);
    Assert.Equal(
      Revision.Create(3),
      domainEvent.Revision);
    Assert.Equal(
      PositionId.Create("position-special"),
      relocated.State.Positions[participant]);

    var replayed = MatchEngine.Reduce(
      initial,
      joined.Events
        .Concat(placed.Events)
        .Concat(relocated.Events));

    Assert.Equal(
      relocated.State.Revision,
      replayed.Revision);
    Assert.Equal(
      relocated.State.Positions[participant],
      replayed.Positions[participant]);
  }

  [Fact]
  public void Participant_cannot_be_relocated_before_placement()
  {
    var initial = MatchState.Create(
      MatchId.Create("match-001"));
    var participant =
      ParticipantId.Create("participant-001");

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
        RelocateParticipantCommand.Create(
          CommandId.Create("relocate-001"),
          initial.MatchId,
          joined.State.Revision,
          participant,
          PositionId.Create("position-special"))));
  }

  [Fact]
  public void Replay_rejects_relocation_from_wrong_source_position()
  {
    var initial = MatchState.Create(
      MatchId.Create("match-001"));
    var participant =
      ParticipantId.Create("participant-001");

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
        PositionId.Create("position-004")));

    MatchEvent[] invalid =
    [
      new ParticipantRelocatedEvent(
        CommandId.Create("relocate-001"),
        initial.MatchId,
        Revision.Create(3),
        participant,
        PositionId.Create("position-wrong"),
        PositionId.Create("position-special"))
    ];

    Assert.Throws<InvalidOperationException>(
      () => MatchEngine.Reduce(
        placed.State,
        invalid));
  }
}
