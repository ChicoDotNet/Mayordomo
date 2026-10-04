using System.Collections.ObjectModel;

namespace Mayordomo.Core;

public interface IHeadlessBot
{
  ActionId Choose(
    MatchState state,
    LegalActionSet legalActions);
}

public sealed class FirstLegalActionBot : IHeadlessBot
{
  private FirstLegalActionBot()
  {
  }

  public static FirstLegalActionBot Instance { get; } = new();

  public ActionId Choose(
    MatchState state,
    LegalActionSet legalActions)
  {
    ArgumentNullException.ThrowIfNull(state);
    ArgumentNullException.ThrowIfNull(legalActions);

    return OrderedActions(legalActions)[0];
  }

  private static ActionId[] OrderedActions(
    LegalActionSet legalActions)
  {
    if (legalActions.Actions.Count == 0)
    {
      throw new InvalidOperationException(
        "A headless bot cannot choose from an empty legal-action set.");
    }

    return legalActions.Actions
      .OrderBy(action => action.Value, StringComparer.Ordinal)
      .ToArray();
  }
}

public sealed class RevisionRoundRobinBot : IHeadlessBot
{
  private RevisionRoundRobinBot()
  {
  }

  public static RevisionRoundRobinBot Instance { get; } = new();

  public ActionId Choose(
    MatchState state,
    LegalActionSet legalActions)
  {
    ArgumentNullException.ThrowIfNull(state);
    ArgumentNullException.ThrowIfNull(legalActions);

    if (legalActions.Actions.Count == 0)
    {
      throw new InvalidOperationException(
        "A headless bot cannot choose from an empty legal-action set.");
    }

    var ordered = legalActions.Actions
      .OrderBy(action => action.Value, StringComparer.Ordinal)
      .ToArray();
    var index = (int)(
      state.Revision.Value % ordered.Length);

    return ordered[index];
  }
}

public delegate MatchTransition HeadlessActionExecutor(
  MatchState state,
  ParticipantId actor,
  ActionId action,
  int step);

public delegate bool HeadlessTerminalPredicate(
  MatchState state);

public sealed record HeadlessSimulationResult(
  MatchState FinalState,
  IReadOnlyList<MatchEvent> Events,
  int Steps,
  bool ReachedTerminal);

public static class HeadlessSimulation
{
  public static HeadlessSimulationResult Run(
    MatchState initialState,
    ILegalActionRules legalActionRules,
    IReadOnlyDictionary<ParticipantId, IHeadlessBot> bots,
    HeadlessActionExecutor executeAction,
    int maxSteps,
    HeadlessTerminalPredicate? isTerminal = null)
  {
    ArgumentNullException.ThrowIfNull(initialState);
    ArgumentNullException.ThrowIfNull(legalActionRules);
    ArgumentNullException.ThrowIfNull(bots);
    ArgumentNullException.ThrowIfNull(executeAction);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSteps);

    var current = initialState;
    var events = new List<MatchEvent>();

    for (var step = 0; step < maxSteps; step++)
    {
      if (isTerminal?.Invoke(current) == true)
      {
        return Result(
          current,
          events,
          step,
          reachedTerminal: true);
      }

      var activeTurn = current.Turn
        ?? throw new InvalidOperationException(
          $"Match '{current.MatchId}' does not have an active turn for headless simulation.");

      if (!bots.TryGetValue(
            activeTurn.ParticipantId,
            out var bot))
      {
        throw new InvalidOperationException(
          $"No headless bot is registered for participant '{activeTurn.ParticipantId}'.");
      }

      var legalActions = MatchEngine.QueryLegalActions(
        current,
        activeTurn.ParticipantId,
        legalActionRules);

      if (legalActions.Actions.Count == 0)
      {
        throw new InvalidOperationException(
          $"Participant '{activeTurn.ParticipantId}' has no legal actions at revision {current.Revision}.");
      }

      var action = bot.Choose(
        current,
        legalActions);

      legalActions.EnsureAllowed(action);

      var transition = executeAction(
        current,
        activeTurn.ParticipantId,
        action,
        step)
        ?? throw new InvalidOperationException(
          "Headless action executor returned null.");

      if (transition.State.MatchId != current.MatchId)
      {
        throw new InvalidOperationException(
          $"Headless action changed match identity from '{current.MatchId}' to '{transition.State.MatchId}'.");
      }

      events.AddRange(transition.Events);
      current = transition.State;
    }

    return Result(
      current,
      events,
      maxSteps,
      reachedTerminal: isTerminal?.Invoke(current) == true);
  }

  private static HeadlessSimulationResult Result(
    MatchState finalState,
    IEnumerable<MatchEvent> events,
    int steps,
    bool reachedTerminal)
  {
    return new HeadlessSimulationResult(
      finalState,
      new ReadOnlyCollection<MatchEvent>(
        [.. events]),
      steps,
      reachedTerminal);
  }
}
