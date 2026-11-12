using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A measurement or offers reading from a version other than the one pinned is listed as such, so a moved pin shows what no longer applies.</summary>
public sealed class VersionCurrencyTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "version-currency", Guid.NewGuid().ToString("N")[..8]);

    public VersionCurrencyTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    [Fact]
    public void A_measurement_and_an_offers_reading_at_another_version_than_the_pin_are_listed()
    {
        var registry = ProviderRegistry.Built();
        Assert.Null(AgentOffers.Offered(_home, "claude-code-acp", "0.84.0", "acct", ["default"], ["low"], DateTimeOffset.UtcNow));

        var lines = VersionCurrency.Of(_home, registry.Door("claude-code-acp"), pinned: "0.106.0");

        Assert.Contains(lines, line => line.Contains("steering", StringComparison.OrdinalIgnoreCase) && line.Contains("measured at adapter version 0.84.0", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("offers", StringComparison.Ordinal) && line.Contains("read at 0.84.0", StringComparison.Ordinal));
    }

    [Fact]
    public void Nothing_is_listed_where_the_pin_is_the_version_it_was_taken_at_or_nothing_is_pinned()
    {
        var registry = ProviderRegistry.Built();
        Assert.Null(AgentOffers.Offered(_home, "claude-code-acp", "0.106.0", "acct", ["default"], ["low"], DateTimeOffset.UtcNow));

        var lines = VersionCurrency.Of(_home, registry.Door("claude-code-acp"), pinned: "0.106.0");

        Assert.DoesNotContain(lines, line => line.StartsWith("offers", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("measured at", StringComparison.Ordinal));
        Assert.Empty(VersionCurrency.Of(_home, registry.Door("claude-code-acp"), pinned: null));
    }
}
