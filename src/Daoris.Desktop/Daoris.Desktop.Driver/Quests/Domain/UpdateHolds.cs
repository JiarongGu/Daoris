namespace Daoris.Driver;

/// <summary>
/// The drain's hold over the planner's verdicts (UPDATE1, D139 §2): the work orders' members of <see cref="InstallUpdate"/>, which says
/// the hold's words (<see cref="InstallUpdate.HoldReason"/>) and names no verdict, so every caller keeps <c>InstallUpdate.HeldFor</c> as spelled.
/// </summary>
public static class UpdateHolds
{
    extension(InstallUpdate)
    {
        /// <summary>
        /// What a draining look said of each quest, with the drain's hold said as the drain's: a <see cref="StartVerdict.Held"/>
        /// in a repository the person did not hold becomes <see cref="StartVerdict.Blocked"/> with <see cref="InstallUpdate.HoldReason"/>. A
        /// hold the person made keeps its own sentence; nothing else is touched.
        /// </summary>
        public static IReadOnlyList<Consideration> HeldFor(IReadOnlyList<Consideration> considered, DriverConfig config) =>
            considered
                .Select(consideration =>
                    consideration.Verdict == StartVerdict.Held
                    && !config.Holds.Contains(consideration.Quest.To, StringComparer.OrdinalIgnoreCase)
                        ? consideration with { Verdict = StartVerdict.Blocked, Reason = InstallUpdate.HoldReason }
                        : consideration)
                .ToList();

        /// <summary>Whether a consideration is the drain's hold: what the tick marks <c>forUpdate</c> for the page.</summary>
        public static bool IsHeldForUpdate(Consideration consideration) =>
            consideration.Verdict == StartVerdict.Blocked && string.Equals(consideration.Reason, InstallUpdate.HoldReason, StringComparison.Ordinal);
    }
}
