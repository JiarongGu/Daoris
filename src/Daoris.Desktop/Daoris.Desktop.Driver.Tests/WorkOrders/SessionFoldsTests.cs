using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Contracts.Sessions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The fold in shadow, from the driver's side: the leases its session markers prove, the generation each start begins, and the
/// fold's state, holder and since laid on a row only where it differs from the stored status, in the list's JSON and its line.
/// </summary>
public sealed class SessionFoldsTests : IDisposable
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.Parse("2026-11-28T08:00:00Z");
    private readonly string _markers = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "fold-markers", Guid.NewGuid().ToString("N")[..8]);

    public SessionFoldsTests() => Directory.CreateDirectory(_markers);

    public void Dispose() => GitSeed.Discard(_markers);

    [Fact]
    public void A_lease_is_each_marker_whose_process_is_alive_as_the_one_it_marked()
    {
        using var self = Process.GetCurrentProcess();
        var started = self.StartTime.ToUniversalTime().Ticks;
        File.WriteAllText(Path.Combine(_markers, "s-alive.pid"), $"{self.Id} {started}");
        File.WriteAllText(Path.Combine(_markers, "s-gone.pid"), $"{int.MaxValue - 1} {started}");
        File.WriteAllText(Path.Combine(_markers, "s-reused.pid"), $"{self.Id} {started - TimeSpan.TicksPerHour}");
        // An opener's mark is no process of the session's.
        File.WriteAllText(Path.Combine(_markers, "s-opening.pid"), $"{self.Id} {started} opening");

        var leases = SessionFolds.Leases(_markers);

        var lease = Assert.Single(leases);
        Assert.Equal("s-alive", lease.Session);
        Assert.Equal(new DateTimeOffset(started, TimeSpan.Zero), DateTimeOffset.Parse(lease.Started));
        Assert.Empty(SessionFolds.Leases(Path.Combine(_markers, "none")));
    }

    [Fact]
    public async Task A_start_begins_its_generation_with_the_application_s_start()
    {
        var doors = new Doors(new ServiceReply(200, """{"generation":4,"started":"2026-11-28T08:00:00.0000000+00:00"}"""));
        using var service = new ServiceClient(doors);

        Assert.True(await SessionFolds.BeginAsync(service, T0, CancellationToken.None));

        var (verb, path, body) = Assert.Single(doors.Asked);
        Assert.Equal((ServiceVerb.Post, BeginGeneration.Path), (verb, path));
        Assert.Equal(T0, DateTimeOffset.Parse(JsonNode.Parse(body!)!["started"]!.GetValue<string>()));
    }

    [Fact]
    public async Task The_fold_s_differences_are_read_with_the_markers_leases_and_none_where_the_service_does_not_answer()
    {
        using var self = Process.GetCurrentProcess();
        File.WriteAllText(Path.Combine(_markers, "s1.pid"), $"{self.Id} {self.StartTime.ToUniversalTime().Ticks}");
        var answer = new FoldSessionsResponse(3, 2, 1.5,
        [
            new FoldDifferenceResponse("s2", "working", "needs-you", null, null, "2026-11-28T09:00:00.0000000+00:00", "its turn began and no process of it is alive", []),
        ]);
        var doors = new Doors(new ServiceReply(200, JsonSerializer.Serialize(answer, JsonSerializerOptions.Web)));
        using var service = new ServiceClient(doors);

        var folds = await SessionFolds.ReadAsync(service, _markers, CancellationToken.None);

        var fold = Assert.Single(folds);
        Assert.Equal("s2", fold.Key);
        Assert.Equal(new FoldShown("needs-you", null, null, "2026-11-28T09:00:00.0000000+00:00", "its turn began and no process of it is alive"), fold.Value);
        var asked = JsonNode.Parse(Assert.Single(doors.Asked).Body!)!["leases"]!.AsArray();
        Assert.Equal("s1", Assert.Single(asked)!["session"]!.GetValue<string>());

        using var older = new ServiceClient(new Doors(new ServiceReply(404, "no route")));
        Assert.Empty(await SessionFolds.ReadAsync(older, _markers, CancellationToken.None));
    }

    [Fact]
    public void A_row_carries_the_fold_only_where_it_differs_in_the_list_s_json_and_its_line()
    {
        JsonObject Record(string id) => new()
        {
            ["id"] = id, ["repository"] = "engine", ["adapter"] = "claude-code", ["state"] = "working", ["kind"] = "driven", ["quest"] = id,
            ["line"] = id, ["created"] = T0.ToString("O"), ["updated"] = T0.ToString("O"), ["said"] = new JsonArray(),
        };
        var look = SessionLook.From(new JsonArray(Record("s1"), Record("s2")).ToJsonString(), [], [], _ => 0) with
        {
            Folds = new Dictionary<string, FoldShown> { ["s2"] = new("needs-you", null, null, T0.AddHours(1).ToString("O"), "no process of it is alive") },
        };

        var rows = SessionGroups.Read(look);

        Assert.Null(rows.Single(row => row.Session == "s1").Fold);
        var differs = rows.Single(row => row.Session == "s2");
        Assert.Equal("needs-you", differs.Fold?.State);

        var json = JsonNode.Parse(SessionsJson.Json(rows))!["sessions"]!.AsArray();
        Assert.Null(json.Single(row => row!["session"]!.GetValue<string>() == "s1")!["fold"]);
        var fold = json.Single(row => row!["session"]!.GetValue<string>() == "s2")!["fold"]!;
        Assert.Equal(("needs-you", T0.AddHours(1).ToString("O")), (fold["state"]!.GetValue<string>(), fold["since"]!.GetValue<string>()));

        var record = look.Records.Single(each => each.Id == "s2");
        Assert.Contains(SessionsListing.Facts(differs, record), said => said.StartsWith("the fold says needs-you", StringComparison.Ordinal));
        Assert.DoesNotContain(SessionsListing.Facts(rows.Single(row => row.Session == "s1"), record), said => said.Contains("fold", StringComparison.Ordinal));
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
