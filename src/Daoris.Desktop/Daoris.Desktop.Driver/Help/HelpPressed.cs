namespace Daoris.Driver;

/// <summary>
/// A press Ask Daoris's verb made in full access, proved by its conversation's delegate key and run with no card: the person's own act,
/// said once on that conversation's console, so what it pressed is read where the person asked for it.
/// </summary>
internal static class HelpPressed
{
    /// <summary>The console's line for a press of <paramref name="route"/>, the module and the route as the pipe names them.</summary>
    public static string Said(string route) =>
        $"— Ask Daoris pressed {route} through Daoris's verb in full access: it ran at once, as your own press.";

    /// <summary>The line said on <paramref name="sessionId"/>'s console, where it is shown here.</summary>
    public static void Tell(SessionOutput? output, string sessionId, string route) => output?.Append(sessionId, Said(route));
}
