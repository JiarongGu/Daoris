using System.Diagnostics;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The library's leases as a process's death keeps them: each call's open and close is a line appended to this process's own file in the
/// home's run folder, written before the library is entered and after it returns. A file whose process is gone and whose last open has
/// no close names the repository that process ended inside (<see cref="Left"/>).
/// </summary>
/// <remarks>
/// <para><b>One file a process, named by its id and start time</b>, so a reused id never inherits a dead process's file and a start reads
/// every other process's file without a lock; a file read is removed.</para>
/// <para><b>Unbuffered and written through.</b> Each line is one write that has reached the operating system when the call returns, which
/// a process's death does not lose. A file that cannot be written leaves the call unguarded: no read waits on the guard.</para>
/// <para>Compiled by link into the test stand-in that dies inside a lease, so it uses the base library alone.</para>
/// </remarks>
internal sealed class GitLibraryLeases(string folder)
{
    private readonly Lock _gate = new();
    private FileStream? _file;
    private long _next;

    /// <summary>Appends an open of <paramref name="repository"/>; the id to close it by, or -1 where the file cannot be written.</summary>
    internal long Open(string repository)
    {
        lock (_gate)
        {
            var id = ++_next;
            return Append($"open\t{id}\t{Uri.EscapeDataString(repository)}\n") ? id : -1;
        }
    }

    /// <summary>Appends the close of <paramref name="id"/>, as <see cref="Open"/> gave it.</summary>
    internal void Close(long id)
    {
        if (id < 0) return;
        lock (_gate) Append($"close\t{id}\n");
    }

    /// <summary>
    /// The repositories a process that is gone ended inside, from each such process's file in <paramref name="folder"/>; every file of
    /// a gone process is removed, and a live process's is left.
    /// </summary>
    internal static IReadOnlyList<string> Left(string folder)
    {
        var left = new List<string>();
        if (!Directory.Exists(folder)) return left;
        foreach (var path in Directory.EnumerateFiles(folder, "*.log"))
        {
            if (!Gone(Path.GetFileNameWithoutExtension(path))) continue;
            try
            {
                left.AddRange(Unclosed(Lines(path)));
                File.Delete(path);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Another start read it first, or it cannot be read now: the next start asks again.
            }
        }

        return [.. left.Distinct(StringComparer.Ordinal)];
    }

    /// <summary>The file's lines, read beside a writer: a process just killed may not have let go of its handle yet.</summary>
    private static string[] Lines(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return new StreamReader(stream, Encoding.UTF8).ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    private static IEnumerable<string> Unclosed(string[] lines)
    {
        var open = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var parts in lines.Select(line => line.Split('\t')))
        {
            if (parts is ["open", var id, var repository]) open[id] = Uri.UnescapeDataString(repository);
            else if (parts is ["close", var closed]) open.Remove(closed);
        }

        return open.Values;
    }

    private static bool Gone(string name)
    {
        if (name.Split('-') is not [var id, var started] || !int.TryParse(id, out var pid) || !long.TryParse(started, out var ticks)) return false;
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.StartTime.ToUniversalTime().Ticks != ticks;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return true;
        }
    }

    private bool Append(string line)
    {
        try
        {
            if (_file is null)
            {
                Directory.CreateDirectory(folder);
                using var self = Process.GetCurrentProcess();
                var name = $"{Environment.ProcessId}-{self.StartTime.ToUniversalTime().Ticks}.log";
                _file = new FileStream(Path.Combine(folder, name), FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.WriteThrough);
            }

            var bytes = new UTF8Encoding(false).GetBytes(line);
            _file.Write(bytes, 0, bytes.Length);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
