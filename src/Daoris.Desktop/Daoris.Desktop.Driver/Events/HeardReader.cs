namespace Daoris.Driver;

/// <summary>
/// A harness's stream as its capture reads it, telling the watcher each time a line is read (SESSWATCH1a, D200, the session-watch design
/// §1.2): the time alone, never the line. Each capture is handed its streams through one of these, its own parameter, so a subagent's
/// line taken aside, a partial delta dropped and a stderr line each count as heard, and nothing rides <see cref="HarnessWords"/>.
/// </summary>
/// <param name="inner">The stream itself.</param>
/// <param name="heard">Told after each line is read; one in-memory write on the reading thread.</param>
internal sealed class HeardReader(TextReader inner, Action heard) : TextReader
{
    public override async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        var line = await inner.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (line is not null) heard();
        return line;
    }

    public override Task<string?> ReadLineAsync() => ReadLineAsync(CancellationToken.None).AsTask();

    public override string? ReadLine()
    {
        var line = inner.ReadLine();
        if (line is not null) heard();
        return line;
    }

    public override int Peek() => inner.Peek();

    public override int Read()
    {
        var read = inner.Read();
        if (read >= 0) heard();
        return read;
    }

    public override int Read(char[] buffer, int index, int count)
    {
        var read = inner.Read(buffer, index, count);
        if (read > 0) heard();
        return read;
    }

    public override async Task<int> ReadAsync(char[] buffer, int index, int count)
    {
        var read = await inner.ReadAsync(buffer, index, count).ConfigureAwait(false);
        if (read > 0) heard();
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
    {
        var read = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (read > 0) heard();
        return read;
    }

    public override string ReadToEnd()
    {
        var text = inner.ReadToEnd();
        if (text.Length > 0) heard();
        return text;
    }

    public override async Task<string> ReadToEndAsync()
    {
        var text = await inner.ReadToEndAsync().ConfigureAwait(false);
        if (text.Length > 0) heard();
        return text;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) inner.Dispose();
        base.Dispose(disposing);
    }
}
