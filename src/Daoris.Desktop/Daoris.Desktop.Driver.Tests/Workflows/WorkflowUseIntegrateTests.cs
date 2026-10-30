using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1r part r2 (the integration design §17.7; the workflow design §2.3, §4.7): <c>daoris driver workflow use</c> of a graph version
/// that integrates its landing. It refuses, writing nothing, where a repository it reaches cannot carry the <c>integrate</c> (each case its own
/// sentence naming the Setup row), and says, per repository it reaches, <i>integrates with no gates</i> where the rule's
/// <c>integrate.gates</c> is absent or <c>none</c>. A workspace's <c>use</c> reaches the registry's repositories of that workspace whose own
/// choice does not replace it.
/// </summary>
public sealed class WorkflowUseIntegrateTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "workflow-use-integrate", Guid.NewGuid().ToString("N")[..8]);
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public WorkflowUseIntegrateTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private string Config => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    private const string Branch = """{"form":"branch","pattern":"w/{quest}"}""";

    private const string Plugged = """{"form":"branch","pattern":"w/{quest}","plugin":"example.pull-request"}""";

    private const string Gated ="""{"form":"branch","pattern":"w/{quest}","autoAccept":true,"integrate":{"gates":"declared"}}""";

    /// <summary>The workflow `integrated`: a claimed node, the landing and an integrate asking as <paramref name="request"/>.</summary>
    private void Stored(string request = "owner", string landing = "{}")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(WorkflowStore.PathOf(_home, "integrated"))!);
        File.WriteAllText(WorkflowStore.PathOf(_home, "integrated"), $$$"""
            {"id":"integrated","name":"Integrated","versions":[{"version":1,"at":"2026-10-21T09:00:00Z","door":"terminal","schema":2,"graph":
            {"schemaVersion":2,"entries":["claim"],"end":"merge-in",
             "nodes":[{"id":"claim","kind":"claimed","version":1,"reach":"observed"},
                      {"id":"land","kind":"handed","version":1,"reach":"observed","options":{{{landing}}}},
                      {"id":"merge-in","kind":"integrate","version":1,"reach":"waited","options":{"request":"{{{request}}}"}}],
             "wires":[{"id":"a","kind":"after","from":"claim","to":"land"},{"id":"b","kind":"after","from":"land","to":"merge-in"}]}}]}
            """);
    }

    private void Rules(string landings) => File.WriteAllText(Config, $$"""{"landings":{{landings}}}""");

    private async Task<int> Run(string words, params (string Repository, string? Workspace, string? Root)[] registry)
    {
        var world = new DriverChoicesWorld
        {
            Verbs = DriverChoiceVerbs.All,
            Instance = new InstanceWorld { Home = _home, Environment = _ => null, Out = _out, Error = _error, Verb = "daoris" },
            ConfigPath = Config,
            Folder = _home,
            Now = () => new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero),
            Registry = _ => Task.FromResult(new ChoicesRegistry(registry, null)),
        };
        return await DriverChoicesCommand.RunAsync(words.Split(' '), world, CancellationToken.None);
    }

    private bool Chose() => File.Exists(Config) && File.ReadAllText(Config).Contains("\"workflows\"", StringComparison.Ordinal);

    [Fact]
    public async Task A_repository_whose_rule_declares_no_integrate_is_said_to_integrate_with_no_gates()
    {
        Stored();
        Rules($$"""{"web-app":{{Branch}}}""");

        var exit = await Run("workflow use integrated --repository web-app", ("web-app", "work", null));

        Assert.Equal((0, ""), (exit, _error.ToString()));
        Assert.Contains("`web-app` integrates with no gates", _out.ToString());
        Assert.True(Chose());
    }

    [Fact]
    public async Task A_repository_whose_rule_declares_its_gates_is_not_said_to_integrate_with_no_gates()
    {
        Stored();
        Rules($$"""{"web-app":{{Gated}}}""");

        var exit = await Run("workflow use integrated --repository web-app", ("web-app", "work", null));

        Assert.Equal((0, ""), (exit, _error.ToString()));
        Assert.DoesNotContain("integrates with no gates", _out.ToString());
        Assert.True(Chose());
    }

    [Fact]
    public async Task A_merge_rule_repository_refuses_the_choice_naming_the_pattern_and_the_setup_row_and_writes_nothing()
    {
        Stored();
        Rules("""{"web-app":{"form":"merge"}}""");

        var exit = await Run("workflow use integrated --repository web-app", ("web-app", "work", null));

        Assert.Equal(1, exit);
        Assert.Contains("`web-app`: its `merge-in` (`integrate`) integrates the branch the landing `land` makes", _error.ToString());
        Assert.Contains("daoris driver landing web-app branch <pattern>", _error.ToString());
        Assert.Contains("Setup → Line and landing → How work lands", _error.ToString());
        Assert.EndsWith("Nothing was written.", _error.ToString().Trim());
        Assert.False(Chose());
    }

    [Fact]
    public async Task A_workspace_choice_says_each_repository_it_reaches_and_skips_one_whose_own_choice_replaces_it()
    {
        Stored();
        File.WriteAllText(Config, $$"""{"landings":{"a":{{Branch}},"b":{{Gated}},"c":{"form":"merge"},"d":{{Branch}}},""" + "\"workflows\":{\"c\":{\"default\":\"other\"}}}");

        var exit = await Run("workflow use integrated --workspace work", ("a", "work", null), ("b", "work", null), ("c", "work", null), ("d", "elsewhere", null));

        Assert.Equal((0, ""), (exit, _error.ToString()));
        var said = _out.ToString();
        Assert.Contains("`a` integrates with no gates", said);
        Assert.DoesNotContain("`b` integrates with no gates", said);
        Assert.DoesNotContain("`c`", said);
        Assert.DoesNotContain("`d`", said);
    }

    [Fact]
    public async Task A_workspace_choice_reaching_a_repository_that_cannot_carry_it_is_refused_whole_naming_that_repository()
    {
        Stored();
        Rules($$"""{"a":{{Branch}},"b":{{Plugged}}}""");

        var exit = await Run("workflow use integrated --workspace work", ("a", "work", null), ("b", "work", null));

        Assert.Equal(1, exit);
        Assert.Contains("`b`: its `merge-in` (`integrate`) integrates a branch no plugin lands", _error.ToString());
        Assert.DoesNotContain("`a`:", _error.ToString());
        Assert.False(Chose());
    }

    [Fact]
    public async Task Done_is_refused_where_the_accepted_declaration_cannot_be_read_and_a_press_where_the_landing_accepts_by_press()
    {
        Stored("done", """{"accept":"automatic"}""");
        Rules($$"""{"web-app":{{Gated}}}""");

        // No checkout here to read the line's accepted declaration from: nothing is passed on a guess.
        Assert.Equal(1, await Run("workflow use integrated --repository web-app", ("web-app", "work", null)));
        Assert.Contains("whether the declaration the owner accepted names a gate could not be read", _error.ToString());
        Assert.False(Chose());

        // The landing's press: the version says `you`.
        Stored("steward", """{"accept":"you"}""");
        Rules("""{"web-app":{"form":"branch","pattern":"w/{quest}","integrate":{"gates":"none"}}}""");
        _error.GetStringBuilder().Clear();
        Assert.Equal(1, await Run("workflow use integrated --repository web-app", ("web-app", "work", null)));
        Assert.Contains("does not set `integrate.gates` to `declared`", _error.ToString());
    }

    [Fact]
    public async Task A_version_that_integrates_nothing_says_nothing_of_gates()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(WorkflowStore.PathOf(_home, "plain"))!);
        File.WriteAllText(WorkflowStore.PathOf(_home, "plain"), """
            {"id":"plain","name":"Plain","versions":[{"version":1,"at":"2026-10-21T09:00:00Z","door":"terminal","schema":2,"graph":
            {"schemaVersion":2,"entries":["claim"],"end":"land",
             "nodes":[{"id":"claim","kind":"claimed","version":1,"reach":"observed"},{"id":"land","kind":"handed","version":1,"reach":"observed"}],
             "wires":[{"id":"a","kind":"after","from":"claim","to":"land"}]}}]}
            """);
        Rules($$"""{"web-app":{{Branch}}}""");

        Assert.Equal(0, await Run("workflow use plain --repository web-app", ("web-app", "work", null)));
        Assert.DoesNotContain("gates", _out.ToString());
    }
}
