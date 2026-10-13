using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A session's transcript file (D46 §4), on the disk as it is said (TRANSCRIPT1): the one writer every door's capture
/// opens, the protocol doors', the native door's and the pipe door's alike.
/// </summary>
/// <remarks>
/// <para>🔴 The transcript was a <see cref="StreamWriter"/> nobody flushed, so it held 0 bytes while its session worked and
/// everything once it ended: nothing reading a live session's transcript saw a line it had said (DEV3d, where a rehearsal
/// waited out its whole bound on a line its stub had already said).</para>
///
/// <para><b>A line asks for a flush, and the lines written before it runs share it.</b> The flush runs on the thread pool,
/// never on the thread that wrote the line: a pump hands its line to the buffer and goes back to its stream, and the lines
/// of a burst written before the flush runs cost one write to the file between them, never one a character. The flush hands the bytes to the operating system and no further,
/// with no wait for the disk itself: a reader on this machine sees them, which is what a live transcript is for.</para>
///
/// <para>Every door writes here from more than one thread (stdout's pump and stderr's, the wire and the driver), so each
/// write and the flush take one lock. The file is opened as it always was: shared for reading, and a reader that shares
/// it for writing too reads it while it is open.</para>
/// </remarks>
internal sealed class TranscriptFile : TextWriter
{
    private readonly object _gate = new();
    private readonly StreamWriter _file;

    // A flush is on its way, so a line written before it runs needs no other.
    private bool _flushing;

    private bool _closed;

    private TranscriptFile(StreamWriter file) => _file = file;

    /// <summary>Open a transcript: from its start, or going on from what a run before it left.</summary>
    public static TranscriptFile Open(string path, bool append) => new(new StreamWriter(path, append));

    public override Encoding Encoding => _file.Encoding;

    public override void Write(char value)
    {
        lock (_gate)
        {
            _file.Write(value);
            AskFlush();
        }
    }

    public override void Write(char[] buffer, int index, int count)
    {
        lock (_gate)
        {
            _file.Write(buffer, index, count);
            AskFlush();
        }
    }

    public override void Write(string? value)
    {
        lock (_gate)
        {
            _file.Write(value);
            AskFlush();
        }
    }

    public override void WriteLine(string? value)
    {
        lock (_gate)
        {
            _file.WriteLine(value);
            AskFlush();
        }
    }

    public override void Flush()
    {
        lock (_gate) _file.Flush();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_gate)
            {
                _closed = true;
                _file.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    // Under the lock: one flush queued at a time, which every line written before it runs shares.
    private void AskFlush()
    {
        if (_flushing) return;
        _flushing = true;
        ThreadPool.UnsafeQueueUserWorkItem(static transcript => transcript.Flushed(), this, preferLocal: false);
    }

    private void Flushed()
    {
        lock (_gate)
        {
            _flushing = false;
            if (_closed) return;
            try
            {
                _file.Flush();
            }
            catch (Exception)
            {
                // On the pool, a throw would end the application, for a file that is diagnostic and never the record
                // (D46 §4). The bytes stay with the writer: the next line's flush tries again, and the close says why.
            }
        }
    }
}
