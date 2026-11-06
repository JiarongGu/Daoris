using System.Globalization;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// BRAINSTORM1a3 (D176 points 1 and 8), ONEWAY1t: a brainstorm's request, send and stop are written by the service's desk alone, and
/// read by it and by the driver's look. They share no code, so the files are the contract: this reader and the service's
/// <c>BrainstormDesk</c> are held, row for row, to one table, <c>fixtures/brainstorm-files.json</c>.
/// </summary>
public sealed class BrainstormFilesTwinTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-brainstorm-twin-" + Guid.NewGuid().ToString("N")[..8]);

    public BrainstormFilesTwinTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private BrainstormFiles Files => new(_home);

    [Fact]
    public void A_pass_and_a_restart_keep_the_actor_request_and_reported_models()
    {
        var asked = ActorAsked.Before("acp-stub");
        var record = new BrainstormPassRecord(new BrainstormBy("g1", "maker", "acp-stub", "one"), "generate", "running", [], 1)
        {
            Actor = "brainstorm", Choice = "before", Asked = asked,
            Ran = new ActorRan(["first-model"], 0, [], 0),
        };
        Files.WritePass("b1", record);
        var restart = Assert.Single(new BrainstormFiles(_home).ReadPasses("b1"));
        Assert.Equal("brainstorm", restart.Actor);
        Assert.Equal("before", restart.Choice);
        Assert.Equal(asked, restart.Asked);
        Assert.Equal("first-model", restart.Ran!.Models.Single());
        var reports = new ActorReports(restart.Ran);
        reports.Report(["next-model"]);
        Files.WritePass("b1", restart with { Turns = 2, Ran = reports.Snapshot });
        var resumed = Assert.Single(new BrainstormFiles(_home).ReadPasses("b1"));
        Assert.Equal(["first-model", "next-model"], resumed.Ran!.Models);
        Assert.Equal(2, resumed.Turns);
    }

    private static JsonNode Table() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "brainstorm-files.json")))!;

    private void Put(string id, string file, string text)
    {
        Directory.CreateDirectory(Files.Folder(id));
        File.WriteAllText(Files.PathOf(id, file), text);
    }

    /// <summary>🔴 Every request, state and send row of the one table reads here as it reads in the service's desk.</summary>
    [Fact]
    public void Every_row_of_the_files_table_reads_as_the_service_reads_it()
    {
        var table = Table();
        var rows = 0;
        foreach (var row in table["requests"]!.AsArray())
        {
            var id = $"rq{rows++:D6}";
            Put(id, "request.json", row!["file"]!.GetValue<string>());
            var read = Files.Request(id);
            var name = row["name"]!.GetValue<string>();
            if (row["reads"] is not JsonObject reads)
            {
                Assert.True(read is null, name);
                continue;
            }

            Assert.True(read is not null, name);
            Assert.Equal(reads["question"]!.GetValue<string>(), read.Question);
            Assert.Equal(reads["repository"]?.GetValue<string>(), read.Repository);
            Assert.Equal(reads["agents"]!.AsArray().Select(agent => agent!.GetValue<string>()), read.Agents);
            Assert.Equal(DateTimeOffset.Parse(reads["at"]!.GetValue<string>(), CultureInfo.InvariantCulture), read.At);
            Assert.Equal(reads["sources"]?.AsArray().Select(source => source!.GetValue<string>()) ?? [], read.Sources ?? []);
        }

        foreach (var row in table["states"]!.AsArray())
        {
            var id = $"st{rows++:D6}";
            Put(id, "state.json", row!["file"]!.GetValue<string>());
            var read = Files.ReadState(id);
            if (row["reads"] is not JsonObject reads)
            {
                Assert.Null(read);
                continue;
            }

            Assert.Equal(
                (reads["state"]!.GetValue<string>(), reads["packet"]?.GetValue<string>(), reads["code"]?.GetValue<string>()),
                (read!.State, read.Packet, read.Code));
        }

        foreach (var row in table["sent"]!.AsArray())
        {
            var id = $"se{rows++:D6}";
            Put(id, "sent.json", row!["file"]!.GetValue<string>());
            Assert.Equal(row["reads"]?.GetValue<string>(), Files.Sent(id));
        }
    }

    /// <summary>🔴 The stop and a meeting's request, state and per-round send rows read here as the service's desk reads them (ONEWAY1t).</summary>
    [Fact]
    public void The_stop_and_the_meeting_rows_of_the_files_table_read_as_the_service_reads_them()
    {
        var table = Table();
        var meetings = new MeetingFiles(_home);

        foreach (var row in table["stops"]!.AsArray())
        {
            var id = $"sp{row["name"]!.GetValue<string>().Length:D3}";
            if (row["file"] is { } file) Put(id, "stop.json", file.GetValue<string>());
            else Directory.CreateDirectory(Files.Folder(id));
            Assert.Equal(row["reads"]!.GetValue<bool>(), Files.Stopped(id));
        }

        var rows = table["meetings"]!.AsArray();
        Directory.CreateDirectory(meetings.Folder("mt000001"));
        File.WriteAllText(meetings.PathOf("mt000001", "request.json"), rows[0]!["file"]!.GetValue<string>());
        var request = meetings.Request("mt000001")!;
        Assert.Equal((rows[0]!["reads"]!["question"]!.GetValue<string>(), rows[0]!["reads"]!["repository"]!.GetValue<string>()), (request.Question, request.Repository));
        Assert.Equal(rows[0]!["reads"]!["agents"]!.AsArray().Select(agent => agent!.GetValue<string>()), request.Agents);

        foreach (var row in new[] { rows[1], rows[2] })
        {
            File.WriteAllText(meetings.PathOf("mt000001", "state.json"), row!["state"]!.GetValue<string>());
            var state = meetings.ReadState("mt000001")!;
            Assert.Equal(
                (row["reads"]!["state"]!.GetValue<string>(), row["reads"]!["round"]!.GetValue<int>(), row["reads"]!["packet"]!.GetValue<string>()),
                (state.State, state.Round, state.Packet));
        }

        var send = rows[3]!;
        Directory.CreateDirectory(Path.Combine(meetings.Folder("mt000001"), "sent"));
        File.WriteAllText(meetings.PathOf("mt000001", "sent", $"{send["round"]!.GetValue<int>()}.json"), send["sent"]!.GetValue<string>());
        Assert.Equal(send["reads"]!["sent"]!.GetValue<string>(), meetings.Sent("mt000001", send["round"]!.GetValue<int>()));
    }
}
