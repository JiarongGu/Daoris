using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Contracts.Sessions;
using Daoris.Driver;
using Daoris.Ports.Sessions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The fold in shadow, from the driver's side: the leases its session markers prove (each with its generation and whether its owner
/// runs), the generation only the home lease's holder begins, the tick's audit, the generation every request carries, and the fold's
/// state laid on a row only where it differs from the stored status, in the list's JSON and its line.
/// </summary>
public sealed class SessionFoldsTests : IDisposable
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.Parse("2026-11-28T08:00:00Z");
    private readonly string _markers = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "fold-markers", Guid.NewGuid().ToString("N")[..8]);

    public SessionFoldsTests() => Directory.CreateDirectory(_markers);

    public void Dispose() => GitSeed.Discard(_markers);

    private void Mark(string session, string line) => File.WriteAllText(Path.Combine(_markers, session + ".pid"), line);

    [Fact]
    public void A_lease_is_each_marker_whose_process_runs_live_while_its_owner_does_so_two_drivers_on_one_home_void_neither()
    {
        using var self = Process.GetCurrentProcess();
        var started = self.StartTime.ToUniversalTime().Ticks;
        // This driver's own process, and one a terminal's driver beside the holder started in an earlier generation: both owners run.
        Mark("s-here", new SessionMarker(self.Id, started, 3, self.Id, started).Line());
        Mark("s-beside", new SessionMarker(self.Id, started, 2, self.Id, started).Line());
        // A process the last start left running: its owner is gone, so its lease is void, never adopted.
        Mark("s-survived", new SessionMarker(self.Id, started, 2, int.MaxValue - 1, started).Line());
        // A build before the generations wrote two fields: a process with no generation and no owner.
        Mark("s-older", $"{self.Id} {started}");
        Mark("s-gone", new SessionMarker(int.MaxValue - 1, started, 3, self.Id, started).Line());
        Mark("s-reused", new SessionMarker(self.Id, started - TimeSpan.TicksPerHour, 3, self.Id, started).Line());
        Mark("s-opening", new SessionMarker(self.Id, started, 0, 0, 0, Opening: true).Line());

        var leases = SessionFolds.Leases(_markers).ToDictionary(lease => lease.Session);

        Assert.Equal(["s-beside", "s-here", "s-older", "s-survived"], leases.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(new LeaseRequest("s-here", 3, true), leases["s-here"]);
        Assert.Equal(new LeaseRequest("s-beside", 2, true), leases["s-beside"]);
        Assert.Equal(new LeaseRequest("s-survived", 2, false), leases["s-survived"]);
        Assert.Equal(new LeaseRequest("s-older", 0, false), leases["s-older"]);
        Assert.Empty(SessionFolds.Leases(Path.Combine(_markers, "none")));
        Assert.True(SessionProcesses.MarkerAlive(Path.Combine(_markers, "s-here.pid")));
        Assert.False(SessionProcesses.MarkerAlive(Path.Combine(_markers, "s-opening.pid")));
    }

    [Fact]
    public async Task Only_the_home_lease_s_holder_begins_a_generation_and_then_writes_in_it()
    {
        var doors = new Doors(new ServiceReply(200, """{"generation":4,"started":"2026-11-28T08:00:00.0000000+00:00"}"""));
        using var service = new ServiceClient(doors);
        var said = new List<string>();

        Assert.True(await SessionFolds.BeginAsync(service, T0, holds: false, said.Add, CancellationToken.None));
        Assert.Empty(doors.Asked);

        Assert.True(await SessionFolds.BeginAsync(service, T0, holds: true, said.Add, CancellationToken.None));
        Assert.Equal(4, WritingGeneration.Current);
        var (verb, path, body) = Assert.Single(doors.Asked);
        Assert.Equal((ServiceVerb.Post, BeginGeneration.Path), (verb, path));
        Assert.Equal(T0, DateTimeOffset.Parse(JsonNode.Parse(body!)!["started"]!.GetValue<string>()));

        using var refusing = new ServiceClient(new Doors(new ServiceReply(404, "no route")));
        Assert.False(await SessionFolds.BeginAsync(refusing, T0, holds: true, said.Add, CancellationToken.None));
        Assert.Contains("404", Assert.Single(said));
    }

    [Fact]
    public async Task A_driver_beside_the_holder_writes_in_the_generation_the_audit_answers_and_every_request_carries_it()
    {
        using var self = Process.GetCurrentProcess();
        Mark("s1", new SessionMarker(self.Id, self.StartTime.ToUniversalTime().Ticks, 6, self.Id, self.StartTime.ToUniversalTime().Ticks).Line());
        var answer = new FoldReadingResponse(6, T0.ToString("O"), 1, 0.5, [], []);
        var doors = new Doors(new ServiceReply(200, JsonSerializer.Serialize(answer, JsonSerializerOptions.Web)));
        using var service = new ServiceClient(doors);

        await SessionFolds.AuditAsync(service, _markers, holds: false, CancellationToken.None);

        Assert.Equal(6, WritingGeneration.Current);
        var lease = Assert.Single(JsonNode.Parse(Assert.Single(doors.Asked).Body!)!["leases"]!.AsArray())!;
        Assert.Equal(("s1", 6, true), (lease["session"]!.GetValue<string>(), lease["generation"]!.GetValue<long>(), lease["ownerRuns"]!.GetValue<bool>()));
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:1/");
        WritingGeneration.Dress(request);
        Assert.Equal("6", Assert.Single(request.Headers.GetValues(SessionLifeHeaders.Generation)));
    }

    [Fact]
    public void A_session_s_terminal_writes_in_the_generation_its_session_s_marker_names()
    {
        using var self = Process.GetCurrentProcess();
        Mark("s-term", new SessionMarker(self.Id, self.StartTime.ToUniversalTime().Ticks, 9, self.Id, self.StartTime.ToUniversalTime().Ticks).Line());

        WritingGeneration.FromMarker(_markers, "s-term");

        Assert.Equal(9, WritingGeneration.Current);
    }

    [Fact]
    public async Task The_list_reads_the_last_audit_and_says_a_service_that_does_not_answer_rather_than_passing_it_over()
    {
        var answer = new FoldReadingResponse(3, T0.ToString("O"), 2, 1.5,
        [
            new FoldDifferenceResponse("s2", "working", "needs-you", null, null, null, "2026-11-28T09:00:00.0000000+00:00", "turn-without-lease",
                new SessionFactResponse(4, "turn-began", "advance", null, 3, "2026-11-28T09:00:00.0000000+00:00", "none")),
        ], []);
        var doors = new Doors(new ServiceReply(200, JsonSerializer.Serialize(answer, JsonSerializerOptions.Web)));
        using var service = new ServiceClient(doors);
        var said = new List<string>();

        var folds = await SessionFolds.ReadAsync(service, said.Add, CancellationToken.None);

        var fold = Assert.Single(folds);
        Assert.Equal(("s2", new FoldShown("needs-you", null, null, null, "2026-11-28T09:00:00.0000000+00:00", "turn-without-lease")), (fold.Key, fold.Value));
        Assert.Equal((ServiceVerb.Get, ReadFold.Path), (Assert.Single(doors.Asked).Verb, doors.Asked[0].Path));
        Assert.Empty(said);

        using var older = new ServiceClient(new Doors(new ServiceReply(404, "no route")));
        Assert.Empty(await SessionFolds.ReadAsync(older, said.Add, CancellationToken.None));
        Assert.Contains("404", Assert.Single(said));
    }

    [Fact]
    public void A_row_carries_the_fold_only_where_it_differs_in_the_list_s_json_and_its_line_said_in_shadow()
    {
        JsonObject Record(string id) => new()
        {
            ["id"] = id, ["repository"] = "engine", ["adapter"] = "claude-code", ["state"] = "working", ["kind"] = "driven", ["quest"] = id,
            ["line"] = id, ["created"] = T0.ToString("O"), ["updated"] = T0.ToString("O"), ["said"] = new JsonArray(),
        };
        var look = SessionLook.From(new JsonArray(Record("s1"), Record("s2")).ToJsonString(), [], [], _ => 0) with
        {
            Folds = new Dictionary<string, FoldShown> { ["s2"] = new("waiting", null, "step", "r1", T0.AddHours(1).ToString("O"), null) },
        };

        var rows = SessionGroups.Read(look);

        Assert.Null(rows.Single(row => row.Session == "s1").Fold);
        var differs = rows.Single(row => row.Session == "s2");
        Assert.Equal("waiting", differs.Fold?.State);

        var json = JsonNode.Parse(SessionsJson.Json(rows))!["sessions"]!.AsArray();
        Assert.Null(json.Single(row => row!["session"]!.GetValue<string>() == "s1")!["fold"]);
        var fold = json.Single(row => row!["session"]!.GetValue<string>() == "s2")!["fold"]!;
        Assert.Equal(("waiting", "step", "r1"), (fold["state"]!.GetValue<string>(), fold["holder"]!.GetValue<string>(), fold["subject"]!.GetValue<string>()));

        var record = look.Records.Single(each => each.Id == "s2");
        Assert.Contains(SessionsListing.Facts(differs, record), said => said.StartsWith("in shadow: waiting, held by step r1 since", StringComparison.Ordinal));
        Assert.DoesNotContain(SessionsListing.Facts(rows.Single(row => row.Session == "s1"), record), said => said.Contains("in shadow", StringComparison.Ordinal));
    }

    /// <summary>The host as the client knocks on it: each request kept, each answer the one handed.</summary>
    private sealed class Doors(ServiceReply reply) : IServiceDoors
    {
        public List<(ServiceVerb Verb, string Path, string? Body)> Asked { get; } = [];

        public string BaseUrl => "http://127.0.0.1:1";

        public Task<string> GetAsync(string path, CancellationToken ct) => throw new NotSupportedException();

        public Task<string> PostAsync(string path, string json, CancellationToken ct) => throw new NotSupportedException();

        public Task<ServiceReply> SendAsync(ServiceVerb verb, string path, string? json, CancellationToken ct)
        {
            Asked.Add((verb, path, json));
            return Task.FromResult(reply);
        }

        public void Dispose()
        {
        }
    }
}
