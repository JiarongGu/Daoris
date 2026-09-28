namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The person's Edge as Daoris's browser (BRW12): where it is looked for, what it is started with,
/// and how an Edge Daoris started before is found again rather than started twice.
/// </summary>
public sealed class EdgeBrowserTests : Bridge
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "daoris-edge");

    private static Func<string, string?> Environment(params (string Name, string Value)[] set) =>
        name => set.FirstOrDefault(pair => pair.Name == name).Value;

    [Fact]
    public void Its_profile_is_the_homes_and_never_the_persons_default()
    {
        var home = Path.Combine(Root, "data");

        Assert.Equal(Path.Combine(home, "browser", "edge"), EdgeBrowser.ProfileFolder(home));
        Assert.Contains($"--user-data-dir={EdgeBrowser.ProfileFolder(home)}", EdgeBrowser.Arguments(EdgeBrowser.ProfileFolder(home), 9422));
    }

    /// <summary>The machine's install first, then one person's; a folder the environment does not name is not looked in.</summary>
    [Fact]
    public void It_is_looked_for_where_Edge_installs_for_the_machine_then_for_one_person()
    {
        var x86 = Path.Combine(Root, "x86");
        var mine = Path.Combine(Root, "me");
        var candidates = EdgeBrowser.Candidates(Environment(("ProgramFiles(x86)", x86), ("LOCALAPPDATA", mine)));

        Assert.Equal(
        [
            Path.Combine(x86, "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(mine, "Microsoft", "Edge", "Application", "msedge.exe"),
        ], candidates);
        Assert.Equal(candidates[1], EdgeBrowser.Locate(Environment(("ProgramFiles(x86)", x86), ("LOCALAPPDATA", mine)), path => path == candidates[1]));
        Assert.Null(EdgeBrowser.Locate(Environment(), _ => true));
    }

    /// <summary>A debug port on its own profile, none of Edge's first-run pages, and sync left to the person.</summary>
    [Fact]
    public void It_is_started_with_its_port_and_without_its_first_run_and_sync_is_left_alone()
    {
        var arguments = EdgeBrowser.Arguments(Path.Combine(Root, "edge"), 9422);

        Assert.Contains("--remote-debugging-port=9422", arguments);
        Assert.Contains("--no-first-run", arguments);
        Assert.DoesNotContain(arguments, argument => argument.Contains("sync", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("{\"port\":9422}", 9422)]
    [InlineData("{\"port\":80}", null)]
    [InlineData("{\"port\":\"9422\"}", null)]
    [InlineData("not json", null)]
    [InlineData(null, null)]
    public void The_recorded_port_is_read_back_or_is_none(string? json, int? port) =>
        Assert.Equal(port, EdgeBrowser.RecordedPort(json));

    [Fact]
    public void What_is_recorded_reads_back() =>
        Assert.Equal(9422, EdgeBrowser.RecordedPort(EdgeBrowser.Record(9422)));

    /// <summary>A port that answers is adopted only when it is an Edge: another program may hold it by now.</summary>
    [Theory]
    [InlineData("{\"Browser\":\"Edg/154.0.4258.37\"}", true)]
    [InlineData("{\"Browser\":\"Chrome/152.0.7977.140\"}", false)]
    [InlineData("{}", false)]
    [InlineData("not json", false)]
    public void An_endpoint_is_an_Edge_when_it_says_so(string version, bool edge) =>
        Assert.Equal(edge, EdgeBrowser.IsEdge(version));
}
