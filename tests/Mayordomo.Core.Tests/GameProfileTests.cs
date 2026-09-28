namespace Mayordomo.Core.Tests;

public sealed class GameProfileTests
{
  [Fact]
  public void Create_preserves_engine_ruleset_content_and_brand_identity()
  {
    var profile = GameProfile.Create(
      gameId: "mayordomo",
      edition: "5.0",
      rulesetId: "mayordomo-5.0",
      contentPackId: "mayordomo-5.0-bootstrap",
      brandPackId: "mayordomo");

    Assert.Equal("mayordomo", profile.GameId);
    Assert.Equal("5.0", profile.Edition);
    Assert.Equal("mayordomo-5.0", profile.RulesetId);
    Assert.Equal("mayordomo-5.0-bootstrap", profile.ContentPackId);
    Assert.Equal("mayordomo", profile.BrandPackId);
  }

  [Theory]
  [InlineData("", "5.0", "rules", "content", "brand")]
  [InlineData("game", "", "rules", "content", "brand")]
  [InlineData("game", "5.0", "", "content", "brand")]
  [InlineData("game", "5.0", "rules", "", "brand")]
  [InlineData("game", "5.0", "rules", "content", "")]
  public void Create_rejects_missing_versioned_identity(
    string gameId,
    string edition,
    string rulesetId,
    string contentPackId,
    string brandPackId)
  {
    Assert.Throws<ArgumentException>(() =>
      GameProfile.Create(gameId, edition, rulesetId, contentPackId, brandPackId));
  }
}
