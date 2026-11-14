namespace Daoris.Driver;

/// <summary>
/// A record that has ended while its line goes on (a carry-on or a start waits on its quest): one reading, so <c>session stop</c> and
/// <c>session archive</c> never answer the same record as ended and as running.
/// </summary>
public static class EndedLine
{
    /// <summary>Whether any part of the line is working or pending, as the sessions' reader placed it.</summary>
    internal static bool GoesOn(IEnumerable<IWorkplaceSessionRow> parts) =>
        parts.Any(row => row.Group is SessionGroup.Working or SessionGroup.Pending);

    /// <summary>What both verbs say of an ended record whose line goes on.</summary>
    public static string Said(string session, string state) =>
        $"{session} ended ({SessionsListing.Word(state)}) and its line goes on at the next look: nothing here runs it, so there is "
        + "nothing to stop, and it is archived once that line ends.";
}
