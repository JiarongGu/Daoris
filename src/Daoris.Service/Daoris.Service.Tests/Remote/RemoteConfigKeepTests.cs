using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// KEEPALL1e (D173's KEEPALL1e note): the service reads the <c>remotes.json</c> its twins' editors write. The driver's <c>RemoteFile</c>
/// and the CLI's <c>readRemoteFile</c> are held to <c>src/Daoris.Cli/test/fixtures/home-files-keep.json</c> for the bytes an edit
/// writes; each <c>remotes.json</c> row's <c>reads</c> is the map every reader reads from them, and this reader is the third.
/// </summary>
public sealed class RemoteConfigKeepTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "daoris-remotekeep-" + Guid.NewGuid().ToString("N")[..8]);

    public RemoteConfigKeepTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void The_service_reads_what_an_edit_wrote_as_its_twins_do(string name, string written, string reads)
    {
        var path = Path.Combine(_dir, "remotes.json");
        File.WriteAllText(path, written);

        Assert.Equal((name, reads), (name, Read(RemoteConfig.Load(_ => null, path))));
    }

    private static string Read(IReadOnlyDictionary<string, RemoteConfig> remotes) =>
        string.Join("; ", remotes.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => $"{entry.Key} {entry.Value.Url} {entry.Value.Key}"));

    private static string Read(JsonElement reads) =>
        string.Join("; ", reads.EnumerateObject().OrderBy(entry => entry.Name, StringComparer.Ordinal)
            .Select(entry => $"{entry.Name} {entry.Value.GetProperty("url").GetString()} {entry.Value.GetProperty("key").GetString()}"));

    public static TheoryData<string, string, string> Rows()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "src", "Daoris.Cli", "test", "fixtures", "home-files-keep.json")));
        var rows = new TheoryData<string, string, string>();
        foreach (var row in table.RootElement.GetProperty("rows").EnumerateArray())
        {
            if (row.GetProperty("file").GetString() != "remotes.json") continue;
            var crlf = row.TryGetProperty("crlf", out var ends) && ends.GetBoolean();
            rows.Add(row.GetProperty("name").GetString()!, Changed(table.RootElement, row.GetProperty("changes"), crlf), Read(row.GetProperty("reads")));
        }

        return rows;
    }

    private static string Lines(JsonElement lines) => string.Join("\n", lines.EnumerateArray().Select(line => line.GetString()));

    /// <summary>The table's <c>remotes.json</c> with <paramref name="changes"/> made, as its twins' <c>HomeFilesKeepTests</c> make them.</summary>
    private static string Changed(JsonElement table, JsonElement changes, bool crlf)
    {
        var text = Lines(table.GetProperty("files").GetProperty("remotes.json")) + "\n";
        foreach (var change in changes.EnumerateArray())
        {
            var from = Lines(change.GetProperty("from"));
            var at = text.IndexOf(from, StringComparison.Ordinal);
            if (at < 0 || text.IndexOf(from, at + 1, StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException($"a change's `from` is in the file once: {from}");
            }

            var to = Lines(change.GetProperty("to"));
            // A change to nothing takes its line's end with it.
            text = to.Length == 0 && text[at + from.Length] == '\n' ? text[..at] + text[(at + from.Length + 1)..] : text[..at] + to + text[(at + from.Length)..];
        }

        return crlf ? text.Replace("\n", "\r\n", StringComparison.Ordinal) : text;
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("no workspace root above the test binaries");
    }
}
