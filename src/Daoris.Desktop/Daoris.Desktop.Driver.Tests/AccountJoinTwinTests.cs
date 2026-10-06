using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An account put into a scope's list, and where an account runs (ACCT1, D125's ACCT1 note; D130 §3.1): at a new account's
/// sign-in's end both doors say which lists and defaults hold it and can add it to a list in the same step, and an account
/// that no list and no default holds says so. The driver's half of a TWIN with the CLI's <c>rotation.ts</c>
/// (<c>joinProblem</c>, <c>withJoined</c>, <c>placesOf</c>), whose <c>account-join.test.ts</c> holds the same tables, row for
/// row and in the same order, and parses these theories to hold them to its own, cell for cell.
/// </summary>
/// <remarks>
/// <para>A scope is a workspace's name, or <c>null</c> for this machine's list. An <c>after</c> is the sections a save
/// writes, compared as JSON, or <c>refused borrows</c> where the workspace has no list or default of its own and so takes
/// this machine's. Places are a JSON list of <c>{ workspace, list, default }</c>, the machine first.</para>
/// <para>🔴 <b>Keep each row on one line, its cells literals</b>: the CLI's test reads them.</para>
/// </remarks>
public sealed class AccountJoinTwinTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-join-twin-" + Guid.NewGuid().ToString("N")[..8]);

    public AccountJoinTwinTests() => Directory.CreateDirectory(_home);

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

    private JsonObject Written()
    {
        var root = JsonNode.Parse(File.ReadAllText(Wiring))!.AsObject();
        var picked = new JsonObject();
        foreach (var section in new[] { "defaults", "workspaces", "rotation", "workspaceRotation", "rotationUse", "workspaceRotationUse" })
        {
            if (root[section] is { } node) picked[section] = node.DeepClone();
        }

        return picked;
    }

    // ——— Joining (D130 §3.1): a scope's own list gains the account at its end; a scope with a default and no list begins one
    // at its default; this machine naming nobody begins one with it; a workspace naming neither takes this machine's list,
    // so it is refused there, naming this machine's.

    [Theory]
    [InlineData("this machine's list gains it at its end", """{"rotation":{"claude-code":["account-1"]}}""", "claude-code", "account-2", null, """{"defaults":{},"workspaces":{},"rotation":{"claude-code":["account-1","account-2"]}}""")]
    [InlineData("a list that holds it already is as it was", """{"rotation":{"claude-code":["account-2","account-1"]}}""", "claude-code", "account-2", null, """{"defaults":{},"workspaces":{},"rotation":{"claude-code":["account-2","account-1"]}}""")]
    [InlineData("a workspace's own list gains it, this machine's kept", """{"rotation":{"claude-code":["account-1"]},"workspaceRotation":{"work":{"claude-code":["account-1"]}}}""", "claude-code", "account-2", "work", """{"defaults":{},"workspaces":{},"rotation":{"claude-code":["account-1"]},"workspaceRotation":{"work":{"claude-code":["account-1","account-2"]}}}""")]
    [InlineData("this machine with a default and no list begins a list at its default", """{"defaults":{"claude-code":"account-1"}}""", "claude-code", "account-2", null, """{"defaults":{"claude-code":"account-1"},"workspaces":{},"rotation":{"claude-code":["account-1","account-2"]}}""")]
    [InlineData("this machine naming nobody begins its list with it", "{}", "claude-code", "account-2", null, """{"defaults":{},"workspaces":{},"rotation":{"claude-code":["account-2"]}}""")]
    [InlineData("a default that is the account itself is a list of it alone", """{"defaults":{"claude-code":"account-2"}}""", "claude-code", "account-2", null, """{"defaults":{"claude-code":"account-2"},"workspaces":{},"rotation":{"claude-code":["account-2"]}}""")]
    [InlineData("a workspace with its own default and no list begins a list at its default", """{"workspaces":{"work":{"claude-code":"account-1"}}}""", "claude-code", "account-2", "work", """{"defaults":{},"workspaces":{"work":{"claude-code":"account-1"}},"workspaceRotation":{"work":{"claude-code":["account-1","account-2"]}}}""")]
    [InlineData("a workspace naming neither takes this machine's list, so joining it is refused", """{"rotation":{"claude-code":["account-1"]}}""", "claude-code", "account-2", "forge", "refused borrows")]
    [InlineData("a workspace naming neither on a machine naming nobody is refused too", "{}", "claude-code", "account-2", "forge", "refused borrows")]
    [InlineData("a workspace naming only another agent's account takes this machine's list", """{"workspaces":{"work":{"codex":"account-1"}}}""", "claude-code", "account-2", "work", "refused borrows")]
    [InlineData("another agent's list is not this one's", """{"rotation":{"codex":["account-1"]}}""", "claude-code", "account-2", null, """{"defaults":{},"workspaces":{},"rotation":{"codex":["account-1"],"claude-code":["account-2"]}}""")]
    [InlineData("a list's settings stay with it", """{"workspaceRotation":{"work":{"claude-code":["account-1"]}},"workspaceRotationUse":{"work":{"claude-code":{"use":"order"}}}}""", "claude-code", "account-2", "work", """{"defaults":{},"workspaces":{},"workspaceRotation":{"work":{"claude-code":["account-1","account-2"]}},"workspaceRotationUse":{"work":{"claude-code":{"use":"order"}}}}""")]
    public void An_account_joins_a_list_as_the_cli_joins_it(string why, string before, string agent, string account, string? workspace, string after)
    {
        var settings = Read(before);

        if (settings.JoinProblemOf(agent, workspace) is not null)
        {
            Assert.True(after == "refused borrows", $"{why}: refused");
            return;
        }

        settings.WithJoined(agent, account, workspace).Save(Wiring);
        var written = Written();
        Assert.True(after != "refused borrows" && JsonNode.DeepEquals(JsonNode.Parse(after), written), $"{why}: {written.ToJsonString()}");
    }

    // ——— Where an account runs: each scope whose own list holds it or whose own default names it, this machine first and then
    // each workspace by name; none is an account no start runs on.

    [Theory]
    [InlineData("in no list and no default is nowhere", """{"rotation":{"claude-code":["account-1"]}}""", "claude-code", "account-2", "[]")]
    [InlineData("this machine's list", """{"rotation":{"claude-code":["account-1","account-2"]}}""", "claude-code", "account-2", """[{"workspace":null,"list":true,"default":false}]""")]
    [InlineData("this machine's default and its list", """{"defaults":{"claude-code":"account-1"},"rotation":{"claude-code":["account-1"]}}""", "claude-code", "account-1", """[{"workspace":null,"list":true,"default":true}]""")]
    [InlineData("this machine's default with no list", """{"defaults":{"claude-code":"account-1"}}""", "claude-code", "account-1", """[{"workspace":null,"list":false,"default":true}]""")]
    [InlineData("a workspace's own default with no list", """{"workspaces":{"work":{"claude-code":"account-1"}}}""", "claude-code", "account-1", """[{"workspace":"work","list":false,"default":true}]""")]
    [InlineData("this machine first, then each workspace by name", """{"rotation":{"claude-code":["account-1"]},"workspaceRotation":{"zeta":{"claude-code":["account-1"]},"alpha":{"claude-code":["account-2","account-1"]}}}""", "claude-code", "account-1", """[{"workspace":null,"list":true,"default":false},{"workspace":"alpha","list":true,"default":false},{"workspace":"zeta","list":true,"default":false}]""")]
    [InlineData("another agent's places are not this one's", """{"defaults":{"codex":"account-1"},"rotation":{"codex":["account-1"]}}""", "claude-code", "account-1", "[]")]
    [InlineData("an account compares exactly, as the wiring compares it", """{"rotation":{"claude-code":["account-1"]}}""", "claude-code", "Account-1", "[]")]
    [InlineData("a letter whose capital is two letters is not those two: straße is not STRASSE", """{"workspaces":{"straße":{"claude-code":"account-1"}},"workspaceRotation":{"STRASSE":{"claude-code":["account-1"]}}}""", "claude-code", "account-1", """[{"workspace":"STRASSE","list":true,"default":false},{"workspace":"straße","list":false,"default":true}]""")]
    [InlineData("a dotted capital I is not an i with a dot above", """{"workspaces":{"İzmir":{"claude-code":"account-1"}},"workspaceRotation":{"i\u0307zmir":{"claude-code":["account-1"]}}}""", "claude-code", "account-1", """[{"workspace":"i\u0307zmir","list":true,"default":false},{"workspace":"İzmir","list":false,"default":true}]""")]
    public void An_account_s_places_read_as_the_cli_reads_them(string why, string wiring, string agent, string account, string places)
    {
        var read = Read(wiring).PlacesOf(agent, account);

        var said = new JsonArray([.. read.Select(place => (JsonNode)new JsonObject
        {
            ["workspace"] = place.Workspace, ["list"] = place.List, ["default"] = place.Default,
        })]);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(places), said), $"{why}: {said.ToJsonString()}");
    }

    [Fact]
    public void A_refused_join_names_this_machine_s_list_and_the_workspace_s_own()
    {
        var problem = Read("{}").JoinProblemOf("claude-code", "forge");

        var sentence = problem!.Sentence("claude-code");
        Assert.Contains("`forge` names no `claude-code` account or list of its own", sentence);
        Assert.Contains("this machine's list", sentence);
        Assert.Contains("daoris agent profile order claude-code <account>… --workspace forge", sentence);
    }

    /// <summary>
    /// The refusal word for word as the CLI's <c>joinRefusal</c> says it (ACCTQUOTE1b): the workspace in its command spelled for
    /// any shell, in double quotes where a space needs them and a placeholder where no spelling holds, and as itself in the
    /// sentence around it. Each sentence is the CLI's own output for that workspace.
    /// </summary>
    [Theory]
    [InlineData("a plain workspace is itself", "forge", "`forge` names no `claude-code` account or list of its own, so its starts take this machine's list — join this machine's list, or give `forge` a list of its own first (`daoris agent profile order claude-code <account>… --workspace forge`).")]
    [InlineData("a space is kept whole in double quotes", "my team", "`my team` names no `claude-code` account or list of its own, so its starts take this machine's list — join this machine's list, or give `my team` a list of its own first (`daoris agent profile order claude-code <account>… --workspace \"my team\"`).")]
    [InlineData("an ampersand has no spelling, so its placeholder", "R&D", "`R&D` names no `claude-code` account or list of its own, so its starts take this machine's list — join this machine's list, or give `R&D` a list of its own first (`daoris agent profile order claude-code <account>… --workspace <workspace>`).")]
    public void A_refused_join_is_said_as_the_cli_says_it(string why, string workspace, string sentence)
    {
        var problem = Read("{}").JoinProblemOf("claude-code", workspace);

        Assert.True(problem!.Sentence("claude-code") == sentence, $"{why}: {problem.Sentence("claude-code")}");
    }
}
