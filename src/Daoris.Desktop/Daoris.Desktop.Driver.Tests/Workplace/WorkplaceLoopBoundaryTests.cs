using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

public sealed class WorkplaceLoopBoundaryTests : IDisposable
{
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "workplace-loop", Guid.NewGuid().ToString("N"));
    public void Dispose() => GitSeed.Remove(_home);

    [Fact]
    public void Wiring_world_answers_the_data_facet_on_the_same_object_with_original_defaults_and_references()
    {
        Func<string, string?> process = _ => null, account = _ => null;
        var origins = new List<ToolOrigin>();
        var world = new WiringWorld { BaseDirectory = _home, Process = process, Account = account, ToolOrigins = origins };
        IToolHomeWorld facts = world;
        Assert.Same(world, facts);
        Assert.Same(process, facts.Process);
        Assert.Same(account, facts.Account);
        Assert.Same(origins, facts.ToolOrigins);
        Assert.Null(facts.Home);
        Assert.Null(facts.ToolHome);
        Assert.Null(facts.ToolHomeWhy);
        Assert.Equal("named", facts.ToolHomeFrom);
        origins.Add(new ToolOrigin(_home, "tree", null));
        Assert.Single(facts.ToolOrigins!);
        Assert.Same(origins, ((IToolHomeWorld)(world with { ToolHome = _home })).ToolOrigins);
    }

    [Fact]
    public void Independent_world_keeps_own_home_getter_and_environment_evaluation_order()
    {
        var calls = new List<string>();
        Func<string, string?> process = name => { calls.Add("process:" + name); return _home; };
        Func<string, string?> account = _ => throw new InvalidOperationException("process wins");
        var world = new World(name =>
        {
            calls.Add(name);
            return name switch { "Process" => process, "Account" => account, "Home" => null, _ => throw new InvalidOperationException(name) };
        });
        Assert.Equal((_home, "process", (string?)null), ToolHomeLeftovers.OwnHome(world));
        Assert.Equal(["Process", "Account", "Home", "process:" + ToolHomeLeftovers.Variable], calls);
        var refused = new IOException("unread environment");
        Func<string, string?> unread = _ => throw refused;
        var broken = new World(name => name switch { "Process" => unread, "Account" or "Home" => null, _ => throw new InvalidOperationException(name) });
        Assert.Same(refused, Assert.Throws<IOException>(() => ToolHomeLeftovers.OwnHome(broken)));
    }

    [Fact]
    public void Data_reads_preserve_getter_order_and_do_not_refresh_environment_or_records()
    {
        var calls = new List<string>();
        var world = new World(name =>
        {
            calls.Add(name);
            return name switch { "ToolHome" => _home, "ToolHomeFrom" => "account", "ToolHomeWhy" => "handed", _ => throw new InvalidOperationException(name) };
        });
        Assert.Equal((_home, "account", "handed"), ToolHomeLeftovers.ToolHomeOf(world));
        Assert.Equal(["ToolHome", "ToolHomeFrom", "ToolHomeWhy"], calls);
        calls.Clear();
        Assert.Equal(Path.Combine(_home, "projects"), ToolHomeLeftovers.ProjectsOf(world));
        Assert.Equal(["ToolHome"], calls);
    }

    [Theory]
    [InlineData("no-home")]
    [InlineData("no-tool-home")]
    [InlineData("missing-projects")]
    [InlineData("records-unread")]
    public void List_short_circuits_before_unneeded_facts_in_its_original_order(string condition)
    {
        var calls = new List<string>();
        if (condition == "records-unread") Directory.CreateDirectory(Path.Combine(_home, "projects"));
        var world = new World(name =>
        {
            calls.Add(name);
            return name switch
            {
                "Home" => condition == "no-home" ? null : _home,
                "ToolHome" when condition != "no-home" => condition == "no-tool-home" ? null : _home,
                "ToolHomeWhy" when condition == "no-tool-home" => "no-tool-home",
                "ToolOrigins" when condition == "records-unread" => null,
                _ => throw new InvalidOperationException("unexpected getter: " + name),
            };
        });
        var listing = ToolHomeLeftovers.List(world);
        Assert.Empty(listing.Folders);
        Assert.Equal(condition == "missing-projects" ? null : condition, listing.Why);
        string[] expected = condition switch
        {
            "no-home" => ["Home"], "no-tool-home" => ["Home", "ToolHome", "ToolHomeWhy"],
            "records-unread" => ["Home", "ToolHome", "ToolOrigins"], _ => ["Home", "ToolHome"],
        };
        Assert.Equal(expected, calls);
    }

    [Theory]
    [InlineData("root")]
    [InlineData("data")]
    [InlineData("case")]
    [InlineData("nested")]
    [InlineData("bare")]
    [InlineData("relative")]
    [InlineData("marked-data")]
    [InlineData("trailing")]
    [InlineData("null")]
    public void Retained_public_path_APIs_keep_markers_priority_immediate_data_and_original_fallback_spelling(string example)
    {
        Directory.CreateDirectory(Path.Combine(_home, "data"));
        File.WriteAllText(Path.Combine(_home, DaorisHome.InstallMarker), "marked");
        string? input = example switch
        {
            "root" => _home, "data" or "marked-data" => Path.Combine(_home, "data"),
            "case" => Path.Combine(_home, "DATA"), "nested" => Path.Combine(_home, "data", "nested"),
            "bare" => Path.Combine(_home, "elsewhere") + Path.DirectorySeparatorChar,
            "relative" => Path.Combine("_fixtures", "workplace-loop-unmarked-" + Path.GetFileName(_home)),
            "trailing" => _home + Path.DirectorySeparatorChar, _ => null,
        };
        if (example == "marked-data") File.WriteAllText(Path.Combine(input!, DaorisHome.InstallMarker), "marked first");
        var install = example switch { "root" or "data" or "case" or "trailing" => _home, "marked-data" => input, _ => null };
        Assert.Equal(install, SystemWiring.InstallOf(input));
        Assert.Equal(install, InstallRoot.InstallOf(input));
        Assert.Equal(install ?? input, SystemWiring.RootOf(input));
        Assert.Equal(install ?? input, InstallRoot.RootOf(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad\0path")]
    public void Invalid_path_exceptions_are_not_swallowed_by_the_extraction(string input)
    {
        Assert.ThrowsAny<ArgumentException>(() => SystemWiring.InstallOf(input));
        Assert.ThrowsAny<ArgumentException>(() => SystemWiring.RootOf(input));
        Assert.ThrowsAny<ArgumentException>(() => InstallRoot.InstallOf(input));
        Assert.ThrowsAny<ArgumentException>(() => InstallRoot.RootOf(input));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_tool_home_equal_to_or_inside_the_install_remains_refused(bool descendant)
    {
        var home = Path.Combine(_home, "data");
        Directory.CreateDirectory(home);
        File.WriteAllText(Path.Combine(_home, DaorisHome.InstallMarker), "marked");
        var tool = descendant ? Path.Combine(_home, "profiles", "work") : _home;
        Assert.Equal(((string?)null, "process", "inside-daoris"), ToolHomeLeftovers.OwnHome(_ => tool, null, _home, home));
    }

    private sealed class World(Func<string, object?> read) : IToolHomeWorld
    {
        public string? Home => (string?)read(nameof(Home));
        public Func<string, string?> Process => (Func<string, string?>)read(nameof(Process))!;
        public Func<string, string?>? Account => (Func<string, string?>?)read(nameof(Account));
        public string? ToolHome => (string?)read(nameof(ToolHome));
        public string ToolHomeFrom => (string)read(nameof(ToolHomeFrom))!;
        public string? ToolHomeWhy => (string?)read(nameof(ToolHomeWhy));
        public IReadOnlyList<ToolOrigin>? ToolOrigins => (IReadOnlyList<ToolOrigin>?)read(nameof(ToolOrigins));
    }

    [Fact]
    public void Fixed_world_keeps_the_terminal_JSON_text_and_read_only_tool_listing()
    {
        // A stable scratch path lets the pre-cut and post-cut runs compare exact output bytes.
        var scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "workplace-loop-parity");
        try
        {
            var install = Path.Combine(scratch, "install");
            var home = Path.Combine(install, "data");
            var tool = Path.Combine(scratch, "person");
            var tree = DaorisHome.Entry(home, HomeEntry.Trees, "default", "engine", "s1");
            var project = Path.Combine(tool, "projects", ToolHomeLeftovers.Flatten(tree));
            Directory.CreateDirectory(home);
            Directory.CreateDirectory(project);
            File.WriteAllText(Path.Combine(install, DaorisHome.InstallMarker), "# Daoris\n");
            var kept = Path.Combine(project, "session.jsonl");
            File.WriteAllText(kept, "kept\n");
            var world = new WiringWorld
            {
                Home = home, BaseDirectory = Path.Combine(install, "app"), Process = _ => null,
                Account = _ => null, Machine = _ => null, Lives = (_, _) => false,
                AskStatus = _ => throw new InvalidOperationException("no network expected"), Sizes = new WiringSizes(),
                ToolHome = tool, ToolHomeFrom = "named", ToolOrigins = [new ToolOrigin(tree, "tree", "session", "s1")],
            };
            var answer = SystemWiring.Read(world);
            var row = Assert.Single(answer["toolHome"]!["folders"]!.AsArray())!;
            Assert.Equal("s1", (string?)row["by"]![0]);
            Assert.Equal("session", (string?)row["keeps"]);
            Assert.Null(row["bytes"]);
            Assert.Equal("not-measured", (string?)row["bytesWhy"]);
            Assert.Null(world.Sizes.Of(home));
            Assert.Equal("kept\n", File.ReadAllText(kept));
            Assert.Equal(0, WiringCommand.Run(["--json"], world, out var json));
            Assert.Equal(SystemWiring.Json(answer), json);
            Assert.Equal(0, WiringCommand.Run([], world, out var text));
            Assert.Equal(WiringCommand.Text(answer), text);
            var evidence = Path.Combine(GitSeed.RepoRoot(), "local", "scratch", "workplace-loop");
            Directory.CreateDirectory(evidence);
            File.WriteAllText(Path.Combine(evidence, "parity-current.json"), json, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(evidence, "parity-current.txt"), text, new UTF8Encoding(false));
        }
        finally { GitSeed.Remove(scratch); }
    }
}
