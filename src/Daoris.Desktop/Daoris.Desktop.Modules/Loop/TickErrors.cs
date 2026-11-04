namespace Daoris.Desktop;

/// <param name="Code">A failure the page words from its catalogue, or null when the words are the driver's.</param>
/// <param name="Message">The driver's own sentence, which a page that does not know the code shows.</param>
public sealed record TickError(string? Code, string Message);

/// <summary>
/// What a failed tick tells the page: said once until a tick runs again, since a tick report is a log and a toast is an
/// interruption, and a failure the driver can name is named.
/// </summary>
/// <remarks>
/// A refused connection is <c>SERVICE_UNREACHABLE</c>, the code the page's own requests use, so both doors say one sentence in the
/// reader's language rather than the socket's own words. Called by the watch loop alone, one tick at a time, so it holds no lock.
/// </remarks>
public sealed class TickErrors
{
    private string? _last;

    /// <summary>The failure to tell the page, or null when it is the one already told.</summary>
    public TickError? Failed(Exception error)
    {
        var said = error is HttpRequestException { StatusCode: null }
            ? new TickError("SERVICE_UNREACHABLE", "the service is not answering — the driver looks again in a few seconds.")
            : new TickError(null, error.Message);
        if (said.Message == _last) return null;
        _last = said.Message;
        return said;
    }

    /// <summary>A tick ran, so the next failure is news again.</summary>
    public void Ran() => _last = null;
}
