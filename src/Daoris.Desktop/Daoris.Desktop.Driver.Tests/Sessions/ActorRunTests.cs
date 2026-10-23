using System.Reflection;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

public sealed class ActorRunTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "modelrole1b-" + Guid.NewGuid().ToString("N"));
    public ActorRunTests() => Directory.CreateDirectory(_home);
    public void Dispose() => GitSeed.Discard(_home);

    [Fact]
    public void Reports_preserve_unsplit_mixed_order_a_fourth_and_a_ninth_model_and_bounded_effort_changes()
    {
        var tokens = TurnModels.Of(null, ["z-model", "a-model"], "message", null, null, "quota");
        Assert.Equal(TurnModels.Mixed, tokens.Model);
        Assert.Equal(["z-model", "a-model"], tokens.Reported);
        var reports = new ActorReports();
        Assert.True(reports.Report(tokens.Reported));
        reports.Report(["third", "fourth"]);
        Assert.Equal("fourth", reports.Snapshot!.Models[3]);
        reports.Report(["fifth", "sixth", "seventh", "eighth", "ninth", "ninth"]);
        Assert.Equal(8, reports.Snapshot!.Models.Count);
        Assert.Equal(1, reports.Snapshot.More);
        var now = DateTimeOffset.Parse("2026-10-24T12:00:00Z");
        for (var i = 0; i < 10; i++) reports.Report(["z-model"], new(now.AddMinutes(i), "z-model", i.ToString()));
        Assert.Equal(8, reports.Snapshot.Entries.Count);
        Assert.Equal(2, reports.Snapshot.EntriesMore);
        Assert.Equal("9", reports.Snapshot.Entries[^1].Effort);
        Assert.Null(new ActorReports().Snapshot);
    }

    [Fact]
    public void Names_from_a_quota_with_no_reconcilable_counts_still_report_what_ran()
    {
        using var raw = JsonDocument.Parse("""[{"model":"z-model"},{"model":"a-model"}]""");
        Assert.Null(TurnModels.Read(raw.RootElement));
        var turn = TurnModels.Of(null, [], "update", null, null, "quota", TurnModels.Names(raw.RootElement));
        Assert.Equal(["z-model", "a-model"], turn.Reported);
        Assert.Equal(TurnModels.Unknown, turn.Model);
    }

    [Fact]
    public void Explicit_default_absent_model_and_uncarried_effort_survive_serialization_and_each_native_prepare()
    {
        var before = ActorAsked.Before("claude-code");
        var explicitDefault = before with { Model = new("entry", "default", true), Effort = new("entry", "high", false) };
        var roundtrip = JsonSerializer.Deserialize<ActorAsked>(JsonSerializer.Serialize(explicitDefault, ActorRun.Json), ActorRun.Json)!;
        Assert.Equal(explicitDefault, roundtrip);
        Assert.Equal("high", roundtrip.Effort.Value);
        var adapter = new ClaudeCodeAdapter();
        var target = new SessionTarget("q", "title", "body", "asker", "Owner", _home, "http://stand-in") { Asked = roundtrip };
        var chat = new ChatTarget("Owner", _home, "http://stand-in") { Asked = roundtrip, Resume = "conv-1" };
        foreach (var info in new[] { adapter.Prepare(target, null), adapter.PrepareResume(target, null, "conv-1", "go on"), adapter.PrepareChat(chat, null) })
        {
            var arguments = info.ArgumentList.ToList();
            Assert.Equal("default", arguments[arguments.IndexOf("--model") + 1]);
            Assert.DoesNotContain("--effort", arguments);
        }
        Assert.DoesNotContain("--model", adapter.Prepare(target with { Asked = before }, null).ArgumentList);
    }

    [Fact]
    public async Task The_watch_keeps_reports_in_order_drains_them_and_logs_the_actor_without_inventing_a_model()
    {
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        var kept = new List<ActorRan>();
        using var log = new MachineLog(_home, "driver");
        await using (var watch = new ActorRun(events, "s1", "build", null, ran => { kept.Add(ran); return Task.CompletedTask; }, log))
        {
            events.Keep("s1", new SessionEvent { Kind = SessionEventKind.Turn }, null);
            Assert.Empty(kept);
            events.Keep("s1", new SessionEvent { Kind = SessionEventKind.Turn, Models = TurnModels.Of(null, ["reported"], "message", null, null, "quota") }, null);
        }
        Assert.Equal("reported", Assert.Single(kept).Models.Single());
        log.Dispose();
        var line = Directory.GetFiles(DaorisHome.Entry(_home, HomeEntry.Logs), "*.jsonl").SelectMany(File.ReadAllLines).Single();
        Assert.Contains("actor.ran", line);
        Assert.Contains("build", line);
    }

    /// <summary>Row b reports alone: a service that refuses the provenance write costs the record it, never the start or the press.</summary>
    [Fact]
    public async Task A_refused_provenance_write_is_logged_and_never_thrown()
    {
        using var http = new HttpClient(new Refusing());
        using var service = new ServiceClient("http://stand-in", key: null, http);
        using (var log = new MachineLog(_home, "driver"))
        {
            Assert.False(await service.TryKeepActorAsync("s1", log, "before", ActorAsked.Before("claude-code")));
            Assert.False(await service.TryKeepActorAsync("s1", null, changed: new Dictionary<string, string> { ["model"] = "sonnet" }));
        }
        var line = Directory.GetFiles(DaorisHome.Entry(_home, HomeEntry.Logs), "*.jsonl").SelectMany(File.ReadAllLines).Single();
        Assert.Contains("actor.keep-failed", line);
        await Assert.ThrowsAsync<DriverException>(() => service.KeepActorAsync("s1", "before"));
    }

    private sealed class Refusing : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError) { Content = new StringContent("{\"error\":\"refused\"}") });
    }

    [Fact]
    public void The_thirteen_start_table_names_an_actor_and_the_two_spawn_seams_require_it()
    {
        var rows = new (int Start, string File, string Actor)[]
        {
            (1, "Driver.cs", "build"), (2, "DriverContinuations.cs", "build"), (3, "Driver.cs", "build"),
            (4, "ChatGoOn.cs", "chat"), (5, "DriverIntakes.cs", "intake"), (6, "DriverOpinions.cs", "opinion"),
            (7, "DriverHelpers.cs", "helper"), (8, "DriverPartners.cs", "partner"), (9, "DriverBrainstorms.cs", "brainstorm"),
            (10, "ChatRunner.cs", "chat"), (11, "ChatRunner.cs", "ask-daoris"), (12, "ChatGoOn.cs", "ask-daoris"), (13, "LimitMove.cs", "ask-daoris"),
        };
        Assert.Equal(13, rows.Select(row => row.Start).Distinct().Count());
        foreach (var row in rows) Assert.Contains('"' + row.Actor + '"', File.ReadAllText(SourceFiles.Driver(row.File)));
        foreach (var type in new[] { typeof(Daoris.Driver.Driver), typeof(ChatRunner) })
        {
            var method = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).Single(method => method.Name == (type == typeof(ChatRunner) ? "RunAsync" : "HoldAsync"));
            Assert.Equal("actor", method.GetParameters()[0].Name);
            Assert.False(method.GetParameters()[0].IsOptional);
        }
    }
}
