using System.Reflection;
using Xunit;

namespace Mayordomo.Core.Tests;

public sealed class CanonicalStateHashV2Tests
{
  [Fact]
  public void State_hash_format_v2_includes_the_processed_command_ledger()
  {
    var initial = MatchState.Create(
      MatchId.Create("hash-ledger"),
      randomSeed: 17UL);

    var withLedger = CopyWithProcessedCommands(
      initial,
      [
        new KeyValuePair<CommandId, string>(
          CommandId.Create("command-001"),
          "FINGERPRINT-001")
      ]);

    Assert.Equal(2, MatchStateHasher.FormatVersion);
    Assert.NotEqual(
      MatchStateHasher.Compute(initial),
      MatchStateHasher.Compute(withLedger));
  }

  [Fact]
  public void Processed_command_dictionary_order_does_not_change_the_hash()
  {
    var initial = MatchState.Create(
      MatchId.Create("hash-ledger-order"),
      randomSeed: 23UL);

    var first = CopyWithProcessedCommands(
      initial,
      [
        new KeyValuePair<CommandId, string>(
          CommandId.Create("command-a"),
          "FINGERPRINT-A"),
        new KeyValuePair<CommandId, string>(
          CommandId.Create("command-b"),
          "FINGERPRINT-B")
      ]);

    var second = CopyWithProcessedCommands(
      initial,
      [
        new KeyValuePair<CommandId, string>(
          CommandId.Create("command-b"),
          "FINGERPRINT-B"),
        new KeyValuePair<CommandId, string>(
          CommandId.Create("command-a"),
          "FINGERPRINT-A")
      ]);

    Assert.Equal(
      MatchStateHasher.Compute(first),
      MatchStateHasher.Compute(second));
  }

  [Fact]
  public void Different_command_fingerprints_change_the_hash()
  {
    var initial = MatchState.Create(
      MatchId.Create("hash-ledger-fingerprint"),
      randomSeed: 29UL);
    var commandId = CommandId.Create("command-001");

    var first = CopyWithProcessedCommands(
      initial,
      [
        new KeyValuePair<CommandId, string>(
          commandId,
          "FINGERPRINT-A")
      ]);

    var second = CopyWithProcessedCommands(
      initial,
      [
        new KeyValuePair<CommandId, string>(
          commandId,
          "FINGERPRINT-B")
      ]);

    Assert.NotEqual(
      MatchStateHasher.Compute(first),
      MatchStateHasher.Compute(second));
  }

  private static MatchState CopyWithProcessedCommands(
    MatchState source,
    IEnumerable<KeyValuePair<CommandId, string>> processedCommands)
  {
    var constructor = typeof(MatchState)
      .GetConstructors(
        BindingFlags.Instance |
        BindingFlags.NonPublic)
      .Single();

    return (MatchState)constructor.Invoke(
      [
        source.MatchId,
        source.Revision,
        source.Participants,
        source.Positions,
        source.HeldItems,
        source.Decks,
        source.Turn,
        source.RandomState,
        processedCommands
      ]);
  }
}
