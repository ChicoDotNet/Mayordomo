using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Mayordomo.Core;

public readonly record struct StateHash
{
  internal StateHash(string value)
  {
    Value = value;
  }

  public string Value { get; }

  public override string ToString() => Value;
}

public static class MatchStateHasher
{
  private static readonly byte[] StateFormatIdentifier =
    "Mayordomo.Core.MatchState"u8.ToArray();

  public const string Algorithm = "SHA-256";

  public const int FormatVersion = 2;

  public static StateHash Compute(MatchState state)
  {
    ArgumentNullException.ThrowIfNull(state);

    using var hash = IncrementalHash.CreateHash(
      HashAlgorithmName.SHA256);
    var writer = new CanonicalHashWriter(hash);

    writer.AppendRaw(StateFormatIdentifier);
    writer.AppendInt32(FormatVersion);
    writer.AppendString(state.MatchId.Value);
    writer.AppendInt64(state.Revision.Value);

    writer.AppendInt32(state.Participants.Count);

    foreach (var participant in state.Participants)
    {
      writer.AppendString(participant.Value);
    }

    writer.AppendInt32(state.Positions.Count);

    foreach (var position in state.Positions
               .OrderBy(
                 entry => entry.Key.Value,
                 StringComparer.Ordinal))
    {
      writer.AppendString(position.Key.Value);
      writer.AppendString(position.Value.Value);
    }

    writer.AppendInt32(state.HeldItems.Count);

    foreach (var inventory in state.HeldItems
               .OrderBy(
                 entry => entry.Key.Value,
                 StringComparer.Ordinal))
    {
      writer.AppendString(inventory.Key.Value);
      writer.AppendInt32(inventory.Value.Count);

      foreach (var item in inventory.Value)
      {
        writer.AppendString(item.Value);
      }
    }

    writer.AppendInt32(state.Decks.Count);

    foreach (var deck in state.Decks
               .OrderBy(
                 entry => entry.Key.Value,
                 StringComparer.Ordinal))
    {
      writer.AppendString(deck.Key.Value);
      writer.AppendInt32(deck.Value.RemainingItems.Count);

      foreach (var item in deck.Value.RemainingItems)
      {
        writer.AppendString(item.Value);
      }
    }

    if (state.Turn is null)
    {
      writer.AppendByte(0);
    }
    else
    {
      writer.AppendByte(1);
      writer.AppendString(state.Turn.ParticipantId.Value);
      writer.AppendString(state.Turn.PhaseId.Value);
    }

    writer.AppendUInt64(state.RandomState.Value);

    writer.AppendInt32(state.ProcessedCommands.Count);

    foreach (var processedCommand in state.ProcessedCommands
               .OrderBy(
                 entry => entry.Key.Value,
                 StringComparer.Ordinal))
    {
      writer.AppendString(processedCommand.Key.Value);
      writer.AppendString(processedCommand.Value);
    }

    return new StateHash(
      Convert.ToHexString(hash.GetHashAndReset()));
  }

  private sealed class CanonicalHashWriter
  {
    private readonly IncrementalHash _hash;

    public CanonicalHashWriter(IncrementalHash hash)
    {
      _hash = hash;
    }

    public void AppendRaw(ReadOnlySpan<byte> value)
    {
      _hash.AppendData(value);
    }

    public void AppendByte(byte value)
    {
      Span<byte> buffer = stackalloc byte[1];
      buffer[0] = value;
      _hash.AppendData(buffer);
    }

    public void AppendInt32(int value)
    {
      Span<byte> buffer = stackalloc byte[sizeof(int)];
      BinaryPrimitives.WriteInt32BigEndian(buffer, value);
      _hash.AppendData(buffer);
    }

    public void AppendInt64(long value)
    {
      Span<byte> buffer = stackalloc byte[sizeof(long)];
      BinaryPrimitives.WriteInt64BigEndian(buffer, value);
      _hash.AppendData(buffer);
    }

    public void AppendUInt64(ulong value)
    {
      Span<byte> buffer = stackalloc byte[sizeof(ulong)];
      BinaryPrimitives.WriteUInt64BigEndian(buffer, value);
      _hash.AppendData(buffer);
    }

    public void AppendString(string value)
    {
      var bytes = Encoding.UTF8.GetBytes(value);

      AppendInt32(bytes.Length);
      _hash.AppendData(bytes);
    }
  }
}
