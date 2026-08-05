using System.Diagnostics;
using System.Text;

namespace Daoris.Devkit;

/// <param name="Label">What to print — enough to find the thing again.</param>
/// <param name="Text">The text to scan.</param>
/// <param name="Sha">
/// The object's full sha, or null for a path. Carried separately from the label so a review
/// acknowledgement can be matched against the identifier rather than against display text.
/// </param>
public readonly record struct HistoryItem(string Label, string Text, string? Sha = null);

/// <summary>Everything a repository has ever contained, behind a seam so the gate is testable.</summary>
public interface IGitObjects
{
    /// <summary>
    /// Every reachable blob's content, every commit message, and every path any blob ever had.
    /// </summary>
    /// <remarks>
    /// Streamed rather than materialized. History is the one input whose size is unbounded — a repo
    /// with a decade of commits has objects measured in gigabytes, and a scan that has to hold them
    /// all before it can report the first finding is a scan nobody waits for.
    /// </remarks>
    IEnumerable<HistoryItem> Everything();
}

/// <summary>git, through one long-lived `cat-file` process.</summary>
/// <remarks>
/// <para><b>One process, not one per object.</b> The obvious implementation shells out to `git cat-file`
/// per sha; on this repository's 932 objects that is 932 process launches, and on a real history it is
/// where the whole audit's time goes. `--batch-all-objects --batch` streams every object through a
/// single pipe.</para>
///
/// <para><b>Read as bytes, not as text.</b> The stream carries binary blobs — images, compiled output —
/// interleaved with the record framing, and a StreamReader would corrupt the framing trying to decode
/// them. The header is parsed as ASCII, then exactly <c>size</c> bytes are consumed, which is the only
/// way to stay aligned.</para>
/// </remarks>
public sealed class CommandLineGitObjects(string repositoryRoot) : IGitObjects
{
    public IEnumerable<HistoryItem> Everything()
    {
        foreach (var item in Paths()) yield return item;
        foreach (var item in Objects()) yield return item;
    }

    /// <summary>
    /// Every path any object ever had.
    /// </summary>
    /// <remarks>
    /// Deleting a file does not remove the name it had from history, and a name can be the leak on its
    /// own — a document titled after a private project says what it says whatever is inside it.
    /// </remarks>
    private IEnumerable<HistoryItem> Paths()
    {
        var result = Process.Run("git", ["rev-list", "--objects", "--all"], repositoryRoot);
        if (result.ExitCode != 0)
        {
            throw new DevkitException($"git rev-list failed: {result.Error.Trim()}");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            // "<sha> <path>" — objects with no path (commits, root trees) are just "<sha>".
            var space = line.IndexOf(' ');
            if (space < 0) continue;

            var path = line[(space + 1)..].Trim();
            if (path.Length > 0 && seen.Add(path)) yield return new HistoryItem($"{path} (path, historical)", path);
        }
    }

    private IEnumerable<HistoryItem> Objects()
    {
        var start = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { "cat-file", "--batch-all-objects", "--batch", "--buffer" })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = System.Diagnostics.Process.Start(start)
            ?? throw new DevkitException("could not start git cat-file");

        var stream = process.StandardOutput.BaseStream;
        while (true)
        {
            var header = ReadHeader(stream);
            if (header is null) break;

            var parts = header.Split(' ');
            if (parts.Length < 3 || !long.TryParse(parts[2], out var size))
            {
                // A malformed record means the stream is no longer aligned, and every byte after it
                // would be scanned as if it were something else. Stopping loudly beats reporting
                // nonsense findings from a desynchronized pipe.
                throw new DevkitException($"git cat-file produced an unreadable record: '{header}'");
            }

            var (sha, type) = (parts[0], parts[1]);
            var payload = ReadExactly(stream, size);
            stream.ReadByte();   // the newline git writes after each object's content

            // Trees are structure, and tags carry a message but no content worth scanning; the paths
            // inside trees already arrived through rev-list.
            if (type is not ("blob" or "commit")) continue;
            if (payload.AsSpan().IndexOf((byte)0) >= 0) continue;   // binary

            var text = Encoding.UTF8.GetString(payload);
            var label = type == "commit" ? $"commit {sha[..7]} (message)" : $"blob {sha[..7]}";
            yield return new HistoryItem(label, text, sha);
        }

        process.WaitForExit();
    }

    /// <summary>One ASCII line, byte at a time — the only way to stop exactly at the content.</summary>
    private static string? ReadHeader(Stream stream)
    {
        var header = new StringBuilder();
        while (true)
        {
            var next = stream.ReadByte();
            if (next < 0) return header.Length == 0 ? null : header.ToString();
            if (next == '\n') return header.ToString();
            header.Append((char)next);
        }
    }

    private static byte[] ReadExactly(Stream stream, long size)
    {
        var buffer = new byte[size];
        var offset = 0;
        while (offset < size)
        {
            var read = stream.Read(buffer, offset, (int)(size - offset));
            if (read == 0) throw new DevkitException("git cat-file ended mid-object");
            offset += read;
        }

        return buffer;
    }
}
