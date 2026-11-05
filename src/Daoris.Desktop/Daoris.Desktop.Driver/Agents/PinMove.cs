namespace Daoris.Driver;

/// <summary>What moving a pin to the newest release comes to (USE1a), decided once for the window's update and the terminal's.</summary>
internal enum PinMoveKind
{
    /// <summary>The newest is the pin and is installed: nothing is fetched and the pin stays.</summary>
    Stays,

    /// <summary>The pin is newer than the newest release: nothing is fetched and the pin stays, for a pin never moves backwards.</summary>
    Behind,

    /// <summary>The newest is the pin and is not installed here: it is installed and pinned again.</summary>
    Reinstall,

    /// <summary>The newest is ahead of the pin: it is installed and pinned.</summary>
    Move,
}

/// <summary>
/// The decisions the pin's install and its update's move share between the window's actions and the terminal's <c>agent pin</c> and
/// <c>agent update</c> (AGENTPROVIDER1 row f): what a move comes to, and the command a package pin runs. Each surface keeps its own words
/// and the way it runs the command (the terminal at its console, the window redirected).
/// </summary>
internal static class PinMove
{
    /// <summary>What moving <paramref name="pin"/> to <paramref name="newest"/> comes to, where <paramref name="installed"/> says the pin's version is on disk.</summary>
    public static PinMoveKind Decide(string newest, string pin, bool installed)
    {
        if (newest == pin && installed) return PinMoveKind.Stays;
        if (HarnessActions.CompareReleases(newest, pin) < 0) return PinMoveKind.Behind;
        return newest == pin ? PinMoveKind.Reinstall : PinMoveKind.Move;
    }

    /// <summary>
    /// The command a package pin runs: <paramref name="npm"/> (the tools' npm by default) installing the package at the version under
    /// the folder Daoris keeps it in, its prefix named so what it installs runs on the tools' node (TOOLS5, D121 §2.7).
    /// </summary>
    /// <exception cref="DriverException">The tools' way cannot run.</exception>
    public static IReadOnlyList<string> PackageCommand(
        string home, string harness, string package, string version, IReadOnlyList<string> npm) =>
        HarnessActions.ThroughTools(home, [.. npm, "install", "--prefix", HarnessSettings.ManagedHome(home, harness, version), $"{package}@{version}"]);
}
