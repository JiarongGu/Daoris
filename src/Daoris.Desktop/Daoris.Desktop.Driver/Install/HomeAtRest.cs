using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// Whether nothing of Daoris runs on a home, for a verb that changes what an instance holds open and so acts only while none does
/// (<c>home purge --history</c>, <c>agent ids --yes</c>): a desktop, a <c>daoris drive</c> or another verb holding the lease, the service
/// the desktop left its address for, or a session whose process still runs.
/// </summary>
internal static class HomeAtRest
{
    /// <summary>Whether the home's lease is this verb's own, which the host took for it before the door ran.</summary>
    /// <param name="verb">The verb's words, as the lease's <c>What</c> names them.</param>
    public static bool OwnLease(string home, string verb) =>
        HomeLease.HeldHere(home) && HomeLease.HeldBy(home) is { Kind: LeaseKind.Verb, What: { } what } && what.Contains(verb, StringComparison.Ordinal);

    /// <summary>What holds the home now, in a sentence, or null where nothing of Daoris runs on it.</summary>
    public static string? Running(string home, string verb)
    {
        if (HomeLease.HeldBy(home) is { } holder && !OwnLease(home, verb)) return $"{home} is held by {holder.Named}.";
        return RunningAddress.Read(home) is { } entry && RunningService.Lives(entry.Pid, entry.Started)
            ? $"Daoris's service runs on {home} (process {entry.Pid.ToString(CultureInfo.InvariantCulture)})."
            : null;
    }

    /// <summary>A session whose process still runs on the home, in a sentence, or null where none does.</summary>
    public static string? Sessions(string home)
    {
        var sessions = LiveSessions.ReadAsync(null, DaorisHome.Entry(home, HomeEntry.SessionPids), CancellationToken.None).GetAwaiter().GetResult();
        return sessions.Problem ?? (sessions.Sessions.Count > 0 ? $"a session still runs on {home}: its connector writes the store." : null);
    }
}
