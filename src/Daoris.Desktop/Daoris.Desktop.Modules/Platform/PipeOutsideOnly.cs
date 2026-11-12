using System.Collections.Frozen;

namespace Daoris.Desktop;

/// <summary>
/// The person's presses only a terminal outside every agents' job may ask: a join of an agent outside
/// Daoris to a channel. An agent Daoris runs is a part of its line already and speaks on its channel as itself, so its join is refused
/// <see cref="Inside"/> before any card; a place the system would not tell is said beside the card's ask as <see cref="StandingWhy.Untold"/>.
/// </summary>
public static class PipeOutsideOnly
{
    /// <summary>The refusal's code for a join from a process in an agents' job.</summary>
    public const string Inside = "inside";

    /// <summary>The refusal's sentence, which the terminal prints.</summary>
    public const string InsideSentence =
        "An agent Daoris runs is a part of its line already and speaks on its channel as itself; it does not join as an outside agent. Nothing was asked.";

    private static readonly FrozenSet<string> Routes = new[] { $"{DriverModule.Name}.CHANNEL_JOIN" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>The refusal's code for a press no agent makes: one that proves a session's key, or comes from a process in an agents' job.</summary>
    public const string PersonAlone = "person-alone";

    public const string PersonAloneSentence =
        "The growth switch is the person's alone: an agent that could turn its own refusal back off would be held by nothing. Change it in Daoris's window (Settings) or from your own terminal. Nothing was changed.";

    private static readonly FrozenSet<string> Alone = new[] { $"{DriverModule.Name}.SET_GROWTH" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the route is the person's alone: refused, in any mode, to a press proving a session's key and to a process in an agents' job.</summary>
    public static bool IsAlone(string? module, string? route) => Alone.Contains($"{module}.{route}");

    /// <summary>
    /// Where <paramref name="module"/>'s <paramref name="route"/> is one of these: the refusal's code for a caller in an agents' job, or
    /// the standing the card's ask says for one the system would not place; neither for a caller outside, or for any other route.
    /// </summary>
    public static (string? Refused, string? Said) Judge(string? module, string? route, IStandingCaller caller, PipeClientProcess? client)
    {
        if (!Routes.Contains($"{module}.{route}")) return (null, null);
        return caller.Place(client) switch
        {
            StandingPlace.InAgentJob => (Inside, null),
            StandingPlace.Outside => (null, null),
            _ => (null, StandingWhy.Untold),
        };
    }
}
