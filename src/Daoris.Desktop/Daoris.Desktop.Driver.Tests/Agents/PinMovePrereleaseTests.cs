using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A pin's update orders two prereleases by semver's precedence, never as strings: a number counts as a number.</summary>
public sealed class PinMovePrereleaseTests
{
    [Theory]
    [InlineData("0.2.0-alpha.10", "0.2.0-alpha.9", "Move")]
    [InlineData("0.2.0-alpha.2", "0.2.0-alpha.10", "Behind")]
    [InlineData("0.2.0-rc.1", "0.2.0-alpha.20", "Move")]
    [InlineData("0.2.0-alpha", "0.2.0-alpha.1", "Behind")]
    [InlineData("0.2.0-alpha.1", "0.2.0-alpha", "Move")]
    [InlineData("0.2.0-1", "0.2.0-beta", "Behind")]
    [InlineData("0.2.0", "0.2.0-rc.10", "Move")]
    [InlineData("0.2.0-rc.10", "0.2.0", "Behind")]
    public void A_prerelease_is_ordered_by_its_identifiers(string newest, string pin, string expected) =>
        Assert.Equal(Enum.Parse<PinMoveKind>(expected), PinMove.Decide(newest, pin, installed: true));
}
