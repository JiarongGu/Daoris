namespace Daoris.Driver;

/// <summary>
/// An install's update, the machine-local half (UPDATE1, D139): the request both doors write under the home, which
/// mode holds for what is staged, and what a draining look plans with.
/// </summary>
/// <remarks>
/// <para><b>Two doors, one file</b> (D50): the application's banner and <c>daoris-driver update</c> write
/// <c>$DAORIS_HOME/update.json</c> through <c>InstallUpdate.Write</c>, and the application reads it at each of its looks. Absent or
/// unreadable is the default, a staged build installed when idle, so a torn file never stops an update nor starts one now.</para>
///
/// <para><b>The drain holds starts, never looks</b> (D139 §2, rejected: skipping looks). A look still reports endings, tells
/// plugins and syncs; <see cref="Drained"/> is the config it plans with, and <see cref="HeldFor"/> says the hold in its own
/// words where the person's hold would otherwise be named.</para>
///
/// <para><b>Its rules are pure</b> (STRUCT3e): the request's file is read, written and cleared, and an install found beside a
/// home, by <see cref="InstallUpdateFile"/>, declared as this type's own static members, so every caller keeps its call.</para>
/// </remarks>
public static class InstallUpdate
{
    /// <summary>The request's file, under the home.</summary>
    public const string FileName = UpdateRequests.FileName;

    /// <summary>
    /// Why a quest sits while an update drains — the driver's sentence, which the page says in the reader's language by the
    /// tick's <c>forUpdate</c> (UX5 U27), never by these words.
    /// </summary>
    public const string HoldReason =
        "an update is waiting for this machine's sessions to end: nothing new starts until it is installed and Daoris "
        + "starts again — Not now, or `daoris-driver update --cancel`, lets work start meanwhile.";

    public static string PathOf(string home) => Path.Combine(home, FileName);

    /// <summary>
    /// The mode that holds for the build staged now: the request's, where it was said of this build or of whatever is
    /// staged; <see cref="UpdateMode.WhenIdle"/> otherwise, so a newer stage drains again after a *Not now* (D139 §3).
    /// </summary>
    public static string ModeFor(UpdateRequest? request, string? staged) => UpdateRequests.ModeFor(request, staged);

    /// <summary>
    /// The config a draining look plans with (D139 §2): every drivable repository held, so no quest starts, resumes or
    /// carries on and no answered park goes on; and no intake, which a hold does not reach. The cap is left, since a
    /// workspace's set-up plan reads it to publish, which starts nothing.
    /// </summary>
    public static DriverConfig Drained(DriverConfig config) => config with
    {
        Holds = [.. config.Holds, .. config.Drivable.Where(repository => !config.Holds.Contains(repository, StringComparer.OrdinalIgnoreCase))],
        IntakeAdapter = null,
    };

    /// <summary>
    /// What a draining look said of each quest, with the drain's hold said as the drain's: a <see cref="StartVerdict.Held"/>
    /// in a repository the person did not hold becomes <see cref="StartVerdict.Blocked"/> with <see cref="HoldReason"/>. A
    /// hold the person made keeps its own sentence; nothing else is touched.
    /// </summary>
    public static IReadOnlyList<Consideration> HeldFor(IReadOnlyList<Consideration> considered, DriverConfig config) =>
        considered
            .Select(consideration =>
                consideration.Verdict == StartVerdict.Held
                && !config.Holds.Contains(consideration.Quest.To, StringComparer.OrdinalIgnoreCase)
                    ? consideration with { Verdict = StartVerdict.Blocked, Reason = HoldReason }
                    : consideration)
            .ToList();

    /// <summary>Whether a consideration is the drain's hold: what the tick marks <c>forUpdate</c> for the page.</summary>
    public static bool IsHeldForUpdate(Consideration consideration) =>
        consideration.Verdict == StartVerdict.Blocked && string.Equals(consideration.Reason, HoldReason, StringComparison.Ordinal);

    /// <summary>Whether the work allows the update (D139 §2): no driven session running and no conversation's turn in flight.</summary>
    public static bool Idle(int driven, int turns) => driven <= 0 && turns <= 0;
}
