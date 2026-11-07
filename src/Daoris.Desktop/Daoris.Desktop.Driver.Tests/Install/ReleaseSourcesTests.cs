using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The release channels are Install's own <see cref="IReleaseSource"/>s, found by channel, so no switch on ids
/// decides which maker's fetch runs, and each keeps its own refusals.
/// </summary>
public sealed class ReleaseSourcesTests
{
    [Fact]
    public void The_channels_are_the_sources_in_their_order()
    {
        Assert.Equal([ClaudeReleases.Channel, CodexReleases.Channel], ReleaseChannels.Known);
        Assert.Equal(ReleaseChannels.Known, ReleaseChannels.Sources.Select(source => source.Channel));
        Assert.IsType<ClaudeReleaseSource>(ReleaseChannels.Of(ClaudeReleases.Channel));
        Assert.IsType<CodexReleaseSource>(ReleaseChannels.Of(CodexReleases.Channel));
    }

    [Fact]
    public void A_channel_this_build_does_not_know_is_refused_before_anything_is_fetched()
    {
        var refused = Assert.Throws<DriverException>(() => ReleaseChannels.Of("elsewhere-releases"));
        Assert.Contains("no `elsewhere-releases` channel", refused.Message);
        Assert.Throws<DriverException>(() => ReleaseChannels.RefuseVersion("elsewhere-releases", "1.0.0"));
    }

    [Fact]
    public void Each_source_refuses_a_version_as_its_channel_did()
    {
        // Claude Code's floor (ReleaseChannelTests) refuses what the channel cannot verify; the source answers the same.
        Assert.Equal(
            Assert.Throws<DriverException>(() => ClaudeReleases.RefuseVersion("2.1.87")).Message,
            Assert.Throws<DriverException>(() => ReleaseChannels.RefuseVersion(ClaudeReleases.Channel, "2.1.87")).Message);
        ReleaseChannels.RefuseVersion(ClaudeReleases.Channel, "2.1.89");
        Assert.Equal(
            Assert.Throws<DriverException>(() => CodexReleases.RefuseVersion("latest")).Message,
            Assert.Throws<DriverException>(() => ReleaseChannels.RefuseVersion(CodexReleases.Channel, "latest")).Message);
    }
}
