using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// How a scope's list is used (TOOL6a; D130 §2, §3.1, §4.6, §14, as §16.6 amends them): <c>rotationUse</c> and
/// <c>workspaceRotationUse</c> in <c>harnesses.json</c>, beside the lists — <i>use accounts</i> (<c>use</c>: <c>goal</c>
/// or <c>order</c>), <i>keep for conversations</i> (<c>keep</c>) and <i>switch before the limit</i> (<c>early</c>,
/// <c>near</c>) — and the rule binding a scope's default and kept account to its list. The driver's half of a TWIN with the
/// CLI's <c>rotation.ts</c>, whose <c>rotation-use.test.ts</c> holds the same tables, row for row and in the same order,
/// and parses these theories to hold them to its own, cell for cell.
/// </summary>
/// <remarks>
/// <para>A scope's settings are said in one word each, today's defaults left out, then <c>?name</c> for each value or
/// setting this build does not know (<see cref="Said"/>). A change is JSON, where <c>"keep": null</c> keeps none, a field
/// absent is left as it was, and <c>null</c> clears the scope's settings. Nothing here counts accounts (§2 rule 6).</para>
/// <para>The driver only stores and reads these here; nothing chooses an account by them until TOOL6b.</para>
/// <para>🔴 <b>Keep each row on one line, its cells literals</b>: the CLI's test reads them.</para>
/// </remarks>
public sealed class RotationUseTwinTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-rotation-use-" + Guid.NewGuid().ToString("N")[..8]);

    public RotationUseTwinTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Wiring => Path.Combine(_home, "harnesses.json");

    private HarnessSettings Read(string file)
    {
        File.WriteAllText(Wiring, file);
        return HarnessSettings.Load(Wiring);
    }

    private static List<string> Names(string list) => [.. JsonNode.Parse(list)!.AsArray().Select(name => name!.GetValue<string>())];

    /// <summary>The named sections of the file as written, those it holds.</summary>
    private JsonObject Written(params string[] sections)
    {
        var root = JsonNode.Parse(File.ReadAllText(Wiring))!.AsObject();
        var picked = new JsonObject();
        foreach (var section in sections)
        {
            if (root[section] is { } node) picked[section] = node.DeepClone();
        }

        return picked;
    }

    /// <summary>
    /// A scope's settings in one word each, today's defaults left out, then what this build does not know. The defaults
    /// are spelled here on purpose, not read from <see cref="RotationUse.Default"/>: a test below holds the record to them.
    /// </summary>
    private static string Said(RotationUse use, IReadOnlyList<string> unknown)
    {
        var parts = new[]
        {
            use.Use != "goal" ? use.Use : null,
            use.Keep is { } keep ? $"keep={keep}" : null,
            !use.Early ? "early=off" : null,
            use.Near != 90 ? $"near={use.Near}" : null,
        }.OfType<string>().Concat(unknown.Select(name => $"?{name}")).ToList();
        return parts.Count > 0 ? string.Join(" ", parts) : "none";
    }

    /// <summary>A change as both tables spell it; <c>null</c> clears.</summary>
    private static UseChange? Change(string json)
    {
        if (JsonNode.Parse(json) is not JsonObject node) return null;
        return new UseChange(
            Use: node["use"]?.GetValue<string>(),
            Keep: node["keep"]?.GetValue<string>(),
            NoKeep: node.ContainsKey("keep") && node["keep"] is null,
            Early: node["early"]?.GetValue<bool>(),
            Near: node["near"]?.GetValue<int>());
    }

    private static string Rung(ChoiceFrom from) => from switch
    {
        ChoiceFrom.Workspace => "workspace",
        ChoiceFrom.Machine => "machine",
        _ => from.ToString(),
    };

    [Fact]
    public void Todays_defaults_live_in_one_place()
    {
        Assert.Equal(new RotationUse("goal", Keep: null, Early: true, Near: 90), RotationUse.Default);
        Assert.Equal(["goal", "order"], RotationUse.Modes);
        Assert.Equal((50, 99), (RotationUse.NearLowest, RotationUse.NearHighest));
    }

    // ——— Read and resolved (§2 rule 1, §14, §16.6): the scope a start reads, its list, where it begins, and its settings.

    [Theory]
    [InlineData("nothing set anywhere is the machine's scope with no list, every setting its default", "{}", "claude-code", null, "machine", null, null, "none")]
    [InlineData("a machine list and no settings: make the most of them, switching before the limit, beginning at its first", """{"rotation":{"claude-code":["account-1","account-2"]}}""", "claude-code", null, "machine", """["account-1","account-2"]""", "account-1", "none")]
    [InlineData("every setting as written", """{"rotation":{"claude-code":["account-1","account-2","account-3"]},"rotationUse":{"claude-code":{"use":"order","keep":"account-3","early":false,"near":85}}}""", "claude-code", null, "machine", """["account-1","account-2","account-3"]""", "account-1", "order keep=account-3 early=off near=85")]
    [InlineData("a choice equal to today's default is that choice", """{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"use":"goal","early":true,"near":90}}}""", "claude-code", null, "machine", """["account-1"]""", "account-1", "none")]
    [InlineData("a way to use accounts this build does not know reads as the default, and is said", """{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"use":"pace"}}}""", "claude-code", null, "machine", """["account-1"]""", "account-1", "?use")]
    [InlineData("a way to use accounts in another case is one this build does not know", """{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"use":"ORDER"}}}""", "claude-code", null, "machine", """["account-1"]""", "account-1", "?use")]
    [InlineData("switching before the limit is off only as JSON false", """{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"early":false}}}""", "claude-code", null, "machine", """["account-1"]""", "account-1", "early=off")]
    [InlineData("a switch that is neither true nor false reads as on, and is said", """{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"early":"no"}}}""", "claude-code", null, "machine", """["account-1"]""", "account-1", "?early")]
    [InlineData("the retired prefer and parallel are skipped, and not said", """{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"prefer":"left","parallel":true}}}""", "claude-code", null, "machine", """["account-1"]""", "account-1", "none")]
    [InlineData("near at its lowest, 50, as written", """{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"near":50}}}""", "claude-code", null, "machine", """["account-1"]""", "account-1", "near=50")]
    [InlineData("near at its highest, 99, as written", """{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"near":99}}}""", "claude-code", null, "machine", """["account-1"]""", "account-1", "near=99")]
    [InlineData("near written as a whole number with a point is that number", """{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"near":85.0}}}""", "claude-code", null, "machine", """["account-1"]""", "account-1", "near=85")]
    [InlineData("near under 50 reads as 90, and is said", """{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"near":49}}}""", "claude-code", null, "machine", """["account-1"]""", "account-1", "?near")]
    [InlineData("near over 99 reads as 90, and is said", """{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"near":100}}}""", "claude-code", null, "machine", """["account-1"]""", "account-1", "?near")]
    [InlineData("near that is not whole reads as 90, and is said", """{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"near":85.5}}}""", "claude-code", null, "machine", """["account-1"]""", "account-1", "?near")]
    [InlineData("near that is not a number reads as 90, and is said", """{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"near":"85"}}}""", "claude-code", null, "machine", """["account-1"]""", "account-1", "?near")]
    [InlineData("a kept account is trimmed", """{"rotation":{"claude-code":["account-1","account-2"]},"rotationUse":{"claude-code":{"keep":" account-2 "}}}""", "claude-code", null, "machine", """["account-1","account-2"]""", "account-1", "keep=account-2")]
    [InlineData("a kept account not in the list is none", """{"rotation":{"claude-code":["account-1","account-2"]},"rotationUse":{"claude-code":{"keep":"account-9"}}}""", "claude-code", null, "machine", """["account-1","account-2"]""", "account-1", "none")]
    [InlineData("a kept account in another case than the list's is none", """{"rotation":{"claude-code":["account-1","account-2"]},"rotationUse":{"claude-code":{"keep":"ACCOUNT-2"}}}""", "claude-code", null, "machine", """["account-1","account-2"]""", "account-1", "none")]
    [InlineData("a kept account that is not a name reads as none, and is said", """{"rotation":{"claude-code":["account-1","account-2"]},"rotationUse":{"claude-code":{"keep":["account-2"]}}}""", "claude-code", null, "machine", """["account-1","account-2"]""", "account-1", "?keep")]
    [InlineData("a setting this build does not know changes nothing, and is said", """{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"weekly":"pace","use":"order"}}}""", "claude-code", null, "machine", """["account-1"]""", "account-1", "order ?weekly")]
    [InlineData("settings with no list are not read", """{"rotationUse":{"claude-code":{"use":"order","early":false}}}""", "claude-code", null, "machine", null, null, "none")]
    [InlineData("settings that are not an object are none", """{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":"order"}}""", "claude-code", null, "machine", """["account-1"]""", "account-1", "none")]
    [InlineData("the machine's default in its list is where it begins", """{"defaults":{"claude-code":"account-2"},"rotation":{"claude-code":["account-1","account-2"]}}""", "claude-code", null, "machine", """["account-1","account-2"]""", "account-2", "none")]
    [InlineData("a default outside its list: the list wins, and it begins at its first", """{"defaults":{"claude-code":"account-9"},"rotation":{"claude-code":["account-1","account-2"]}}""", "claude-code", null, "machine", """["account-1","account-2"]""", "account-1", "none")]
    [InlineData("no list: the scope is its default alone", """{"defaults":{"claude-code":"account-2"}}""", "claude-code", null, "machine", null, "account-2", "none")]
    [InlineData("a workspace with a list of its own reads its own settings, never the machine's", """{"rotation":{"claude-code":["account-1","account-2"]},"rotationUse":{"claude-code":{"use":"order"}},"workspaceRotation":{"work":{"claude-code":["account-2","account-3"]}},"workspaceRotationUse":{"work":{"claude-code":{"early":false}}}}""", "claude-code", "work", "workspace", """["account-2","account-3"]""", "account-2", "early=off")]
    [InlineData("a workspace with a list of its own and no settings has every setting its default", """{"rotation":{"claude-code":["account-1","account-2"]},"rotationUse":{"claude-code":{"use":"order"}},"workspaceRotation":{"work":{"claude-code":["account-2","account-3"]}}}""", "claude-code", "work", "workspace", """["account-2","account-3"]""", "account-2", "none")]
    [InlineData("a workspace that names its own default and no list is its own scope, that account alone", """{"rotation":{"claude-code":["account-1","account-2"]},"rotationUse":{"claude-code":{"use":"order"}},"workspaces":{"work":{"claude-code":"account-2"}}}""", "claude-code", "work", "workspace", null, "account-2", "none")]
    [InlineData("a workspace's own default in its own list is where it begins", """{"workspaces":{"work":{"claude-code":"account-3"}},"workspaceRotation":{"work":{"claude-code":["account-2","account-3"]}}}""", "claude-code", "work", "workspace", """["account-2","account-3"]""", "account-3", "none")]
    [InlineData("a workspace that names nothing reads the machine's scope and settings", """{"rotation":{"claude-code":["account-1","account-2"]},"rotationUse":{"claude-code":{"use":"order"}},"workspaceRotation":{"work":{"claude-code":["account-2"]}}}""", "claude-code", "home", "machine", """["account-1","account-2"]""", "account-1", "order")]
    [InlineData("a workspace's settings with no list of its own are not read", """{"rotation":{"claude-code":["account-1"]},"workspaces":{"work":{"claude-code":"account-1"}},"workspaceRotationUse":{"work":{"claude-code":{"early":false}}}}""", "claude-code", "work", "workspace", null, "account-1", "none")]
    [InlineData("a workspace's list for another agent is not this one's", """{"rotation":{"claude-code":["account-1"]},"workspaceRotation":{"work":{"codex":["account-9"]}},"workspaceRotationUse":{"work":{"codex":{"early":false}}}}""", "claude-code", "work", "machine", """["account-1"]""", "account-1", "none")]
    [InlineData("one account in a list: every setting as written, nothing counted", """{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"use":"order","early":false}}}""", "claude-code", null, "machine", """["account-1"]""", "account-1", "order early=off")]
    [InlineData("a file that does not read is none", "not json", "claude-code", null, "machine", null, null, "none")]
    public void A_scope_is_read_as_the_cli_reads_it(string name, string file, string agent, string? workspace, string from, string? list, string? begins, string use)
    {
        var scope = Read(file).ResolveScope(agent, workspace);

        var wanted = list is null ? [] : Names(list);
        Assert.True(from == Rung(scope.From), $"{name}: {Rung(scope.From)}");
        Assert.True(wanted.SequenceEqual(scope.List), $"{name}: [{string.Join(", ", scope.List)}]");
        Assert.True(begins == scope.Begins, $"{name}: begins {scope.Begins}");
        Assert.True(use == Said(scope.Use, scope.Unknown), $"{name}: {Said(scope.Use, scope.Unknown)}");
    }

    // ——— Edited (§2, §16.6): a change merged into the scope's settings, each choice written as made, so a later default
    // never overturns one; what this build does not know kept as written; null clears the scope's settings, and a scope
    // with none is dropped.

    [Theory]
    [InlineData("one by one, in order, set on the machine", "{}", "claude-code", """{"use":"order"}""", null, """{"rotationUse":{"claude-code":{"use":"order"}}}""")]
    [InlineData("every setting set at once", "{}", "claude-code", """{"near":85,"early":false,"keep":"account-3","use":"order"}""", null, """{"rotationUse":{"claude-code":{"use":"order","keep":"account-3","early":false,"near":85}}}""")]
    [InlineData("a setting changed keeps the others", """{"rotationUse":{"claude-code":{"use":"order","early":false}}}""", "claude-code", """{"use":"goal"}""", null, """{"rotationUse":{"claude-code":{"use":"goal","early":false}}}""")]
    [InlineData("a choice equal to today's default is written as chosen, so a later default does not overturn it", """{"rotationUse":{"claude-code":{"use":"order","early":false,"near":85}}}""", "claude-code", """{"use":"goal","early":true,"near":90}""", null, """{"rotationUse":{"claude-code":{"use":"goal","early":true,"near":90}}}""")]
    [InlineData("keeping none takes the kept account out, and nothing else", """{"rotationUse":{"claude-code":{"use":"order","keep":"account-3"}}}""", "claude-code", """{"keep":null}""", null, """{"rotationUse":{"claude-code":{"use":"order"}}}""")]
    [InlineData("keeping none where it was all that was set leaves the scope no settings", """{"rotationUse":{"claude-code":{"keep":"account-3"}}}""", "claude-code", """{"keep":null}""", null, "{}")]
    [InlineData("another agent's settings are kept", """{"rotationUse":{"codex":{"early":false}}}""", "claude-code", """{"use":"order"}""", null, """{"rotationUse":{"claude-code":{"use":"order"},"codex":{"early":false}}}""")]
    [InlineData("a workspace's settings set, the machine's kept", """{"rotationUse":{"claude-code":{"use":"order"}}}""", "claude-code", """{"early":false}""", "work", """{"rotationUse":{"claude-code":{"use":"order"}},"workspaceRotationUse":{"work":{"claude-code":{"early":false}}}}""")]
    [InlineData("the machine's settings cleared, another agent's kept", """{"rotationUse":{"claude-code":{"use":"order","weekly":"pace"},"codex":{"early":false}}}""", "claude-code", "null", null, """{"rotationUse":{"codex":{"early":false}}}""")]
    [InlineData("a workspace's settings cleared, and a workspace left with none dropped", """{"workspaceRotationUse":{"work":{"claude-code":{"use":"order"}},"lab":{"codex":{"early":false}}}}""", "claude-code", "null", "work", """{"workspaceRotationUse":{"lab":{"codex":{"early":false}}}}""")]
    [InlineData("a kept account is kept trimmed", "{}", "claude-code", """{"keep":" account-2 "}""", null, """{"rotationUse":{"claude-code":{"keep":"account-2"}}}""")]
    [InlineData("a value and a setting this build does not know are kept when another is set", """{"rotationUse":{"claude-code":{"use":"pace","weekly":{"spread":true}}}}""", "claude-code", """{"early":false}""", null, """{"rotationUse":{"claude-code":{"use":"pace","early":false,"weekly":{"spread":true}}}}""")]
    [InlineData("a value this build does not know is replaced when that setting is set", """{"rotationUse":{"claude-code":{"use":"pace"}}}""", "claude-code", """{"use":"order"}""", null, """{"rotationUse":{"claude-code":{"use":"order"}}}""")]
    [InlineData("the retired prefer and parallel go with any edit", """{"rotationUse":{"claude-code":{"prefer":"left","parallel":true,"near":85}}}""", "claude-code", """{"use":"order"}""", null, """{"rotationUse":{"claude-code":{"use":"order","near":85}}}""")]
    [InlineData("no change changes nothing", """{"rotationUse":{"claude-code":{"near":85}}}""", "claude-code", "{}", null, """{"rotationUse":{"claude-code":{"near":85}}}""")]
    public void Settings_are_set_and_cleared_as_the_cli_writes_them(string why, string before, string agent, string change, string? workspace, string after)
    {
        Read(before).WithUse(agent, Change(change), workspace).Save(Wiring);

        var written = Written("rotationUse", "workspaceRotationUse");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(after), written), $"{why}: {written.ToJsonString()}");
    }

    // ——— A list's settings come with it (§2 rule 1): a list cleared takes its scope's settings; a list replaced keeps them.

    [Theory]
    [InlineData("the machine's list cleared takes its settings with it, another agent's kept", """{"rotation":{"claude-code":["account-1"],"codex":["account-1"]},"rotationUse":{"claude-code":{"use":"order"},"codex":{"early":false}}}""", "claude-code", null, null, """{"rotationUse":{"codex":{"early":false}}}""")]
    [InlineData("a workspace's list cleared takes its settings with it, the machine's kept", """{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"use":"order"}},"workspaceRotation":{"work":{"claude-code":["account-2"]}},"workspaceRotationUse":{"work":{"claude-code":{"early":false}}}}""", "claude-code", null, "work", """{"rotationUse":{"claude-code":{"use":"order"}}}""")]
    [InlineData("a list replaced keeps its settings", """{"rotation":{"claude-code":["account-1","account-2"]},"rotationUse":{"claude-code":{"use":"order","keep":"account-2"}}}""", "claude-code", """["account-2","account-1"]""", null, """{"rotationUse":{"claude-code":{"use":"order","keep":"account-2"}}}""")]
    [InlineData("a list of nobody is a clear, and its settings go", """{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"early":false}}}""", "claude-code", "[]", null, "{}")]
    public void A_lists_settings_go_with_it_as_the_cli_writes_them(string why, string before, string agent, string? order, string? workspace, string after)
    {
        Read(before).WithRotation(agent, order is null ? null : Names(order), workspace).Save(Wiring);

        var written = Written("rotationUse", "workspaceRotationUse");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(after), written), $"{why}: {written.ToJsonString()}");
    }

    // ——— The rule binding a scope (§3.1, §4.6): its default and its kept account are of its list, and driven work keeps an
    // account. The first problem is the one said; no list is refused for how many it names.

    [Theory]
    [InlineData("a default in its list, and a kept account beside another", "account-2", """["account-1","account-2"]""", "account-1", null)]
    [InlineData("no list: a default is its one account, and nothing is refused", "account-9", "[]", null, null)]
    [InlineData("one account, its default, keeping none: nothing is counted", "account-1", """["account-1"]""", null, null)]
    [InlineData("six accounts, nothing counted", "account-5", """["account-1","account-2","account-3","account-4","account-5","account-6"]""", "account-6", null)]
    [InlineData("no default and no kept account", null, """["account-1"]""", null, null)]
    [InlineData("a default outside its list", "account-9", """["account-1","account-2"]""", null, "default account-9")]
    [InlineData("a default in another case than the list's is outside it", "Account-1", """["account-1"]""", null, "default Account-1")]
    [InlineData("a kept account outside its list", null, """["account-1","account-2"]""", "account-9", "keep account-9")]
    [InlineData("a kept account with no list is outside it", null, "[]", "account-1", "keep account-1")]
    [InlineData("keeping the one account a list holds leaves driven work none", null, """["account-1"]""", "account-1", "alone account-1")]
    [InlineData("the default is said before the kept account", "account-9", """["account-1"]""", "account-8", "default account-9")]
    public void A_scope_is_refused_as_the_cli_refuses_it(string why, string? scopeDefault, string list, string? keep, string? refused)
    {
        var problem = ScopeProblem.Of(scopeDefault, Names(list), keep);

        var said = problem is null ? null : $"{problem.Kind.ToString().ToLowerInvariant()} {problem.Account}";
        Assert.True(refused == said, $"{why}: {said}");
    }

    // ——— What the twin tables do not hold: a kept account read only of its list, and the save's refusal.

    [Fact]
    public void A_kept_account_outside_its_list_is_kept_as_written_and_read_as_none()
    {
        var settings = new HarnessSettings().WithRotation("claude-code", ["account-1", "account-2"])
            .WithUse("claude-code", new UseChange(Keep: "account-9"));

        Assert.Null(settings.ResolveScope("claude-code", null).Use.Keep);
        Assert.Equal(["account-1", "account-2"], settings.ResolveScope("claude-code", null).List);
        Assert.Equal("account-9", settings.Uses["claude-code"]["keep"]?.GetString());
    }

    [Fact]
    public void An_edit_over_wiring_that_could_not_be_read_is_refused_and_keeps_the_settings()
    {
        File.WriteAllText(Wiring, "{ not json");

        Assert.Throws<DriverException>(() => HarnessSettings.Load(Wiring).WithUse("claude-code", new UseChange(Use: "order")).Save(Wiring));
        Assert.Equal("{ not json", File.ReadAllText(Wiring));
    }
}
