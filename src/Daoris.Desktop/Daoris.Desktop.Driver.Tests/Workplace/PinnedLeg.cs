namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The pinned leg of the git library's proofs (GITLIB1d): the same classes run once more with the MinGit the toolchain pins as the
/// only git on the host's <c>PATH</c>, so the product's own starts and the fixtures' find it beside each other. The leg is asked by
/// its settings file (<c>git.pinned.process.runsettings</c>), which sets <see cref="Switch"/>.
/// </summary>
/// <remarks>
/// Where the pin cannot be had (a platform without one, a fetch that fails) <see cref="Absent"/> says why and every
/// <see cref="GitLegFactAttribute"/> is skipped with it, never run on the runner's git and called the pinned leg's.
/// </remarks>
internal static class PinnedLeg
{
    internal const string Switch = "DAORIS_PINNED_GIT_LEG";

    /// <summary>Whether this host was started as the pinned leg.</summary>
    internal static bool Requested { get; private set; }

    /// <summary>Why the pinned git is not here, where the leg was asked and it cannot run; null otherwise.</summary>
    internal static string? Absent { get; private set; }

    /// <summary>
    /// Read the switch and fetch the pin, once, as the host loads; the folder to put first on <c>PATH</c> (the host sets it, being the one
    /// source that does), or null where the leg was not asked or the pin is absent.
    /// </summary>
    internal static string? Apply()
    {
        if (Environment.GetEnvironmentVariable(Switch) != "1") return null;
        Requested = true;
        try
        {
            return Path.GetDirectoryName(PinnedMinGit.Exe);
        }
        catch (Exception error)
        {
            // The host is loading: a throw here would fail every test rather than skip those that need the pin.
            Absent = $"{error.GetType().Name}: {error.Message}";
            return null;
        }
    }
}

/// <summary>A fact that runs in every leg and is skipped, saying why, in the pinned leg where the pinned git is absent.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class GitLegFactAttribute : FactAttribute
{
    public GitLegFactAttribute()
    {
        if (PinnedLeg.Absent is { } why) Skip = $"the pinned MinGit leg has no pinned git on this machine ({why})";
    }
}
