using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The person's words a press puts on an ask, judged by who pressed it (<see cref="PipeDoor.By"/>): Ask Daoris's against the person's own
/// messages in the conversation whose delegate key proved it, read from this Daoris's session events (<see cref="AskQuotes"/>).
/// </summary>
internal static class AskQuotedPress
{
    /// <summary>Why <paramref name="carried"/> cannot go on an ask as this request was pressed, or null.</summary>
    public static string? Refused(DriverLoop loop, AskCarried carried)
    {
        var (by, session) = PipeDoor.By;
        return AskQuotes.Refused(carried, by, () => session is null ? [] : AskQuotes.PersonSaid(loop.Events.After(session, 0).Events));
    }

    /// <summary>The refusal said on the press's own lines, exit 1, and nothing sent.</summary>
    public static Task<int> Say(TextWriter output, string refused)
    {
        output.WriteLine(refused);
        return Task.FromResult(1);
    }
}
