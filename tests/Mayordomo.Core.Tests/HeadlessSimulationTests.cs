using Xunit;

namespace Mayordomo.Core.Tests;

public sealed class HeadlessSimulationTests
{
  private static readonly ActionId RandomAction = ActionId.Create("a-random");
  private static readonly ActionId EndTurnAction = ActionId.Create("b-end-turn");
  private static readonly PhaseId MainPhase = PhaseId.Create("phase-main");

  [Fact]
  public void First_legal_action_bot_uses_ordinal_action_order()
  {
    var state = CreateStartedMatch(seed: 17UL);
    var rules = new FixedRules([EndTurnAction, RandomAction]);
    var legal = MatchEngine.QueryLegalActions(
      state,
      state.Turn!.ParticipantId,
      rules);

    var selected = FirstLegalActionBot.Instance.Choose(
      state,
      legal);

    Assert.Equal(RandomAction, selected);
  }

  [Fact]
  public void Revision_round_robin_bot_is_deterministic()
  {
    var state = CreateStartedMatch(seed: 17UL);
    var rules = new FixedRules([EndTurnAction, RandomAction]);
    var legal = MatchEngine.QueryLegalActions(
      state,
      state.Turn!.ParticipantId,
      rules);

    var bot = RevisionRoundRobinBot.Instance;
    var first = bot.Choose(state, legal);
    var second = bot.Choose(state, legal);

    Assert.Equal(first, second);

    var ordered = new[] { RandomAction, EndTurnAction };
    var expected = ordered[
      (int)(state.Revision.Value % ordered.Length)];

    Assert.Equal(expected, first);
  }

  [Fact]
  public void Runner_executes_without_UI_and_collects_authoritative_events()
  {
    var initial = CreateStartedMatch(seed: 17UL);
    var bots = initial.Participants.ToDictionary(
      participant => participant,
      _ => (IHeadlessBot)FirstLegalActionBot.Instance);

    var result = HeadlessSimulation.Run(
      initial,
      new FixedRules([EndTurnAction]),
      bots,
      ExecuteAction,
      maxSteps: 10);

    Assert.Equal(10, result.Steps);
    Assert.False(result.ReachedTerminal);
    Assert.Equal(20, result.Events.Count);
    Assert.Equal(
      Revision.Create(initial.Revision.Value + 20),
      result.FinalState.Revision);
    Assert.Equal(initial.MatchId, result.FinalState.MatchId);
  }

  [Fact]
  public void Hundreds_of_seeded_runs_are_repeatable()
  {
    const int scenarioCount = 128;

    for (var scenario = 0; scenario < scenarioCount; scenario++)
    {
      var seed = ((ulong)scenario * 104729UL) + 17UL;
      var first = RunRandomScenario(seed);
      var second = RunRandomScenario(seed);

      Assert.Equal(first.Steps, second.Steps);
      Assert.Equal(
        first.FinalState.RandomState,
        second.FinalState.RandomState);
      Assert.Equal(
        first.FinalState.Revision,
        second.FinalState.Revision);

      var firstValues = first.Events
        .OfType<RandomIntGeneratedEvent>()
        .Select(domainEvent => (
          domainEvent.Value,
          domainEvent.RandomStateAfter))
        .ToArray();
      var secondValues = second.Events
        .OfType<RandomIntGeneratedEvent>()
        .Select(domainEvent => (
          domainEvent.Value,
          domainEvent.RandomStateAfter))
        .ToArray();

      Assert.Equal(firstValues, secondValues);
    }
  }

  [Fact]
  public void Terminal_predicate_stops_a_run_before_the_step_budget()
  {
    var initial = CreateStartedMatch(seed: 91UL);
    var bots = initial.Participants.ToDictionary(
      participant => participant,
      _ => (IHeadlessBot)FirstLegalActionBot.Instance);
    var terminalRevision = initial.Revision.Value + 12;

    var result = HeadlessSimulation.Run(
      initial,
      new FixedRules([RandomAction]),
      bots,
      ExecuteAction,
      maxSteps: 100,
      isTerminal: state =>
        state.Revision.Value >= terminalRevision);

    Assert.True(result.ReachedTerminal);
    Assert.Equal(12, result.Steps);
    Assert.Equal(
      Revision.Create(terminalRevision),
      result.FinalState.Revision);
  }

  private static HeadlessSimulationResult RunRandomScenario(
    ulong seed)
  {
    var initial = CreateStartedMatch(seed);
    var bots = initial.Participants.ToDictionary(
      participant => participant,
      _ => (IHeadlessBot)FirstLegalActionBot.Instance);

    return HeadlessSimulation.Run(
      initial,
      new FixedRules([RandomAction, EndTurnAction]),
      bots,
      ExecuteAction,
      maxSteps: 32);
  }

  private static MatchTransition ExecuteAction(
    MatchState state,
    ParticipantId actor,
    ActionId action,
    int step)
  {
    if (action == RandomAction)
    {
      return MatchEngine.Execute(
        state,
        GenerateRandomIntCommand.Create(
          CommandId.Create($"simulation-random-{step:D4}"),
          state.MatchId,
          state.Revision,
          minInclusive: 1,
          maxExclusive: 7));
    }

    if (action == EndTurnAction)
    {
      return MatchEngine.Execute(
        state,
        EndTurnCommand.Create(
          CommandId.Create($"simulation-end-{step:D4}"),
          state.MatchId,
          state.Revision,
          actor,
          MainPhase));
    }

    throw new InvalidOperationException(
      $"Unknown synthetic action '{action}'.");
  }

  private static MatchState CreateStartedMatch(ulong seed)
  {
    var state = MatchState.Create(
      MatchId.Create($"simulation-{seed}"),
      seed);
    var participantOne = ParticipantId.Create("participant-001");
    var participantTwo = ParticipantId.Create("participant-002");

    state = MatchEngine.Execute(
      state,
      JoinParticipantCommand.Create(
        CommandId.Create("join-001"),
        state.MatchId,
        state.Revision,
        participantOne)).State;

    state = MatchEngine.Execute(
      state,
      JoinParticipantCommand.Create(
        CommandId.Create("join-002"),
        state.MatchId,
        state.Revision,
        participantTwo)).State;

    return MatchEngine.Execute(
      state,
      StartTurnCommand.Create(
        CommandId.Create("start-turn"),
        state.MatchId,
        state.Revision,
        participantOne,
        MainPhase)).State;
  }

  private sealed class FixedRules(
    IReadOnlyList<ActionId> actions)
    : ILegalActionRules
  {
    public IReadOnlyList<ActionId> Query(
      MatchState state,
      ParticipantId actor)
    {
      _ = state;
      _ = actor;
      return actions;
    }
  }
}
