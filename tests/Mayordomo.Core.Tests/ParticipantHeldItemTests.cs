using Xunit;

namespace Mayordomo.Core.Tests;

public sealed class ParticipantHeldItemTests
{
  [Fact]
  public void Participant_can_receive_and_release_opaque_item_with_exact_replay()
  {
    var initial = MatchState.Create(
      MatchId.Create("match-001"));
    var participant =
      ParticipantId.Create("participant-001");
    var item =
      ParticipantItemId.Create("item-001");

    var joined = MatchEngine.Execute(
      initial,
      JoinParticipantCommand.Create(
        CommandId.Create("join-001"),
        initial.MatchId,
        initial.Revision,
        participant));

    var granted = MatchEngine.Execute(
      joined.State,
      GrantParticipantItemCommand.Create(
        CommandId.Create("grant-001"),
        initial.MatchId,
        joined.State.Revision,
        participant,
        item));

    var grantEvent = Assert.IsType<ParticipantItemGrantedEvent>(
      Assert.Single(granted.Events));

    Assert.Equal(participant, grantEvent.ParticipantId);
    Assert.Equal(item, grantEvent.ItemId);
    Assert.Equal(
      [item],
      granted.State.HeldItems[participant]);

    var revoked = MatchEngine.Execute(
      granted.State,
      RevokeParticipantItemCommand.Create(
        CommandId.Create("revoke-001"),
        initial.MatchId,
        granted.State.Revision,
        participant,
        item));

    var revokeEvent = Assert.IsType<ParticipantItemRevokedEvent>(
      Assert.Single(revoked.Events));

    Assert.Equal(participant, revokeEvent.ParticipantId);
    Assert.Equal(item, revokeEvent.ItemId);
    Assert.Empty(revoked.State.HeldItems[participant]);

    var replayed = MatchEngine.Reduce(
      initial,
      joined.Events
        .Concat(granted.Events)
        .Concat(revoked.Events));

    Assert.Equal(revoked.State.Revision, replayed.Revision);
    Assert.Equal(
      revoked.State.HeldItems[participant],
      replayed.HeldItems[participant]);
  }

  [Fact]
  public void Item_cannot_be_granted_to_non_participant()
  {
    var initial = MatchState.Create(
      MatchId.Create("match-001"));

    Assert.Throws<InvalidOperationException>(
      () => MatchEngine.Execute(
        initial,
        GrantParticipantItemCommand.Create(
          CommandId.Create("grant-001"),
          initial.MatchId,
          initial.Revision,
          ParticipantId.Create("participant-001"),
          ParticipantItemId.Create("item-001"))));
  }

  [Fact]
  public void Same_item_cannot_be_granted_twice_to_same_participant()
  {
    var fixture = CreateJoinedParticipant();
    var item = ParticipantItemId.Create("item-001");

    var granted = MatchEngine.Execute(
      fixture.State,
      GrantParticipantItemCommand.Create(
        CommandId.Create("grant-001"),
        fixture.State.MatchId,
        fixture.State.Revision,
        fixture.Participant,
        item));

    Assert.Throws<InvalidOperationException>(
      () => MatchEngine.Execute(
        granted.State,
        GrantParticipantItemCommand.Create(
          CommandId.Create("grant-002"),
          fixture.State.MatchId,
          granted.State.Revision,
          fixture.Participant,
          item)));
  }

  [Fact]
  public void Item_cannot_be_revoked_when_participant_does_not_hold_it()
  {
    var fixture = CreateJoinedParticipant();

    Assert.Throws<InvalidOperationException>(
      () => MatchEngine.Execute(
        fixture.State,
        RevokeParticipantItemCommand.Create(
          CommandId.Create("revoke-001"),
          fixture.State.MatchId,
          fixture.State.Revision,
          fixture.Participant,
          ParticipantItemId.Create("item-001"))));
  }

  [Fact]
  public void Different_participants_can_hold_same_opaque_item_identifier()
  {
    var initial = MatchState.Create(
      MatchId.Create("match-001"));
    var first = ParticipantId.Create("participant-001");
    var second = ParticipantId.Create("participant-002");
    var item = ParticipantItemId.Create("shared-kind");

    var firstJoined = MatchEngine.Execute(
      initial,
      JoinParticipantCommand.Create(
        CommandId.Create("join-001"),
        initial.MatchId,
        initial.Revision,
        first));

    var secondJoined = MatchEngine.Execute(
      firstJoined.State,
      JoinParticipantCommand.Create(
        CommandId.Create("join-002"),
        initial.MatchId,
        firstJoined.State.Revision,
        second));

    var firstGrant = MatchEngine.Execute(
      secondJoined.State,
      GrantParticipantItemCommand.Create(
        CommandId.Create("grant-001"),
        initial.MatchId,
        secondJoined.State.Revision,
        first,
        item));

    var secondGrant = MatchEngine.Execute(
      firstGrant.State,
      GrantParticipantItemCommand.Create(
        CommandId.Create("grant-002"),
        initial.MatchId,
        firstGrant.State.Revision,
        second,
        item));

    Assert.Equal([item], secondGrant.State.HeldItems[first]);
    Assert.Equal([item], secondGrant.State.HeldItems[second]);
  }

  private static JoinedFixture CreateJoinedParticipant()
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

    return new JoinedFixture(
      joined.State,
      participant);
  }

  private sealed record JoinedFixture(
    MatchState State,
    ParticipantId Participant);
}
