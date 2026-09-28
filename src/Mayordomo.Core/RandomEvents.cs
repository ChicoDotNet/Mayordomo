namespace Mayordomo.Core;

public readonly record struct RandomState(ulong Value);

public sealed record GenerateRandomIntCommand
{
  private GenerateRandomIntCommand(
    CommandId commandId,
    MatchId matchId,
    Revision expectedRevision,
    int minInclusive,
    int maxExclusive)
  {
    CommandId = commandId;
    MatchId = matchId;
    ExpectedRevision = expectedRevision;
    MinInclusive = minInclusive;
    MaxExclusive = maxExclusive;
  }

  public CommandId CommandId { get; }

  public MatchId MatchId { get; }

  public Revision ExpectedRevision { get; }

  public int MinInclusive { get; }

  public int MaxExclusive { get; }

  public static GenerateRandomIntCommand Create(
    CommandId commandId,
    MatchId matchId,
    Revision expectedRevision,
    int minInclusive,
    int maxExclusive)
  {
    if (minInclusive >= maxExclusive)
    {
      throw new ArgumentOutOfRangeException(
        nameof(maxExclusive),
        "Maximum must be greater than minimum.");
    }

    return new GenerateRandomIntCommand(
      commandId,
      matchId,
      expectedRevision,
      minInclusive,
      maxExclusive);
  }
}

public sealed record RandomIntGeneratedEvent(
  CommandId CommandId,
  MatchId MatchId,
  Revision Revision,
  int MinInclusive,
  int MaxExclusive,
  int Value,
  RandomState RandomStateAfter)
  : MatchEvent(CommandId, MatchId, Revision);
