using System.Globalization;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// BRAINSTORM1a3 (D176 points 1 and 8): a brainstorm's records are written by two doors, this verb and the local host's brainstorm
/// door that <c>daoris brainstorm</c> reaches, and read by both. They share no code, so the files are the contract: this reader and
/// the service's <c>BrainstormDesk</c> are held, row for row, to one table, <c>fixtures/brainstorm-files.json</c>.
/// </summary>
public sealed class BrainstormFilesTwinTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-brainstorm-twin-" + Guid.NewGuid().ToString("N")[..8]);

    public BrainstormFilesTwinTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private BrainstormFiles Files => new(_home);

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

    /// <summary>The request this verb writes is the table's first row's shape, key for key in its order, as the service's door writes it.</summary>
    [Fact]
    public void The_verbs_request_is_written_in_the_tables_shape()
    {
        var id = Files.WriteRequest("Why?", "reports", ["stub"], DateTimeOffset.Parse("2026-10-19T10:00:00Z", CultureInfo.InvariantCulture));

        var written = JsonNode.Parse(File.ReadAllText(Files.PathOf(id, "request.json")))!.AsObject();
        var shape = JsonNode.Parse(Table()["requests"]![0]!["file"]!.GetValue<string>())!.AsObject();
        Assert.Equal(shape.Select(pair => pair.Key), written.Select(pair => pair.Key));
        Assert.Equal(id, written["id"]!.GetValue<string>());
    }
}
