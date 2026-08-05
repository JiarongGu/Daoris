using Daoris.Devkit;

namespace Daoris.Devkit.Tests;

public sealed class VersionAndDocsGateTests : IDisposable
{
    private readonly Fixture _fx = new("version");

    public void Dispose() => _fx.Dispose();

    private GateResult RunVersion(VersionOptions version, DocsOptions? docs = null) =>
        new VersionGate().Run(new GateContext(
            _fx.Path, new GateDeclaration { Version = version, Docs = docs ?? new DocsOptions() }));

    [Fact]
    public void No_declared_source_skips_rather_than_passing()
    {
        var result = RunVersion(new VersionOptions());

        Assert.True(result.Passed);
        Assert.True(result.Skipped);
    }

    [Fact]
    public void A_props_file_and_a_package_json_are_read_without_declaring_a_pattern()
    {
        _fx.Write("Directory.Build.props", "<Project><PropertyGroup><VersionPrefix>1.4.0</VersionPrefix></PropertyGroup></Project>");
        _fx.Write("web/package.json", """{ "name": "x", "version": "1.4.0" }""");

        var result = RunVersion(new VersionOptions("Directory.Build.props", Mirrors: ["web/package.json"]));

        Assert.True(result.Passed, result.Detail);
        Assert.Contains("1.4.0", result.Detail);
    }

    [Fact]
    public void A_mirror_that_disagrees_names_both_versions()
    {
        _fx.Write("Directory.Build.props", "<Project><VersionPrefix>1.4.0</VersionPrefix></Project>");
        _fx.Write("web/package.json", """{ "version": "1.3.9" }""");

        var result = RunVersion(new VersionOptions("Directory.Build.props", Mirrors: ["web/package.json"]));

        Assert.False(result.Passed);
        Assert.Contains("1.3.9", result.Detail);
        Assert.Contains("1.4.0", result.Detail);
    }

    /// <summary>
    /// The failure the gate actually exists for. A hand-bumped version leaves every file consistent —
    /// consistency was never the property at risk, authorship was — and the tell is that the release
    /// tooling never stamped a changelog heading for it.
    /// </summary>
    [Fact]
    public void A_version_with_no_changelog_entry_is_reported_as_hand_written()
    {
        _fx.Write("Directory.Build.props", "<Project><VersionPrefix>2.0.0</VersionPrefix></Project>");
        _fx.Write("CHANGELOG.md", "# Changelog\n\n## 1.9.0\n\n- something\n");

        var result = RunVersion(
            new VersionOptions("Directory.Build.props"), new DocsOptions(Changelog: "CHANGELOG.md"));

        Assert.False(result.Passed);
        Assert.Contains("written by hand", result.Detail);
    }

    /// <summary>0.x is development. A version that is not yet a claim cannot be a false one.</summary>
    [Fact]
    public void A_pre_release_version_is_not_held_to_the_changelog()
    {
        _fx.Write("Directory.Build.props", "<Project><VersionPrefix>0.0.3</VersionPrefix></Project>");
        _fx.Write("CHANGELOG.md", "# Changelog\n\nnothing released yet\n");

        var result = RunVersion(
            new VersionOptions("Directory.Build.props"), new DocsOptions(Changelog: "CHANGELOG.md"));

        Assert.True(result.Passed, result.Detail);
    }

    private sealed class FakeHistory(Dictionary<string, DateTimeOffset?> dates) : IGitHistory
    {
        public DateTimeOffset? LastCommitDate(string relativePath) =>
            dates.TryGetValue(relativePath, out var when) ? when : null;
    }

    private GateResult RunDocs(IGitHistory history, params TrackedDocument[] tracked) =>
        new DocsGate(history).Run(new GateContext(
            _fx.Path, new GateDeclaration { Docs = new DocsOptions(Tracked: tracked) }));

    [Fact]
    public void A_document_older_than_what_it_describes_fails_and_names_both_dates()
    {
        _fx.Write("docs/api.md", "the api");
        _fx.Write("src/Api.cs", "class Api;");

        var result = RunDocs(
            new FakeHistory(new()
            {
                ["docs/api.md"] = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                ["src/Api.cs"] = DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            }),
            new TrackedDocument("docs/api.md", ["src/Api.cs"]));

        Assert.False(result.Passed);
        Assert.Contains("2026-06-01", result.Detail);
    }

    [Fact]
    public void A_document_newer_than_what_it_describes_passes()
    {
        _fx.Write("docs/api.md", "the api");
        _fx.Write("src/Api.cs", "class Api;");

        var result = RunDocs(
            new FakeHistory(new()
            {
                ["docs/api.md"] = DateTimeOffset.Parse("2026-06-02T00:00:00Z"),
                ["src/Api.cs"] = DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            }),
            new TrackedDocument("docs/api.md", ["src/Api.cs"]));

        Assert.True(result.Passed, result.Detail);
    }

    /// <summary>
    /// Found by running the gate against this repository on its first real day. Both the README and the
    /// code it describes had been committed that morning, in separate commits minutes apart, and the
    /// gate failed the build — technically correct and completely useless, since that is what every
    /// working session looks like. A gate that fires on normal work is a gate people route around.
    /// </summary>
    [Fact]
    public void Code_committed_later_the_SAME_DAY_is_not_stale()
    {
        _fx.Write("README.md", "the readme");
        _fx.Write("src/Api.cs", "class Api;");

        var result = RunDocs(
            new FakeHistory(new()
            {
                ["README.md"] = DateTimeOffset.Parse("2026-08-05T09:14:00Z"),
                ["src/Api.cs"] = DateTimeOffset.Parse("2026-08-05T16:41:00Z"),
            }),
            new TrackedDocument("README.md", ["src/Api.cs"]));

        Assert.True(result.Passed, result.Detail);
    }

    /// <summary>A newly declared document has no history yet, and the first run must not fail on it.</summary>
    [Fact]
    public void A_document_that_has_never_been_committed_is_not_stale()
    {
        _fx.Write("docs/new.md", "brand new");
        _fx.Write("src/Api.cs", "class Api;");

        var result = RunDocs(
            new FakeHistory(new() { ["src/Api.cs"] = DateTimeOffset.Parse("2026-06-01T00:00:00Z") }),
            new TrackedDocument("docs/new.md", ["src/Api.cs"]));

        Assert.True(result.Passed, result.Detail);
    }

    [Fact]
    public void Nothing_declared_skips_rather_than_passing()
    {
        var result = new DocsGate(new FakeHistory([])).Run(new GateContext(_fx.Path, new GateDeclaration()));

        Assert.True(result.Skipped);
    }
}
