namespace Daoris.Driver;

/// <summary>
/// The run's journal of its acts, <c>workflows/acts/&lt;key&gt;.json</c>, written by the pass alone (<see cref="StepActs"/>) from the
/// sessions' rounds (<see cref="PluginActRounds"/>), in their shape.
/// </summary>
public static class StepActFiles
{
    public static string JournalPath(string home, string unit) => Path.Combine(DaorisHome.Entry(home, HomeEntry.Workflows, "acts"), NodeRecordFiles.KeyOf(unit) + ".json");

    /// <summary>The acts journaled on the run of <paramref name="unit"/>, oldest first.</summary>
    public static IReadOnlyList<StepActRow> Journal(string home, string unit) => PluginActRounds.Read(JournalPath(home, unit));

    /// <summary>Writes the run's journal whole.</summary>
    public static void Keep(string home, string unit, IReadOnlyList<StepActRow> rows) => PluginActRounds.Write(JournalPath(home, unit), rows);
}
