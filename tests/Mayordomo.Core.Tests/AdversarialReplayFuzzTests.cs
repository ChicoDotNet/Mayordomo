using Xunit;

namespace Mayordomo.Core.Tests;

public sealed class AdversarialReplayFuzzTests
{
  [Fact]
  public void Deterministic_event_corruption_fails_closed_across_many_seeds()
  {
    const int seedCount = 64;
    const int mutationCount = 6;

    for (var seed = 0; seed < seedCount; seed++)
    {
      var fixture = BuildFixture(
        unchecked(((ulong)seed * 65537UL) + 23UL));

      for (var mutation = 0;
           mutation < mutationCount;
           mutation++)
      {
        var corrupted = Mutate(
          fixture.Events,
          mutation);

        var rejected = false;

        try
        {
          _ = MatchEngine.Reduce(
            fixture.InitialState,
            corrupted);
        }
        catch (InvalidOperationException)
        {
          rejected = true;
        }

        Assert.True(
          rejected,
          $"Seed {seed}, mutation {mutation} was not rejected.");
      }
    }
  }

  [Fact]
  public void Structural_event_corruption_is_caught_by_formal_invariants()
  {
    var fixture = BuildFixture(42UL);
    var events = fixture.Events.ToArray();
    var deckIndex = FindEventIndex<DeckCreatedEvent>(
      events);
    var created =
      (DeckCreatedEvent)events[deckIndex];
    var duplicate =
      DeckItemId.Create("duplicate-card");

    events[deckIndex] = created with
    {
      Items =
      [
        duplicate,
        duplicate
      ]
    };

    var replayed = MatchEngine.Reduce(
      fixture.InitialState,
      events.Take(deckIndex + 1));

    var validation =
      MatchInvariantValidator.Validate(
        replayed);

    Assert.False(validation.IsValid);
    Assert.Contains(
      validation.Violations,
      violation =>
        violation.Code ==
        MatchInvariantViolationCode.DuplicateDeckItem);
  }

  private static ReplayFixture BuildFixture(
    ulong seed)
  {
    var initial = MatchState.Create(
      MatchId.Create("fuzz-match"),
      seed);
    var participant =
      ParticipantId.Create("participant-a");
    var events = new List<MatchEvent>();
    var current = initial;

    current = Apply(
      events,
      MatchEngine.Execute(
        current,
        JoinParticipantCommand.Create(
          CommandId.Create("join"),
          current.MatchId,
          current.Revision,
          participant)));

    current = Apply(
      events,
      MatchEngine.Execute(
        current,
        PlaceParticipantCommand.Create(
          CommandId.Create("place"),
          current.MatchId,
          current.Revision,
          participant,
          PositionId.Create("position-start"))));

    current = Apply(
      events,
      MatchEngine.Execute(
        current,
        CreateDeckCommand.Create(
          CommandId.Create("create-deck"),
          current.MatchId,
          current.Revision,
          DeckId.Create("deck-main"),
          [
            DeckItemId.Create("card-a"),
            DeckItemId.Create("card-b"),
            DeckItemId.Create("card-c"),
            DeckItemId.Create("card-d"),
            DeckItemId.Create("card-e")
          ])));

    var randomTransition =
      MatchEngine.Execute(
        current,
        GenerateRandomIntCommand.Create(
          CommandId.Create("random"),
          current.MatchId,
          current.Revision,
          minInclusive: 1,
          maxExclusive: 7));
    var generated =
      randomTransition.Events[0]
        as RandomIntGeneratedEvent
      ?? throw new InvalidOperationException(
        "Fuzz fixture expected a random event.");

    current = Apply(
      events,
      randomTransition);

    current = Apply(
      events,
      MatchEngine.Execute(
        current,
        MoveParticipantCommand.Create(
          CommandId.Create("move"),
          current.MatchId,
          current.Revision,
          participant,
          PositionId.Create(
            $"position-{generated.Value}"),
          generated.Value)));

    _ = Apply(
      events,
      MatchEngine.Execute(
        current,
        DrawDeckItemCommand.Create(
          CommandId.Create("draw"),
          current.MatchId,
          current.Revision,
          DeckId.Create("deck-main"))));

    return new ReplayFixture(
      initial,
      events.ToArray());
  }

  private static MatchEvent[] Mutate(
    IReadOnlyList<MatchEvent> source,
    int mutation)
  {
    var events = source.ToArray();

    switch (mutation)
    {
      case 0:
      {
        var index =
          FindEventIndex<ParticipantJoinedEvent>(
            events);
        var domainEvent =
          (ParticipantJoinedEvent)events[index];

        events[index] = domainEvent with
        {
          MatchId = MatchId.Create("foreign-match")
        };
        break;
      }

      case 1:
      {
        var index =
          FindEventIndex<ParticipantPlacedEvent>(
            events);
        var domainEvent =
          (ParticipantPlacedEvent)events[index];

        events[index] = domainEvent with
        {
          Revision = Revision.Create(
            checked(domainEvent.Revision.Value + 1))
        };
        break;
      }

      case 2:
      {
        var index =
          FindEventIndex<RandomIntGeneratedEvent>(
            events);
        var domainEvent =
          (RandomIntGeneratedEvent)events[index];
        var range =
          domainEvent.MaxExclusive
          - domainEvent.MinInclusive;
        var offset =
          domainEvent.Value
          - domainEvent.MinInclusive;
        var tamperedValue =
          domainEvent.MinInclusive
          + ((offset + 1) % range);

        events[index] = domainEvent with
        {
          Value = tamperedValue
        };
        break;
      }

      case 3:
      {
        var index =
          FindEventIndex<RandomIntGeneratedEvent>(
            events);
        var domainEvent =
          (RandomIntGeneratedEvent)events[index];

        events[index] = domainEvent with
        {
          RandomStateAfter =
            new RandomState(
              domainEvent.RandomStateAfter.Value ^ 1UL)
        };
        break;
      }

      case 4:
      {
        var index =
          FindEventIndex<ParticipantMovedEvent>(
            events);
        var domainEvent =
          (ParticipantMovedEvent)events[index];

        events[index] = domainEvent with
        {
          FromPositionId =
            PositionId.Create("tampered-from")
        };
        break;
      }

      case 5:
      {
        var index =
          FindEventIndex<DeckItemDrawnEvent>(
            events);
        var domainEvent =
          (DeckItemDrawnEvent)events[index];

        events[index] = domainEvent with
        {
          ItemId =
            DeckItemId.Create("tampered-card")
        };
        break;
      }

      default:
        throw new ArgumentOutOfRangeException(
          nameof(mutation));
    }

    return events;
  }

  private static int FindEventIndex<TEvent>(
    MatchEvent[] events)
    where TEvent : MatchEvent
  {
    for (var index = 0;
         index < events.Length;
         index++)
    {
      if (events[index] is TEvent)
      {
        return index;
      }
    }

    throw new InvalidOperationException(
      $"Event type '{typeof(TEvent).Name}' was not found.");
  }

  private static MatchState Apply(
    List<MatchEvent> events,
    MatchTransition transition)
  {
    events.AddRange(
      transition.Events);

    return transition.State;
  }

  private sealed record ReplayFixture(
    MatchState InitialState,
    IReadOnlyList<MatchEvent> Events);
}
