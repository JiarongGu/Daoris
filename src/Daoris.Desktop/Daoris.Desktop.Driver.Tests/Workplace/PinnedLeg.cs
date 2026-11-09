namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The git library's proofs run once more with the MinGit the toolchain pins as the only git on the host's <c>PATH</c>, so the product's
/// own starts and the fixtures' are both that git. Its settings file (<c>git.pinned.process.runsettings</c>) sets <see cref="Switch"/>.
/// </summary>
/// <remarks>
/// Where the pin cannot be had (a platform without one, a fetch that fails) <see cref="Absent"/> says why and every
/// <see cref="GitLegFactAttribute"/> is skipped with it: the runner's git must never pass for the pinned one.
/// </remarks>
internal static class PinnedLeg
{
    internal const string Switch = "DAORIS_PINNED_GIT_LEG";

    /// <summary>Whether this host is the pinned leg's.</summary>
    internal static bool Requested { get; private set; }

    /// <summary>Why the pinned git is not here when the leg needs it; null otherwise.</summary>
    internal static string? Absent { get; private set; }

    /// <summary>
    /// Read the switch and fetch the pin as the host loads; the folder to put first on <c>PATH</c> (the host sets it, the one source the
    /// environment census allows), or null outside the leg or without the pin.
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
