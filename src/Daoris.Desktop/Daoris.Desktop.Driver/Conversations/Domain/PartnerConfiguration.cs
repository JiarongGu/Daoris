using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The effective configuration a partner's start hands its agent (COAGENT1b, D199 point 4; the co-agent design §3.3): what a measurement
/// of its confinement (M2) is taken in, and what admission compares with a measured row. Two starts that hand the same configuration
/// read the same, whichever piece and copy: the copy is named by its shape, never its path.
/// </summary>
/// <param name="Agent">The adapter's name.</param>
/// <param name="HarnessVersion">The harness's version the account's walk resolved, or null where none was read (which no row holds).</param>
/// <param name="Posture">The posture handed, in the harness's own words: the adapter's, since a partner's start names no access setup.</param>
/// <param name="WritableRoots">
/// What the start hands it to write: <see cref="PartnerConfigurations.CopyAlone"/>, its working folder being its copy and nothing else
/// named writable.
/// </param>
/// <param name="Network">
/// What the start hands of network and loopback reach: <see cref="PartnerConfigurations.HarnessDefault"/>, since Daoris names none and the
/// harness's own default stands, which is what M2 measures.
/// </param>
/// <param name="System">The operating system and its version, as this process reads them.</param>
/// <param name="SettingsDigest">
/// The SHA-256 of what Daoris passes beside the instruction, the copy's path replaced by its shape: the posture, the rules (where its
/// agent takes them), the classifier's hard denials and whether the tree guard holds it.
/// </param>
public sealed record PartnerConfiguration(
    string Agent, string? HarnessVersion, string? Posture, string WritableRoots, string Network, string System, string SettingsDigest);

/// <summary>A partner's effective configuration, made from what its start hands (the co-agent design §3.3, §8.5).</summary>
public static class PartnerConfigurations
{
    /// <summary>Its copy is its working folder and the one thing it is handed to write.</summary>
    public const string CopyAlone = "copy-alone";

    /// <summary>Daoris hands no network setting; the harness's default stands.</summary>
    public const string HarnessDefault = "harness-default";

    /// <summary>What the copy's path reads as in the digest, so every piece's copy digests alike.</summary>
    private const string CopyShape = "<copy>";

    /// <summary>
    /// The configuration a partner's start on <paramref name="adapter"/> hands: its posture, its copy alone to write, no network named,
    /// this machine's system, and the digest of <paramref name="rules"/> and <paramref name="hardDeny"/> as handed, which are empty where
    /// its agent takes no rules.
    /// </summary>
    public static PartnerConfiguration Of(
        ISessionAdapter adapter, string? harnessVersion, string copy, RuleLists rules, IReadOnlyList<string> hardDeny, bool guarded) =>
        new(adapter.Name, harnessVersion, adapter.AcpPosture, CopyAlone, HarnessDefault, System(),
            Digest(adapter.AcpPosture, copy, adapter.TakesSettings ? rules : RuleLists.Empty, adapter.TakesSettings ? hardDeny : [], adapter.TakesSettings && guarded));

    /// <summary>
    /// The digest of what Daoris passes: each list in ordinal order, so the same rules in another order digest alike, and the copy's
    /// path, in either slash, read as its shape.
    /// </summary>
    public static string Digest(string? posture, string copy, RuleLists rules, IReadOnlyList<string> hardDeny, bool guarded)
    {
        var text = new StringBuilder();
        text.Append("posture=").Append(posture ?? "").Append('\n');
        text.Append("guard=").Append(guarded ? "copy" : "none").Append('\n');
        void List(string name, IEnumerable<string> items)
        {
            foreach (var item in items.Select(each => Shaped(each, copy)).Order(StringComparer.Ordinal)) text.Append(name).Append('=').Append(item).Append('\n');
        }

        List("allow", rules.Allow);
        List("ask", rules.Ask);
        List("deny", rules.Deny);
        List("hard", hardDeny);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static string Shaped(string rule, string copy)
    {
        if (copy.Length == 0) return rule;
        var forward = copy.Replace('\\', '/').TrimEnd('/');
        var back = copy.Replace('/', '\\').TrimEnd('\\');
        return rule.Replace(forward, CopyShape, StringComparison.OrdinalIgnoreCase).Replace(back, CopyShape, StringComparison.OrdinalIgnoreCase);
    }

    private static string System() =>
        $"{(OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux")} {Environment.OSVersion.Version}";
}
