namespace Daoris.Driver;

/// <summary>
/// The keys of <c>driver.json</c> no code of this build writes, taken out (D232 point 3): <c>gitMoved</c>, since the move is read from the doors laid
/// (<see cref="GitMove.Moved"/>), and <c>timeoutMinutes</c>, which bounds no session. The staffing keys and
/// <c>strikes</c> go with their own steps.
/// </summary>
public sealed class DriverKeysStep : IHomeMigrationStep
{
    /// <summary>The keys taken out, by name.</summary>
    public static IReadOnlyList<string> Keys { get; } = ["gitMoved", "timeoutMinutes"];

    public string Name => "driver-keys";

    public void Run(HomeMigrationRun run) =>
        run.ChangeJson(DaorisHome.Entry(run.Home, HomeEntry.DriverConfig), root => Keys.Count(root.Remove) > 0);
}
