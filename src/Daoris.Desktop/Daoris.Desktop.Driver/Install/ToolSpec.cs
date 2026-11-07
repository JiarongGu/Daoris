namespace Daoris.Driver;

/// <summary>
/// A program Daoris manages: its binary, how to ask its version, install and update
/// it, the package or release channel a pin is fetched from (<see cref="IReleaseSource"/> names the channel), the environment a pinned
/// copy is run in, and whether it is present by a file look alone. Install owns the shape and each provider and door states its own,
/// so a pin, an update and a probe read one description of a program, never a second copy of its facts.
/// </summary>
/// <param name="Binary">The command that starts it: its program and any leading arguments.</param>
/// <param name="VersionArguments">The arguments that ask its version; none for a program no version question may be put to.</param>
/// <param name="Install">The command that installs it onto the machine, or null where Daoris has no sanctioned way.</param>
/// <param name="UpdateArguments">The arguments of its own updater, or null where it updates itself or its package manager does.</param>
/// <param name="Package">The npm package a pin is fetched as, or null.</param>
/// <param name="Channel">The name of the <see cref="IReleaseSource"/> a pin is fetched from, or null.</param>
/// <param name="PinnedEnvironment">The variables a pinned copy runs with, or null.</param>
/// <param name="ProbeByPresence">Whether it is found by a file look, since asking it anything would stall it.</param>
public sealed record ToolSpec(
    IReadOnlyList<string> Binary,
    IReadOnlyList<string> VersionArguments,
    IReadOnlyList<string>? Install,
    IReadOnlyList<string>? UpdateArguments,
    string? Package,
    string? Channel,
    IReadOnlyDictionary<string, string>? PinnedEnvironment,
    bool ProbeByPresence)
{
    /// <summary>The command this program actually runs as: the machine's configured one, or the declared one.</summary>
    public IReadOnlyList<string> Command(IReadOnlyList<string>? configured) =>
        configured is { Count: > 0 } ? configured : Binary;
}
