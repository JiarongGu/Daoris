using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CONTEXTONE1d (D230 point 3, the context-one design part D): a session Daoris runs on a Claude door holds its connector's tools from
/// its first request, never as names behind a search step, whatever its kind. One switch, the adapter's own (HELP5), on every spawn; a
/// door whose harness has no such switch, Codex's among them, is handed nothing.
/// </summary>
public sealed class ToolsAtTheStartTests
{
    private const string Variable = "ENABLE_TOOL_SEARCH";

    private static readonly IReadOnlyList<string> Command = ["node", "stand-in.mjs"];

    private static SessionTarget Target() => new(
        QuestId: "abc123",
        Title: "Fix the flaky gate",
        Body: "It fails one run in five; the log is attached.",
        Asker: "Platform",
        Repository: "Game",
        Root: "D:/fam/Game",
        ServiceUrl: "http://localhost:5177");

    /// <summary>The census of doors: every door this build has whose program is Claude Code names the switch, and no other door does.</summary>
    [Fact]
    public void No_Claude_door_defers_the_connectors_tools()
    {
        var adapters = ProviderRegistry.Built();
        var claude = new List<string>();
        foreach (var name in adapters.Names)
        {
            var adapter = adapters.Resolve(name);
            if (adapter.Toolchain?.Product == "Claude Code")
            {
                claude.Add(name);
                Assert.Equal(new Dictionary<string, string> { [Variable] = "false" }, adapter.ToolsUpFront);
            }
            else
            {
                Assert.Empty(adapter.ToolsUpFront);
            }
        }

        Assert.Equal(["claude-code", "claude-code-acp"], claude);
    }

    /// <summary>
    /// Every kind Daoris drives is prepared by <c>Driver.Prepare</c>: a start (a quest's, a hand-over's, a helper's, an intake's, an
    /// opinion's, a partner's, a brainstorm's) and a go-on on its kept conversation alike. A Claude door's spawn carries the switch; any
    /// other door's is left as its harness made it.
    /// </summary>
    [Theory]
    [InlineData("claude-code", true)]
    [InlineData("claude-code-acp", true)]
    [InlineData("codex-acp", false)]
    [InlineData("dsh", false)]
    [InlineData("stub", false)]
    [InlineData("acp-stub", false)]
    public void Every_driven_spawn_on_a_Claude_door_loads_its_tools_at_the_start(string name, bool loads)
    {
        var adapter = ProviderRegistry.Built().Resolve(name);
        using var service = new ServiceClient("http://ledger.test", null);
        var driver = Driver(service, name);
        var target = Target();
        var own = Said(adapter.Prepare(target, Command));

        var started = driver.Prepare(adapter, target, new HarnessSelection(null)).Info;
        var kept = adapter.PrepareResume(target, Command, "kept-conversation") ?? adapter.Prepare(target, Command);
        var resumed = driver.Prepare(adapter, target, new HarnessSelection(null), kept).Info;

        foreach (var info in new[] { started, resumed })
        {
            Assert.Equal(loads ? "false" : own, Said(info));
        }
    }

    /// <summary>The switch is set after an account's own variables, so an account cannot put Daoris's sessions' tools back behind a search.</summary>
    [Fact]
    public void An_accounts_own_variables_do_not_defer_the_tools_again()
    {
        var adapter = ProviderRegistry.Built().Resolve("claude-code-acp");
        using var service = new ServiceClient("http://ledger.test", null);
        var account = new HarnessSelection(null, Environment: new Dictionary<string, string> { [Variable] = "true" });

        var info = Driver(service, "claude-code-acp").Prepare(adapter, Target(), account).Info;

        Assert.Equal("false", Said(info));
    }

    private static Daoris.Driver.Driver Driver(ServiceClient service, string name) => new(
        service,
        DriverConfig.Empty with { Commands = new Dictionary<string, IReadOnlyList<string>> { [name] = Command } },
        ProviderRegistry.Built(),
        Path.GetFullPath("_fixtures/toolsatstart-unused"));

    private static string? Said(ProcessStartInfo info) => info.Environment.TryGetValue(Variable, out var value) ? value : null;
}
