using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// <b>dsh over the protocol door</b> (ACP3/D53): a configuration of the door, not a second seam.
/// </summary>
/// <remarks>
/// <para><b>A profile IS a home here.</b> <c>dsh --profile &lt;name&gt;</c> boots a directory under
/// <c>$DSH_HOME/profiles</c>, so the one variable isolates credentials, settings and sessions
/// together — which is why <c>DSH_HOME</c> is the account seam and why what Daoris writes for this
/// harness goes inside a directory it created rather than beside one it did not.</para>
///
/// <para>🔴 <b>Its wire carries no posture.</b> Observed at 0.1.6-alpha.2: <c>session/new</c> answers
/// with <c>sessionId</c> and <c>configOptions</c> and no <c>modes</c> key — and the one config option
/// is the model catalogue, which Daoris never turns itself (D24; since D98 a person may, for one
/// conversation). So the wire carries no posture to set. It is set in the environment instead, below.</para>
///
/// <para><b>No login question</b> (SES3): dsh has no account to be logged out of, and only a definite
/// *out* refuses — so `unknown` is permissive and a session starts.</para>
///
/// <para><b>No model is named</b> (D24): which model answers is the profile's own `settings.yaml`.</para>
/// </remarks>
public sealed class DshAdapter : ISessionAdapter
{
    public string Name => "dsh";

    public SessionWire Wire => SessionWire.Acp;

    /// <summary>
    /// The profile dsh boots for the automation surface. Its ACP server is <b>automation-only by
    /// their own decision</b> — they removed it as an editor UI — which is exactly the half Daoris
    /// wants and none of the product half D53 declined.
    /// </summary>
    public const string Profile = "acp";

    /// <summary>
    /// dsh's permission posture, as an environment variable read at boot.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>`workspace-write` is D37 in dsh's vocabulary</b>, read from its own shipped bundle: it
    /// feeds both the sandbox policy's mode and the approval policy, which derives to <c>ask</c> for
    /// every value except <c>danger-full-access</c>, where it becomes <c>never</c>. Writes inside the
    /// workspace are sandbox-legal and proceed without asking; anything escalating past the sandbox
    /// asks, which over ACP arrives as <c>session/request_permission</c> and is refused by
    /// construction (D52). Failing closed on escalation is the behaviour, not a limitation.
    ///
    /// <para>It is stated even though it is <b>also dsh's default</b>. A posture that happens to match
    /// somebody else's default is not one Daoris has set, and the default is theirs to change.</para>
    /// </remarks>
    public const string PermissionVariable = "DSH_PERMISSION_MODE";

    /// <summary>The value that expresses D37 here. Never <c>danger-full-access</c>, which is approvals off.</summary>
    public const string Posture = "workspace-write";

    public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command)
    {
        var resolved = Resolve(command);
        var info = Spawning.InRoot(
            target, resolved[0], resolved.Skip(1).Concat(["--profile", Profile]), redirectInput: true);

        // Set HERE rather than in the driver's one line, because this posture is genuinely this
        // harness's own mechanism — the other two protocol adapters express the same boundary as a
        // mode on the wire, and a driver that applied one mechanism to all three would set nothing
        // for two of them and report success.
        info.Environment[PermissionVariable] = Posture;

        return info;
    }

    /// <summary>
    /// Pinned exact, and <b>vendored nowhere</b>: 561 MB per machine, and a harness's own packaging is
    /// its own problem — but the version the toolchain installs is asserted, not assumed (D53).
    /// </summary>
    public HarnessToolchain? Toolchain => new(
        // The name its maker publishes it under (AGENTS2): `dsh` beside DeepSeek still read as no DeepSeek agent. The
        // binary and the adapter's Daoris name stay `dsh`, which is what a terminal types.
        Product: "DeepSeek Harness",
        Mark: "DH",
        Maker: "DeepSeek",
        Binary: ["dsh"],
        // `-V, --version` — verified against the installed CLI, which printed its exact version.
        VersionArguments: ["--version"],
        ProfileVariable: "DSH_HOME",
        Install: ["npm", "install", "-g", "@deepseek-ai/dsh"],
        // No login flow and no login question: there is no account here to be out of.
        Package: "@deepseek-ai/dsh");

    private IReadOnlyList<string> Resolve(IReadOnlyList<string>? command) =>
        command is { Count: > 0 } ? command : Toolchain!.Binary;
}
