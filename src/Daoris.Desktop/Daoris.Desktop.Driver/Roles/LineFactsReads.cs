namespace Daoris.Driver;

/// <summary>
/// What a line's counts, parts and go-ons are read through below the course (CONTEXTDRIVE1b, CONTEXTDRIVE1i): the brief and the
/// supervisor's print read them; the course installs it when the driver loads (<c>Course/LineFactsWiring.cs</c>).
/// </summary>
internal interface ILineFactsReads
{
    /// <summary>The line <paramref name="session"/> is on, over the records' text as the service answered it (null where it did not).</summary>
    LineFacts Of(string home, string session, string? recordsJson);

    /// <summary>The same, the records read from <paramref name="service"/> now.</summary>
    Task<LineFacts> OfAsync(ServiceClient service, string home, string session, CancellationToken ct);
}

internal static class LineFactsReads
{
    private static ILineFactsReads? _current;

    internal static ILineFactsReads Current
    {
        get => _current ?? throw new InvalidOperationException("no line facts reads are installed: the course installs them when the driver loads");
        set => _current = value;
    }
}
