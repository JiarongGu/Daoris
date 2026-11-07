namespace Daoris.Driver;

/// <summary>
/// A door that asks no version question and is present by a look at its command's file (a declared door with no version arguments):
/// the look is kept as its binary's reading, so the sheet and the role structure say what the last report found of it, as
/// they say a version question's answer, and no look of their own is made.
/// </summary>
internal static class BinaryPresence
{
    /// <summary>
    /// Whether <paramref name="resolved"/>'s program is there, kept for <paramref name="door"/> as a reading of the file found; written
    /// only where it differs from the kept one, so a look that finds what the last one found writes nothing.
    /// </summary>
    public static (bool Ran, string Output, string? Problem) Look(string home, string door, IReadOnlyList<string> resolved, Func<DateTimeOffset> now)
    {
        var found = resolved.Count > 0 ? CommandPresence.Resolve(resolved[0]) : null;
        IReadOnlyList<BinaryFile> files = found is not null && BinaryFile.Of(found) is { } file ? [file] : [];
        var present = found is not null;
        if (BinaryReads.Of(home, door) is { } kept && kept.Present == present && kept.Version is null && kept.Of(files)) return (present, "", null);
        try
        {
            BinaryReads.Keep(home, door, new BinaryRead(files, present, null, null, now()));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A look not kept is looked at again by the next report; never the answer this one gives.
        }

        return (present, "", null);
    }
}
