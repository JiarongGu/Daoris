using System.Text.Json;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// The connector's `step_settle` (D318): the working session settles a run's step Daoris handed it, one file per session under the home's
/// sessions folder in the shape the driver's <c>StepSettleAsks</c> reads (held there by <c>AgentStepSettleTests</c>), one at a time.
/// </summary>
public sealed class McpStepSettleToolTests : McpToolsFixture
{
    private string RulesHome => Path.Combine(_root, "rules-home");

    private string Sessions(params string[] file) => DaorisHome.Entry(RulesHome, HomeEntry.Sessions, file);

    private async Task<StepSettleTools> ConnectorAsync(Func<SessionStore, Task<string?>> session)
    {
        var sessions = await SessionStore.OpenAsync(_connection);
        var ledger = new SessionLedger(_quests, sessions, _service, await AskStore.OpenAsync(_connection));
        return new StepSettleTools(intake: new IntakeScope(null, await session(sessions)), ledger: ledger, box: new StepSettleBox(RulesHome));
    }

    /// <summary>🔴 The working session's settle is a file named by its session, with its run, step, outcome, words, evidence and epoch.</summary>
    [Fact]
    public async Task A_working_sessions_settle_is_a_file_named_by_its_session_and_one_waits_at_a_time()
    {
        string asker = null!;
        var tools = await ConnectorAsync(async store =>
            asker = (await store.CreateAsync("q1", "Owner", "claude-code", DateTimeOffset.UtcNow, tree: Path.Combine(_root, "trees", "work"))).Id);

        var said = await tools.SettleAsync("#r1", "config-change", null, "changed the live config", new() { ["version"] = "v42", ["capture"] = "https://c" }, 2);

        using (var kept = JsonDocument.Parse(File.ReadAllText(Sessions(asker + StepSettleBox.AskSuffix))))
        {
            var root = kept.RootElement;
            Assert.Equal((asker, "r1", "config-change", "changed the live config", 2), (root.GetProperty("session").GetString(), root.GetProperty("run").GetString(),
                root.GetProperty("node").GetString(), root.GetProperty("words").GetString(), root.GetProperty("epoch").GetInt32()));
            Assert.Equal("v42", root.GetProperty("evidence").GetProperty("version").GetString());
            Assert.False(root.TryGetProperty("outcome", out _));
            Assert.Contains($"`#{root.GetProperty("id").GetString()}`", said);
        }

        Assert.Contains("one at a time", await tools.SettleAsync("r1", "check-release"));
    }

    /// <summary>🔴 No session, a helper and a settle naming no step are refused in a sentence, and nothing is written.</summary>
    [Fact]
    public async Task step_settle_refuses_what_it_can_judge_and_writes_nothing()
    {
        Assert.Contains("no session", await (await ConnectorAsync(_ => Task.FromResult<string?>(null))).SettleAsync("r1", "n"));
        var helper = await ConnectorAsync(async store =>
        {
            var working = await store.CreateAsync("q1", "Owner", "claude-code", DateTimeOffset.UtcNow);
            return (await store.CreateAsync(null, "Owner", "codex-acp", DateTimeOffset.UtcNow, kind: SessionKind.Chat, serves: working.Id)).Id;
        });
        Assert.Contains("no run's working session", await helper.SettleAsync("r1", "n"));
        var working = await ConnectorAsync(async store => (await store.CreateAsync("q2", "Owner", "claude-code", DateTimeOffset.UtcNow)).Id);
        Assert.Contains("names the run and the step", await working.SettleAsync(" ", "n"));
        Assert.False(Directory.Exists(Sessions()) && Directory.GetFiles(Sessions(), "*" + StepSettleBox.AskSuffix).Length > 0);
    }
}
