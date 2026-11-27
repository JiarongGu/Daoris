using System.Text;

namespace Daoris.Driver;

/// <summary>
/// Reads a check's log as its runner writes it, for the one line that says its builds are done (<see cref="Line"/>, from
/// <c>tools/merge/reached-steps.ts</c>'s <c>builtLine</c>): whole, alone on its line, from the line's first character. A line half written is
/// not yet said, and the same words inside another line are not it. Reads only what the log gained since the last look, and only whole lines.
/// </summary>
internal sealed class BuiltWatch(string log)
{
    /// <summary>The line the runner says once, when no project it builds is left to build.</summary>
    internal const string Line = "reached-built: {}";

    private long _offset;

    private bool _seen;

    /// <summary>Whether the line has been said, looking at what the log gained since the last look; true for good once it has.</summary>
    internal bool Seen()
    {
        if (_seen) return true;
        try
        {
            if (!File.Exists(log)) return false;
            using var stream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length < _offset) _offset = 0;
            stream.Seek(_offset, SeekOrigin.Begin);
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var bytes = buffer.ToArray();
            var whole = Array.LastIndexOf(bytes, (byte)'\n') + 1;
            if (whole == 0) return false;
            _offset += whole;
            _seen = Encoding.UTF8.GetString(bytes, 0, whole).Split('\n').Any(each => each.TrimEnd('\r') == Line);
            return _seen;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Looks at the log every <paramref name="poll"/> until the line is said, then says so once and ends; ends quietly, saying nothing, where
    /// <paramref name="ct"/> ends first.
    /// </summary>
    internal static async Task RunAsync(string log, Action said, TimeSpan poll, TimeProvider clock, CancellationToken ct)
    {
        var watch = new BuiltWatch(log);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (watch.Seen())
                {
                    said();
                    return;
                }

                await Task.Delay(poll, clock, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The run ended before it said it.
        }
    }
}
