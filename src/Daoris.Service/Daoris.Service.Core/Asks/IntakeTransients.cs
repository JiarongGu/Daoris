namespace Daoris.Knowledge;

/// <summary>
/// Whether an intake's ask is let go once more after its provider kept failing (AGENTERRORS1d, D210 point 8; the agent-errors design §7):
/// the first intake that ended <c>intake.transient</c> frees its ask for a fresh one, and an ask whose earlier intake already ended the
/// same way stays bound to the failed record, so an outage costs two bounds before the person hears of it.
/// </summary>
internal static class IntakeTransients
{
    /// <summary>The code of the line the driver writes on an intake's end past its bound, as its <c>PeerNoteCodes.IntakeTransient</c> spells it.</summary>
    public const string Code = "intake.transient";

    /// <summary>
    /// Whether <paramref name="ended"/>'s end is a transient one and an earlier intake of the same ask ended so before it: its ask is then
    /// not let go.
    /// </summary>
    public static async Task<bool> SecondAsync(SessionStore sessions, Session ended, CancellationToken ct)
    {
        if (ended.Ask is not { } ask || !NoteParts.Carries(ended.NoteParts, Code)) return false;
        return (await sessions.ListAsync(ended.Repository, includeClosed: true, ct: ct).ConfigureAwait(false))
            .Any(other => other.Id != ended.Id && other.Ask == ask && NoteParts.Carries(other.NoteParts, Code));
    }
}
