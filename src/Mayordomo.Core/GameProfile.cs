namespace Mayordomo.Core;

public sealed record GameProfile
{
  private GameProfile(
    string gameId,
    string edition,
    string rulesetId,
    string contentPackId,
    string brandPackId)
  {
    GameId = gameId;
    Edition = edition;
    RulesetId = rulesetId;
    ContentPackId = contentPackId;
    BrandPackId = brandPackId;
  }

  public string GameId { get; }

  public string Edition { get; }

  public string RulesetId { get; }

  public string ContentPackId { get; }

  public string BrandPackId { get; }

  public static GameProfile Create(
    string gameId,
    string edition,
    string rulesetId,
    string contentPackId,
    string brandPackId)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
    ArgumentException.ThrowIfNullOrWhiteSpace(edition);
    ArgumentException.ThrowIfNullOrWhiteSpace(rulesetId);
    ArgumentException.ThrowIfNullOrWhiteSpace(contentPackId);
    ArgumentException.ThrowIfNullOrWhiteSpace(brandPackId);

    return new GameProfile(gameId, edition, rulesetId, contentPackId, brandPackId);
  }
}
