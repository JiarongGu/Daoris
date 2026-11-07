namespace Daoris.Driver;

/// <summary>
/// The session list's text read as runs that go on read each record (<see cref="SessionReads.Prior"/>), by id, parsed once for each
/// text: a reader that asks one record of every record in the list asks each of the same text, which the kept list hands out once for
/// each state of the list (<see cref="SessionListKept.JsonAsync"/>), so every ask after the first is a lookup. Parsed again for each
/// ask, a home of three thousand chats read its whole list three thousand times at every look of the session groups.
/// </summary>
/// <remarks>
/// The last text alone is held, by its reference: a reader of another text parses its own, and two at once at worst parse twice. What
/// is handed out is shared between readers, who only read it.
/// </remarks>
internal static class ListedPriors
{
    private static volatile Parsed? _last;

    /// <summary>This machine's records in <paramref name="json"/> by id, a teammate's left out, the first of an id kept.</summary>
    public static IReadOnlyDictionary<string, PriorSession> Of(string json)
    {
        var last = _last;
        if (last is not null && ReferenceEquals(last.Json, json)) return last.Records;
        var parsed = new Parsed(json, SessionReads.Records(SessionReads.Listed(json)));
        _last = parsed;
        return parsed.Records;
    }

    private sealed record Parsed(string Json, IReadOnlyDictionary<string, PriorSession> Records);
}
