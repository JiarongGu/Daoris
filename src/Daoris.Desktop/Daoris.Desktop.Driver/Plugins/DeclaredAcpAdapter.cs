using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// A harness a plugin declared, riding the ACP door (D64 §3): the same door dsh and codex arrive by,
/// configured from a file rather than from Daoris code.
/// </summary>
/// <remarks>
/// <para>The posture is the manifest's, in the harness's own vocabulary, or null when its wire
/// carries none (ACP3) — never a guessed mode. The command resolves like every adapter's: the
/// machine's `commands` row first, then the declaration.</para>
///
/// <para>The toolchain rows are what the roster needs to show it beside the built-in harnesses: the
/// binary is the command's first word, so "is it installed" is asked of the thing that will run.</para>
///
/// <para><b>It starts nothing and reads nothing</b> (STRUCT3a): it says how the agents' door
/// (<see cref="ISessionAdapter"/>) runs what a plugin declares. The adapter set (Agents) builds one per declared harness,
/// and reaches Plugins through it.</para>
/// </remarks>
public sealed class DeclaredAcpAdapter(PluginHarness harness, string plugin) : ISessionAdapter
{
    public string Name => harness.Name;

    /// <summary>The plugin it came from — what the roster shows beside it, and nothing a session record carries.</summary>
    public string Plugin => plugin;

    public SessionWire Wire => SessionWire.Acp;

    public string? AcpPosture => harness.Posture;

    public bool Interactive => true;

    public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command)
    {
        var resolved = Resolve(command);
        return Spawning.InRoot(target, resolved[0], resolved.Skip(1), redirectInput: true);
    }

    public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command)
    {
        var resolved = Resolve(command);
        return Spawning.ChatInRoot(target, resolved[0], resolved.Skip(1));
    }

    public HarnessToolchain? Toolchain => new(
        Binary: [harness.Command[0]],
        VersionArguments: harness.VersionArguments ?? [],
        ProfileVariable: harness.ProfileVariable,
        Install: harness.Install,
        Package: harness.Package,
        AccountOf: harness.AccountOf,
        // No version question declared: presence is asked of the file or PATH, never by running it.
        ProbeByPresence: harness.VersionArguments is null,
        // What the plugin says its tool is and who makes it (XAGENT1b), so its agent's family reads as a built-in one's.
        Product: harness.Product,
        Maker: harness.Maker,
        // OWNSPACE1c (D175): the plugin's word that its agent's own home is nobody's; otherwise a variable means a Daoris account.
        RunsInOwnHome: harness.OwnHome);

    private IReadOnlyList<string> Resolve(IReadOnlyList<string>? command) =>
        command is { Count: > 0 } ? command : harness.Command;
}
