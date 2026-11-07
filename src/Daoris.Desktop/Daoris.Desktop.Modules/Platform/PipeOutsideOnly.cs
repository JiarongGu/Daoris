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
