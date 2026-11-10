using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>The families of the person's own tool home a start is handed, each the person's to switch off (<see cref="OwnHomeDefaults"/>).</summary>
[Flags]
public enum OwnFamilies
{
    None = 0,

    /// <summary>Its skills, subagent definitions and commands.</summary>
    Skills = 1,

    /// <summary>Its user-scope servers.</summary>
    Servers = 2,

    /// <summary>The plugins it installed and enables.</summary>
    Plugins = 4,
}

/// <summary>
/// Where the person's own run of an agent finds its tool home, and which of it one start is handed (D253 point 7). The person's own
/// run reads the environment the driver inherited and the person's profile, so a provider's layout reads them so too, and never writes
/// there.
/// </summary>
/// <param name="Environment">A variable as the person's own run of the agent reads it.</param>
/// <param name="UserHome">The person's profile folder.</param>
/// <param name="Session">A variable as the session's process holds it: the home of the account it runs in among them.</param>
/// <param name="Families">The families the person leaves on for this start.</param>
public sealed record OwnTools(Func<string, string?> Environment, string UserHome, Func<string, string?> Session, OwnFamilies Families)
{
    /// <summary>
    /// This machine's: the driver's own environment and profile, the session's process as equipped, the person's switches. A scratch
    /// home (<see cref="ScratchSignIns.IsScratch"/>: a test's, a probe's) is handed nothing of the person's, as no real sign-in runs there.
    /// </summary>
    /// <param name="connectorOnly">A pass held to Daoris's connector alone (agents rule 13), which is handed no server of the person's.</param>
    public static OwnTools Machine(string home, ProcessStartInfo info, bool connectorOnly = false) => new(
        System.Environment.GetEnvironmentVariable,
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
        name => info.Environment.TryGetValue(name, out var value) ? value : null,
        ScratchSignIns.IsScratch(home) ? OwnFamilies.None : OwnHomeDefaults.Families(PermissionRules.Load(home), connectorOnly));
}
