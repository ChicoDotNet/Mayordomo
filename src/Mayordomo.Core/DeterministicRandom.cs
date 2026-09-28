namespace Mayordomo.Core;

/// <summary>
/// Small deterministic pseudo-random source with stable, engine-owned semantics.
/// </summary>
/// <remarks>
/// Uses SplitMix64 so replay behavior does not depend on System.Random implementation details.
/// This is a game-simulation primitive, not a cryptographic random source.
/// </remarks>
public sealed class DeterministicRandom
{
  private const ulong Increment = 0x9E3779B97F4A7C15UL;
  private const ulong Mix1 = 0xBF58476D1CE4E5B9UL;
  private const ulong Mix2 = 0x94D049BB133111EBUL;

  private ulong _state;

  private DeterministicRandom(ulong seed)
  {
    _state = seed;
  }

  public static DeterministicRandom Create(ulong seed)
  {
    return new DeterministicRandom(seed);
  }

  public ulong NextUInt64()
  {
    _state = unchecked(_state + Increment);

    var value = _state;
    value = unchecked((value ^ (value >> 30)) * Mix1);
    value = unchecked((value ^ (value >> 27)) * Mix2);

    return value ^ (value >> 31);
  }

  public int NextInt32(
    int minInclusive,
    int maxExclusive)
  {
    if (minInclusive >= maxExclusive)
    {
      throw new ArgumentOutOfRangeException(
        nameof(maxExclusive),
        "Maximum must be greater than minimum.");
    }

    var range = (ulong)((long)maxExclusive - minInclusive);
    var threshold = unchecked(0UL - range) % range;

    ulong sample;

    do
    {
      sample = NextUInt64();
    }
    while (sample < threshold);

    var offset = (long)(sample % range);

    return checked((int)(minInclusive + offset));
  }
}
