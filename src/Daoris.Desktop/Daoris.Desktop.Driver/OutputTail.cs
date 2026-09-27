using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A file another program is writing, read line by line as it grows (CONSOLE2): how a background
/// task's output reaches its console stream.
/// </summary>
/// <remarks>
/// <para><b>The wire carries none of a task's output.</b> The harness writes it to a file and the
/// task names the file, so reading that file is the only live view of a dev server
/// (docs/2026-09-28-console2-streams-evidence.md). This only ever READS it, shared with the writer,
/// and the lines stay on this machine: the file is under the harness's own temporary directory, and
/// the console never leaves the machine (D47 §4).</para>
///
/// <para><b>Polled, not watched.</b> A watcher's events are advisory and coalesce; a read from the
/// last offset on a short timer sees every byte whatever the writer did. A file that is not there yet
/// is read as nothing until it is, because a task names its file before the command writes to it.</para>
/// </remarks>
public sealed class OutputTail
{
    /// <summary>How often the file is read while its task runs. Short enough to read as live.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// The longest a line may run before it is said anyway. A progress bar redrawn with carriage
    /// returns never ends a line, and holding it whole would grow without bound.
    /// </summary>
    internal const int LongestLine = 16 * 1024;

    private readonly string _path;
    private readonly Action<string> _onLine;
    private readonly Decoder _decoder = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetDecoder();
    private readonly StringBuilder _open = new();
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;
    private long _offset;
    private int _finished;

    /// <param name="interval"><see cref="Interval"/> unless a test wants it faster.</param>
    public OutputTail(string path, Action<string> onLine, TimeSpan? interval = null)
    {
        _path = path;
        _onLine = onLine;
        var every = interval ?? Interval;
        _loop = Task.Run(() => LoopAsync(every));
    }

    /// <summary>
    /// Stop reading on the timer, read what is left, and say a last line that has no newline. Once:
    /// a task's own ending and its session's can both ask.
    /// </summary>
    public async Task FinishAsync()
    {
        if (Interlocked.Exchange(ref _finished, 1) == 1) return;
        _stop.Cancel();
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The timer's own ending.
        }

        lock (_gate)
        {
            Read();
            var chars = new char[8];
            var count = _decoder.GetChars([], 0, 0, chars, 0, flush: true);
            _open.Append(chars, 0, count);
            if (_open.Length > 0) Say(_open.ToString());
            _open.Clear();
        }
    }

    private async Task LoopAsync(TimeSpan every)
    {
        while (!_stop.IsCancellationRequested)
        {
            lock (_gate) Read();
            await Task.Delay(every, _stop.Token).ConfigureAwait(false);
        }
    }

    /// <summary>Everything written since the last read, as lines. Called under the gate.</summary>
    private void Read()
    {
        try
        {
            using var file = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            // A file that shrank was replaced or cut: read it again from the start rather than past its end.
            if (file.Length < _offset) _offset = 0;
            file.Seek(_offset, SeekOrigin.Begin);

            var bytes = new byte[8192];
            var chars = new char[_decoder.GetCharCount(bytes, 0, bytes.Length) + 8];
            int read;
            while ((read = file.Read(bytes, 0, bytes.Length)) > 0)
            {
                _offset += read;
                var count = _decoder.GetChars(bytes, 0, read, chars, 0, flush: false);
                _open.Append(chars, 0, count);
                SayWholeLines();
            }
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException
                                          or IOException or UnauthorizedAccessException)
        {
            // Not there yet, or held for a moment: the next read tries again, and a task whose file
            // never appears is a stream that said nothing.
        }
    }

    private void SayWholeLines()
    {
        var text = _open.ToString();
        var cut = text.LastIndexOf('\n');
        if (cut >= 0)
        {
            foreach (var line in text[..cut].Split('\n')) Say(line);
            text = text[(cut + 1)..];
        }

        if (text.Length > LongestLine)
        {
            Say(text);
            text = string.Empty;
        }

        _open.Clear().Append(text);
    }

    /// <summary>One line out. A reader that throws costs its own line, never the tail.</summary>
    private void Say(string line)
    {
        try
        {
            _onLine(line.TrimEnd('\r'));
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // The console's failure is its own; the file is still the harness's record.
        }
    }
}
