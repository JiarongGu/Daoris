using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The order in <c>harnesses.json</c> (TOOL4e, D125 §3.1): <c>rotation</c> and <c>workspaceRotation</c>, the accounts
/// rotation may use per agent, for the machine and for a workspace. The driver's half of a TWIN with the CLI's
/// <c>rotation.ts</c> and <c>toolchain.ts</c>, whose <c>rotation.test.ts</c> holds the same tables, row for row and in
/// the same order, and parses these theories to hold them to its own, cell for cell.
/// </summary>
/// <remarks>
/// <para>An order is a JSON list of account names; <c>null</c> is none. A file is the text on disk, and an <c>after</c>
/// is the sections a save writes, compared as JSON.</para>
/// <para>🔴 <b>Keep each row on one line, its cells literals</b>: the CLI's test reads them.</para>
/// </remarks>
public sealed class RotationTwinTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-rotation-" + Guid.NewGuid().ToString("N")[..8]);

    public RotationTwinTests() => Directory.CreateDirectory(_home);

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

    private static string Rung(ChoiceFrom from) => from switch
    {
        ChoiceFrom.Workspace => "workspace",
        ChoiceFrom.Machine => "machine",
        ChoiceFrom.Unset => "unset",
        _ => from.ToString(),
    };

    // ——— Reading and resolving (§3.1): the workspace's order for the agent, else the machine's, else none.

    [Theory]
    [InlineData("no order anywhere is none, and nothing rotates", "{}", "claude-code", null, null, "unset")]
    [InlineData("the machine's order, as written", """{"rotation":{"claude-code":["account-1","account-2"]}}""", "claude-code", null, """["account-1","account-2"]""", "machine")]
    [InlineData("a workspace's own order wins over the machine's", """{"rotation":{"claude-code":["account-1","account-2"]},"workspaceRotation":{"work":{"claude-code":["account-2","account-3"]}}}""", "claude-code", "work", """["account-2","account-3"]""", "workspace")]
    [InlineData("a workspace with no order of its own takes the machine's", """{"rotation":{"claude-code":["account-1","account-2"]},"workspaceRotation":{"work":{"claude-code":["account-2","account-3"]}}}""", "claude-code", "home", """["account-1","account-2"]""", "machine")]
    [InlineData("a workspace's order for another agent is not this one's", """{"rotation":{"claude-code":["account-1"]},"workspaceRotation":{"work":{"codex":["account-9"]}}}""", "claude-code", "work", """["account-1"]""", "machine")]
    [InlineData("another agent's order is not this one's", """{"rotation":{"codex":["account-1"]}}""", "claude-code", null, null, "unset")]
    [InlineData("the order is kept as the person wrote it, never sorted", """{"rotation":{"claude-code":["account-3","account-1","account-2"]}}""", "claude-code", null, """["account-3","account-1","account-2"]""", "machine")]
    [InlineData("a name is trimmed, and a blank and what is not a name are skipped", """{"rotation":{"claude-code":[" account-1 ","",7,null,"account-2"]}}""", "claude-code", null, """["account-1","account-2"]""", "machine")]
    [InlineData("a name written twice, in any case, is read once where first written", """{"rotation":{"claude-code":["account-2","account-1","ACCOUNT-2"]}}""", "claude-code", null, """["account-2","account-1"]""", "machine")]
    [InlineData("an order that is not a list is none", """{"rotation":{"claude-code":"account-1"}}""", "claude-code", null, null, "unset")]
    [InlineData("an order that names nobody is none, so the workspace takes the machine's", """{"rotation":{"claude-code":["account-1"]},"workspaceRotation":{"work":{"claude-code":["  "]}}}""", "claude-code", "work", """["account-1"]""", "machine")]
    [InlineData("a rotation that is not an object is none", """{"rotation":["account-1"]}""", "claude-code", null, null, "unset")]
    [InlineData("a workspace's orders that are not an object are none", """{"workspaceRotation":{"work":["account-1"]}}""", "claude-code", "work", null, "unset")]
    [InlineData("a file that does not read is none", "not json", "claude-code", null, null, "unset")]
    [InlineData("a letter whose capital is two letters is not those two: straße is not STRASSE", """{"rotation":{"claude-code":["straße","STRASSE"]}}""", "claude-code", null, """["straße","STRASSE"]""", "machine")]
    [InlineData("a dotted capital I is not an i with a dot above", """{"rotation":{"claude-code":["İzmir","i\u0307zmir"]}}""", "claude-code", null, """["İzmir","i\u0307zmir"]""", "machine")]
    public void An_order_resolves_as_the_cli_resolves_it(string name, string file, string agent, string? workspace, string? order, string from)
    {
        var (resolved, rung) = Read(file).ResolveRotationFrom(agent, workspace);

        var wanted = order is null ? [] : Names(order);
        Assert.True(wanted.SequenceEqual(resolved), $"{name}: [{string.Join(", ", resolved)}]");
        Assert.True(from == Rung(rung), $"{name}: {Rung(rung)}");
    }

    // ——— Editing (§3.1, §6): an order set replaces the agent's whole; cleared, its entry goes; written only when set.

    [Theory]
    [InlineData("a machine order set", "{}", "claude-code", """["account-1","account-2"]""", null, """{"rotation":{"claude-code":["account-1","account-2"]}}""")]
    [InlineData("a machine order replaced whole, in its new order", """{"rotation":{"claude-code":["account-1","account-2"]}}""", "claude-code", """["account-2","account-1"]""", null, """{"rotation":{"claude-code":["account-2","account-1"]}}""")]
    [InlineData("a machine order cleared, another agent's kept", """{"rotation":{"claude-code":["account-1"],"codex":["account-1"]}}""", "claude-code", null, null, """{"rotation":{"codex":["account-1"]}}""")]
    [InlineData("the last order cleared leaves no rotation in the file", """{"rotation":{"claude-code":["account-1"]}}""", "claude-code", null, null, "{}")]
    [InlineData("a workspace order set, the machine's kept", """{"rotation":{"claude-code":["account-1"]}}""", "claude-code", """["account-2","account-3"]""", "work", """{"rotation":{"claude-code":["account-1"]},"workspaceRotation":{"work":{"claude-code":["account-2","account-3"]}}}""")]
    [InlineData("a workspace order cleared, another agent's there kept", """{"workspaceRotation":{"work":{"claude-code":["account-2"],"codex":["account-1"]}}}""", "claude-code", null, "work", """{"workspaceRotation":{"work":{"codex":["account-1"]}}}""")]
    [InlineData("a workspace left with no order is dropped", """{"workspaceRotation":{"work":{"claude-code":["account-2"]},"lab":{"codex":["account-1"]}}}""", "claude-code", null, "work", """{"workspaceRotation":{"lab":{"codex":["account-1"]}}}""")]
    [InlineData("an order of nobody is a clear", """{"rotation":{"claude-code":["account-1"]}}""", "claude-code", "[]", null, "{}")]
    [InlineData("clearing what is not set changes nothing", """{"rotation":{"codex":["account-1"]}}""", "claude-code", null, "work", """{"rotation":{"codex":["account-1"]}}""")]
    [InlineData("a name is kept trimmed", "{}", "claude-code", """[" account-1 "]""", null, """{"rotation":{"claude-code":["account-1"]}}""")]
    public void An_order_is_set_and_cleared_as_the_cli_writes_it(string why, string before, string agent, string? order, string? workspace, string after)
    {
        Read(before).WithRotation(agent, order is null ? null : Names(order), workspace).Save(Wiring);

        var written = Written("rotation", "workspaceRotation");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(after), written), $"{why}: {written.ToJsonString()}");
    }

    // ——— Refused by both doors (§3.1): an account that does not exist, or one named twice.

    [Theory]
    [InlineData("accounts that exist, each once, in any order", """["account-1","account-2"]""", """["account-2","account-1"]""", null)]
    [InlineData("an account that does not exist", """["account-1"]""", """["account-1","account-9"]""", "missing account-9")]
    [InlineData("one account twice", """["account-1","account-2"]""", """["account-1","account-2","account-1"]""", "twice account-1")]
    [InlineData("a name in another case than its directory's names no account", """["account-1"]""", """["Account-1"]""", "missing Account-1")]
    [InlineData("one account twice in another case, where both directories are there", """["account-1","ACCOUNT-1"]""", """["account-1","ACCOUNT-1"]""", "twice ACCOUNT-1")]
    [InlineData("the first problem in the order is the one said", """["account-1"]""", """["account-8","account-1","account-1"]""", "missing account-8")]
    [InlineData("a letter whose capital is two letters is not those two: straße is not STRASSE", """["straße","STRASSE"]""", """["straße","STRASSE"]""", null)]
    [InlineData("a dotted capital I is not an i with a dot above", """["İzmir","i\u0307zmir"]""", """["İzmir","i\u0307zmir"]""", null)]
    public void An_order_is_refused_as_the_cli_refuses_it(string why, string accounts, string order, string? refused)
    {
        var problem = HarnessSettings.OrderProblem(Names(accounts), Names(order));

        var said = problem is null ? null : $"{(problem.Twice ? "twice" : "missing")} {problem.Account}";
        Assert.True(refused == said, $"{why}: {said}");
    }

    // ——— An account removed (D66 §3): no default and no order names it afterwards.

    [Theory]
    [InlineData("a machine default naming it is cleared, another agent's kept", """{"defaults":{"claude-code":"account-1","codex":"account-1"}}""", "claude-code", "account-1", """{"defaults":{"codex":"account-1"},"workspaces":{}}""")]
    [InlineData("a workspace default naming it is cleared, and a workspace left naming none is dropped", """{"workspaces":{"work":{"claude-code":"account-1"},"lab":{"claude-code":"account-2"}}}""", "claude-code", "account-1", """{"defaults":{},"workspaces":{"lab":{"claude-code":"account-2"}}}""")]
    [InlineData("it leaves the machine's order, the rest kept in theirs", """{"rotation":{"claude-code":["account-3","account-1","account-2"]}}""", "claude-code", "account-1", """{"defaults":{},"workspaces":{},"rotation":{"claude-code":["account-3","account-2"]}}""")]
    [InlineData("it leaves a workspace's order, and an order left naming nobody goes", """{"workspaceRotation":{"work":{"claude-code":["account-1"]},"lab":{"claude-code":["account-1","account-2"]}}}""", "claude-code", "account-1", """{"defaults":{},"workspaces":{},"workspaceRotation":{"lab":{"claude-code":["account-2"]}}}""")]
    [InlineData("another agent's account of the same name stays", """{"defaults":{"codex":"account-1"},"rotation":{"codex":["account-1"]}}""", "claude-code", "account-1", """{"defaults":{"codex":"account-1"},"workspaces":{},"rotation":{"codex":["account-1"]}}""")]
    [InlineData("a kept account removed is kept no longer, the rest of its settings kept", """{"rotation":{"claude-code":["account-1","account-2"]},"rotationUse":{"claude-code":{"use":"order","keep":"account-1"}}}""", "claude-code", "account-1", """{"defaults":{},"workspaces":{},"rotation":{"claude-code":["account-2"]},"rotationUse":{"claude-code":{"use":"order"}}}""")]
    [InlineData("an account removed that leaves a workspace's list naming nobody takes its settings too", """{"workspaceRotation":{"work":{"claude-code":["account-1"]}},"workspaceRotationUse":{"work":{"claude-code":{"keep":"account-1","use":"order"}}}}""", "claude-code", "account-1", """{"defaults":{},"workspaces":{}}""")]
    public void An_account_removed_leaves_the_wiring_as_the_cli_leaves_it(string why, string before, string agent, string profile, string after)
    {
        Read(before).WithoutAccount(agent, profile).Save(Wiring);

        var written = Written("defaults", "workspaces", "rotation", "workspaceRotation", "rotationUse", "workspaceRotationUse");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(after), written), $"{why}: {written.ToJsonString()}");
    }

    // ——— 🔴 Each writer keeps the other's sections (D125 §0.1): both write the same file for the same wiring, a key
    // neither knows included, so a screen edit after a terminal's order, or the other way, loses nothing.

    [Theory]
    [InlineData("every section either writes, and a key neither knows, written back as read", """{"later":{"kept":true},"defaults":{"claude-code":"account-1"},"workspaces":{"work":{"claude-code":"account-2"}},"versions":{"claude-code":"2.1.0"},"workspaceVersions":{"work":{"codex":"0.50.0"}},"rotation":{"claude-code":["account-1","account-2","account-3"]},"rotationUse":{"claude-code":{"use":"order","keep":"account-3","early":false,"near":85}},"workspaceRotation":{"work":{"claude-code":["account-2","account-3"]}},"workspaceRotationUse":{"work":{"claude-code":{"use":"goal"}}}}""", null, """{"later":{"kept":true},"defaults":{"claude-code":"account-1"},"workspaces":{"work":{"claude-code":"account-2"}},"versions":{"claude-code":"2.1.0"},"workspaceVersions":{"work":{"codex":"0.50.0"}},"rotation":{"claude-code":["account-1","account-2","account-3"]},"rotationUse":{"claude-code":{"use":"order","keep":"account-3","early":false,"near":85}},"workspaceRotation":{"work":{"claude-code":["account-2","account-3"]}},"workspaceRotationUse":{"work":{"claude-code":{"use":"goal"}}}}""")]
    [InlineData("a default set keeps the orders, their settings, the pins and the key neither knows", """{"later":{"kept":true},"defaults":{"claude-code":"account-1"},"workspaces":{"work":{"claude-code":"account-2"}},"versions":{"claude-code":"2.1.0"},"workspaceVersions":{"work":{"codex":"0.50.0"}},"rotation":{"claude-code":["account-1","account-2","account-3"]},"rotationUse":{"claude-code":{"use":"order","keep":"account-3","early":false,"near":85}},"workspaceRotation":{"work":{"claude-code":["account-2","account-3"]}},"workspaceRotationUse":{"work":{"claude-code":{"use":"goal"}}}}""", "account-9", """{"later":{"kept":true},"defaults":{"claude-code":"account-1","codex":"account-9"},"workspaces":{"work":{"claude-code":"account-2"}},"versions":{"claude-code":"2.1.0"},"workspaceVersions":{"work":{"codex":"0.50.0"}},"rotation":{"claude-code":["account-1","account-2","account-3"]},"rotationUse":{"claude-code":{"use":"order","keep":"account-3","early":false,"near":85}},"workspaceRotation":{"work":{"claude-code":["account-2","account-3"]}},"workspaceRotationUse":{"work":{"claude-code":{"use":"goal"}}}}""")]
    [InlineData("a file that names no order is written with none", """{"defaults":{"claude-code":"account-1"},"workspaces":{},"versions":{},"workspaceVersions":{}}""", null, """{"defaults":{"claude-code":"account-1"},"workspaces":{},"versions":{},"workspaceVersions":{}}""")]
    [InlineData("an empty file is written with the four sections and no order", "{}", null, """{"defaults":{},"workspaces":{},"versions":{},"workspaceVersions":{}}""")]
    [InlineData("a setting's fields go out in one order, whatever order they were read in, each as chosen", """{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"near":85,"early":true,"keep":"account-1","use":"goal"}}}""", null, """{"defaults":{},"workspaces":{},"versions":{},"workspaceVersions":{},"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"use":"goal","keep":"account-1","early":true,"near":85}}}""")]
    [InlineData("a value and a setting this build does not know go back as read, after the ones it knows", """{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"weekly":"pace","near":120,"use":"drain"}}}""", null, """{"defaults":{},"workspaces":{},"versions":{},"workspaceVersions":{},"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"use":"drain","near":120,"weekly":"pace"}}}""")]
    [InlineData("the retired prefer and parallel are not written back, and an entry naming nothing else goes", """{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"prefer":"left","keep":"account-1","parallel":true},"codex":{"prefer":"soonest"}}}""", null, """{"defaults":{},"workspaces":{},"versions":{},"workspaceVersions":{},"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"keep":"account-1"}}}""")]
    [InlineData("settings with no list are written back as read, and read only with one", """{"rotationUse":{"claude-code":{"early":false}},"workspaceRotationUse":{"work":{"codex":{"keep":"account-1"}}}}""", null, """{"defaults":{},"workspaces":{},"versions":{},"workspaceVersions":{},"rotationUse":{"claude-code":{"early":false}},"workspaceRotationUse":{"work":{"codex":{"keep":"account-1"}}}}""")]
    public void Both_twins_write_the_same_file(string why, string before, string? codexDefault, string after)
    {
        var settings = Read(before);
        if (codexDefault is not null) settings = settings.WithDefault("codex", codexDefault);
        settings.Save(Wiring);

        var wanted = JsonNode.Parse(after)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true, NewLine = "\n" }) + "\n";
        Assert.True(wanted == File.ReadAllText(Wiring), $"{why}:\n{File.ReadAllText(Wiring)}");
    }

    // ——— What the twin tables do not hold: the sentence a door says, and the order's resolution beside a pick.

    [Fact]
    public void A_refused_order_says_which_account_and_names_the_ones_there()
    {
        var accounts = new[] { "account-1", "account-2" };

        Assert.Equal(
            "`claude-code` has no account `account-9` on this machine, so an order cannot name it — accounts there: account-1, account-2.",
            new RotationProblem("account-9", Twice: false).Sentence("claude-code", accounts));
        Assert.Equal(
            "`account-1` is named twice — an order names each account once, in the order rotation tries them.",
            new RotationProblem("account-1", Twice: true).Sentence("claude-code", accounts));
    }

    [Fact]
    public void An_order_is_no_default_and_a_default_is_no_order()
    {
        var settings = new HarnessSettings().WithDefault("claude-code", "account-2").WithRotation("claude-code", ["account-1", "account-2"]);

        Assert.Equal("account-2", settings.Resolve("claude-code", null, null));
        Assert.Equal(["account-1", "account-2"], settings.ResolveRotationFrom("claude-code", null).Order);
        Assert.Empty(new HarnessSettings().WithDefault("claude-code", "account-2").ResolveRotationFrom("claude-code", null).Order);
    }

    [Fact]
    public void An_edit_over_wiring_that_could_not_be_read_is_refused_and_keeps_the_order()
    {
        File.WriteAllText(Wiring, "{ not json");

        Assert.Throws<DriverException>(() => HarnessSettings.Load(Wiring).WithRotation("claude-code", ["account-1"]).Save(Wiring));
        Assert.Equal("{ not json", File.ReadAllText(Wiring));
    }
}
