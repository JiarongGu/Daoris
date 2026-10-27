namespace Daoris.Driver;

/// <summary>
/// How long the landing waits on a plugin: pure vocabulary, which <see cref="LandingPlugins"/> keeps under its own name for the readers
/// above the workplace, and which the hold on a tree or branch is reckoned past (<see cref="LandingHolds.Wait"/>).
/// </summary>
internal static class LandingPatience
{
    /// <summary>
    /// How long a plugin has to push and open the pull request. Longer than a decision's ten seconds: a
    /// push and a platform's API are network round trips, and the person pressed and is waiting.
    /// </summary>
    internal static readonly TimeSpan Plugin = TimeSpan.FromMinutes(2);
}
