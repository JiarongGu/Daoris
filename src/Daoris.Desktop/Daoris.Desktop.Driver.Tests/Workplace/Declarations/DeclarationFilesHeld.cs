using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The run-time half for files (the design's §23, §62): each judged declaration file of a flow's fixtures is held open with no sharing,
/// so any other open of one, by any API, helper or library, throws a sharing violation, which is seen at its first chance even where
/// the reader swallows it. An open from a data reader's member, a row of <see cref="Readers"/>, is no declaration's read: it opens any
/// file by data and never reads a declaration's meaning.
/// </summary>
internal sealed class DeclarationFilesHeld : IDisposable
{
    /// <summary>The data readers, each by its file under the desktop tree and its member, with why it opens a declaration's file as data.</summary>
    internal static readonly IReadOnlyDictionary<string, string> Readers = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Daoris.Desktop.Modules/Loop/LoopGrounds.cs LoopGrounds.Hash"] =
            "the pipe's grounds hash each path a tree holds uncommitted, comparing bytes at the press and at Confirm",
        ["Daoris.Desktop.Modules/Loop/LoopGrounds.cs LoopGrounds.Files"] = "the pipe's grounds hash each file under a folder the person names",
    };

    private readonly List<FileStream> _held;
    private readonly HashSet<string> _readers;
    private readonly List<string> _problems = [];

    private DeclarationFilesHeld(IEnumerable<string> files, IEnumerable<string> readers)
    {
        _readers = [.. readers.Select(row => row[(row.IndexOf(' ') + 1)..])];
        _held = [.. files.Select(file => new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))];
        AppDomain.CurrentDomain.FirstChanceException += Seen;
    }

    /// <summary>Holds <paramref name="files"/> until disposed, an open by one of <paramref name="readers"/> (rows as <see cref="Readers"/> keys them) passing.</summary>
    internal static DeclarationFilesHeld Hold(IEnumerable<string> files, IEnumerable<string> readers) => new(files, readers);

    /// <summary>Each open of a held file seen so far, by the member that opened it.</summary>
    internal IReadOnlyList<string> Problems
    {
        get
        {
            lock (_problems) return [.. _problems];
        }
    }

    /// <summary>Each row of <see cref="Readers"/> whose file no longer declares its member.</summary>
    internal static IEnumerable<string> Gone(IEnumerable<string> rows) => rows.Where(row =>
    {
        var file = Path.Combine(DesktopCompilations.Desktop, row[..row.IndexOf(' ')]);
        var method = row[(row.LastIndexOf('.') + 1)..];
        return !File.Exists(file) || !File.ReadAllText(file).Contains($" {method}(", StringComparison.Ordinal);
    });

    public void Dispose()
    {
        AppDomain.CurrentDomain.FirstChanceException -= Seen;
        foreach (var handle in _held) handle.Dispose();
    }

    private void Seen(object? sender, FirstChanceExceptionEventArgs raised)
    {
        if (raised.Exception is not IOException refused || _held.FirstOrDefault(handle => refused.Message.Contains(handle.Name, StringComparison.OrdinalIgnoreCase)) is not { } held)
            return;
        var frames = new StackTrace(false).GetFrames().Select(frame => frame.GetMethod())
            .OfType<System.Reflection.MethodBase>().Where(method => method.DeclaringType is { } type && type != typeof(DeclarationFilesHeld)
                && type.Assembly.GetName().Name?.StartsWith("Daoris", StringComparison.Ordinal) is true)
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}").ToList();
        if (frames.Any(_readers.Contains)) return;
        lock (_problems) _problems.Add($"{frames.FirstOrDefault() ?? "a library"} opened the held {Path.GetFileName(held.Name)}");
    }
}
