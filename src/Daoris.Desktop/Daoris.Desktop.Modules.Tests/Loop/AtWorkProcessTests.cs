using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Microsoft.Extensions.Logging.Abstractions;
using Shenora.Core.Events;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// COWORKVIEW1a rule 5's <i>Process</i> cases on a held composition (the design §3.6, §4.1): a child reads <i>working</i> only while this
/// window watches its process alive, <i>its agent ended</i> from the exit seen, and <i>not known</i> where another Daoris process issued
/// it, each asserted against a real stand-in process the test starts and ends itself.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class AtWorkProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-atwork-proc-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly EventBus _bus = new(NullLogger<EventBus>.Instance);
    private readonly LineageTokens _tokens = new(4243, new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc));
    private readonly StateSlice _groups;
    private readonly StateSlice _sessions;
    private readonly List<Process> _standIns = [];
    private int _woken;

    public AtWorkProcessTests()
    {
        Directory.CreateDirectory(_home);
        var entries = new JsonObject { ["s1"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["session"] = "s1" }) } };
        var records = new JsonObject { ["s1"] = new JsonObject { ["id"] = "s1", ["state"] = "working", ["note"] = "" } };
        _groups = new StateSlice(GroupsStateSlice.Name, _bus, _ => Task.FromResult<IReadOnlyList<SliceJudged>?>([new(GroupsStateSlice.Entries, entries.DeepClone())]), "g") { Keyed = true };
        _sessions = new StateSlice(SessionsStateSlice.Name, _bus, _ => Task.FromResult<IReadOnlyList<SliceJudged>?>([new("default", records.DeepClone())]), "s") { Keyed = true };
    }

    public void Dispose()
    {
        foreach (var standIn in _standIns)
        {
            try
            {
                if (!standIn.HasExited) standIn.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            standIn.Dispose();
        }

        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private Process StandIn()
    {
        var process = Process.Start(new ProcessStartInfo("node", ["-e", "setTimeout(() => {}, 120000)"]) { UseShellExecute = false, CreateNoWindow = true })!;
        _standIns.Add(process);
        return process;
    }

    private AtWorkHeld Compose() =>
        AtWorkRead.Compose("s1", _groups, _sessions, record => ChildLineage.Reading(_home, record.Id, record.Kind, _tokens), session => ChildLineage.PathOf(_home, session));

    private string StateOfChild() => Compose().Answer!.Subagents.Children.Single().State;

    private async Task<AtWorkWatch> HeldAsync()
    {
        await _groups.JudgeAsync();
        await _sessions.JudgeAsync();
        var watch = new AtWorkWatch(() => _woken++, _groups, _sessions, _tokens);
        watch.Register("q", Assert.IsType<AtWorkDependencies>(Compose().Dependencies));
        return watch;
    }

    private void Lines(string proc, string issuer)
    {
        ChildLineage.Start(_home, "s1", proc, issuer, DateTimeOffset.UnixEpoch);
        ChildLineage.Append(_home, "s1", new JsonObject { ["by"] = "hook", ["event"] = "started", ["agent"] = "x1", ["session"] = "root-1", ["proc"] = proc }.ToJsonString());
    }

    private static async Task UntilAsync(Func<bool> done)
    {
        for (var i = 0; i < 300 && !done(); i++) await Task.Delay(50);
        Assert.True(done());
    }

    [Fact]
    public async Task A_child_of_a_process_this_window_watches_reads_working_while_it_lives_and_its_agent_ended_once_its_exit_is_seen()
    {
        var token = _tokens.Issue();
        var process = StandIn();
        _tokens.Hand(token, process);
        Lines(token, _tokens.Issuer);
        await _groups.JudgeAsync();
        await _sessions.JudgeAsync();
        Assert.Equal("working", StateOfChild());
        var watch = await HeldAsync();

        process.Kill(entireProcessTree: true);
        await UntilAsync(() => _tokens.Reading(token) == TokenReading.Gone);

        Assert.True(_woken >= 1, "the held composition was not woken by the token's leaving");
        Assert.Equal("agentEnded", StateOfChild());
        Assert.Equal(0, watch.Holding);
    }

    [Fact]
    public async Task A_child_another_daoris_process_issued_reads_not_known_while_its_process_lives_and_after_it_exits()
    {
        var process = StandIn();
        Lines("foreign-proc", "another-daoris");
        await _groups.JudgeAsync();
        await _sessions.JudgeAsync();
        Assert.Equal("uncertain", StateOfChild());

        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync();

        Assert.Equal("uncertain", StateOfChild());
    }
}
