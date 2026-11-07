using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// A door's version question, asked once per binary (PERF3): what was read of the files the question would start is its answer while
/// they stand as they stood, so a look, a list and a restart start no process. Each start of a harness went through the machine's
/// file scanners, its own children with it, and cost hundreds of milliseconds.
/// </summary>
internal static class BinaryVersion
{
    /// <summary>
    /// The answer to <paramref name="info"/>, <paramref name="door"/>'s version question as the probe would start it, said of it as
    /// <paramref name="named"/>: the kept reading of the same files, unless <paramref name="again"/>; else asked, and kept where files
    /// answered or nothing was there to start. A file there that did not answer, or an answer of no file found, is asked again next time.
    /// </summary>
    public static async Task<(bool Ran, string Output, string? Problem)> AskAsync(
        string home, string door, ProcessStartInfo info, string named, bool again, Func<DateTimeOffset> now, CancellationToken ct)
    {
        var files = FilesOf(info);
        if (!again && BinaryReads.Of(home, door) is { } kept && kept.Of(files)) return (kept.Present, kept.Version ?? "", kept.Problem);

        var asked = await HarnessProbe.RunAsync(info, named, ct).ConfigureAwait(false);
        if (asked.Ran != files.Count > 0) return asked;
        try
        {
            BinaryReads.Keep(home, door, new BinaryRead(files, asked.Ran, asked.Ran ? HarnessProbe.FirstLine(asked.Output) : null, asked.Problem, now()));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A reading not kept is asked again at the next look; never the answer this one gives.
        }

        return asked;
    }

    /// <summary>
    /// The files a start runs: its program, found on the start's own <c>PATH</c>, and its first argument where that is a file, the
    /// script a runtime is handed (npm's package started as node and its script, LAUNCHCHAIN1). A later argument is the program's own.
    /// </summary>
    internal static IReadOnlyList<BinaryFile> FilesOf(ProcessStartInfo info)
    {
        var path = info.Environment.TryGetValue(CommandPresence.PathVariable, out var inherited) ? inherited : null;
        var script = info.ArgumentList.Count > 0 && Path.IsPathRooted(info.ArgumentList[0]) ? info.ArgumentList[0] : null;
        IEnumerable<string?> named = [CommandPresence.Resolve(info.FileName, path), script];
        return [.. named.OfType<string>().Select(BinaryFile.Of).OfType<BinaryFile>()];
    }
}
