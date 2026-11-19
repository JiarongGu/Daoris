using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>What Daoris's own release channel says: a version is one exact release, a release is newer than the build running, and an asset is an HTTPS address with its hash.</summary>
public sealed class DaorisReleasesTests
{
    [Theory]
    [InlineData("0.1.0")]
    [InlineData("12.0.34")]
    public void An_exact_version_is_accepted(string version)
    {
        DaorisReleases.RefuseVersion(version);
        Assert.True(DaorisReleases.IsVersion(version));
    }

    [Theory]
    [InlineData("v0.1.0")]
    [InlineData("latest")]
    [InlineData("0.1")]
    [InlineData("0.1.0-beta.1")]
    [InlineData("")]
    public void A_tag_a_pointer_or_a_prerelease_is_no_version(string version)
    {
        var refused = Assert.Throws<DriverException>(() => DaorisReleases.RefuseVersion(version));
        Assert.Contains("not a Daoris version", refused.Message);
        Assert.False(DaorisReleases.IsVersion(version));
    }

    [Theory]
    [InlineData("0.1.1", "0.1.0", true)]
    [InlineData("0.2.0", "0.1.9+abc1234", true)]
    [InlineData("1.0.0", "0.9.9+dev", true)]
    [InlineData("0.1.0", "0.1.0+abc1234", false)]
    [InlineData("0.1.0", "0.2.0", false)]
    [InlineData("0.10.0", "0.9.0", true)]
    [InlineData("0.1.0", null, true)]
    [InlineData("0.1.0", "dev", true)]
    public void A_release_is_newer_than_the_build_running_by_its_number_alone(string release, string? running, bool newer) =>
        Assert.Equal(newer, DaorisReleases.IsNewer(release, running));

    [Fact]
    public void The_package_and_its_checksum_are_named_by_version_and_target()
    {
        Assert.Equal("Daoris-0.1.0-win-x64.zip", DaorisReleases.Package("0.1.0"));
        Assert.Equal("Daoris-0.1.0-win-x64.zip.sha256", DaorisReleases.Sums("0.1.0"));
    }

    [Fact]
    public void The_newest_release_is_the_highest_published_one_carrying_the_package_and_its_checksum()
    {
        static string Release(string version, bool draft = false, bool prerelease = false, bool complete = true)
        {
            var zip = $"{{ \"name\": \"Daoris-{version}-win-x64.zip\" }}";
            var sums = $"{{ \"name\": \"Daoris-{version}-win-x64.zip.sha256\" }}";
            return $"{{ \"tag_name\": \"v{version}\", \"draft\": {(draft ? "true" : "false")}, \"prerelease\": {(prerelease ? "true" : "false")}, "
                + $"\"assets\": [ {(complete ? zip + ", " + sums : zip)} ] }}";
        }

        var list = (params string[] releases) => Encoding.UTF8.GetBytes("[" + string.Join(",", releases) + "]");

        Assert.Equal("0.3.0", DaorisReleases.Newest(list(Release("0.2.0"), Release("0.10.0", draft: true), Release("0.3.0"), Release("0.9.0", prerelease: true), Release("0.4.0", complete: false))));
        Assert.Null(DaorisReleases.Newest(list(Release("0.4.0", complete: false))));
        Assert.Null(DaorisReleases.Newest(list("""{ "tag_name": "nightly", "assets": [] }""")));
        Assert.Throws<DriverException>(() => DaorisReleases.Newest(Encoding.UTF8.GetBytes("not json")));
        Assert.Throws<DriverException>(() => DaorisReleases.Newest(Encoding.UTF8.GetBytes("{}")));
    }

    [Fact]
    public void Metadata_for_another_release_is_refused_as_that_release()
    {
        var metadata = JsonDocument.Parse("""{ "tag_name": "v0.2.0" }""").RootElement;
        var refused = Assert.Throws<DriverException>(() => DaorisReleases.RefuseTag(metadata, "0.1.0"));
        Assert.Contains("metadata for another release", refused.Message);
        DaorisReleases.RefuseTag(metadata, "0.2.0");
    }

    [Fact]
    public void An_asset_is_an_https_address_and_the_hash_the_api_states_for_it_where_it_states_one()
    {
        var hash = new string('a', 64);
        var metadata = JsonDocument.Parse($$"""
            { "assets": [
              { "name": "a.zip", "browser_download_url": "https://example.test/a.zip", "digest": "sha256:{{hash}}" },
              { "name": "b.zip", "browser_download_url": "https://example.test/b.zip" },
              { "name": "c.zip", "browser_download_url": "http://example.test/c.zip" } ] }
            """).RootElement;

        Assert.Equal(("https://example.test/a.zip", hash), DaorisReleases.Asset(metadata, "a.zip", "0.1.0"));
        Assert.Equal(("https://example.test/b.zip", (string?)null), DaorisReleases.Asset(metadata, "b.zip", "0.1.0"));
        Assert.Contains("not HTTPS", Assert.Throws<DriverException>(() => DaorisReleases.Asset(metadata, "c.zip", "0.1.0")).Message);
        Assert.Contains("publishes no d.zip", Assert.Throws<DriverException>(() => DaorisReleases.Asset(metadata, "d.zip", "0.1.0")).Message);
    }

    [Fact]
    public void A_checksum_file_lists_the_hash_of_the_package_it_names_and_no_other()
    {
        var hash = new string('b', 64);
        var sums = Encoding.UTF8.GetBytes($"{hash}  Daoris-0.1.0-win-x64.zip\n{new string('c', 64)} *other.zip\n");

        Assert.Equal(hash, DaorisReleases.Listed(sums, "Daoris-0.1.0-win-x64.zip"));
        Assert.Null(DaorisReleases.Listed(sums, "Daoris-0.2.0-win-x64.zip"));
    }
}
