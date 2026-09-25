using System.Net;

namespace Daoris.Knowledge;

/// <summary>One service, two modes, chosen by configuration (D21) — never by build.</summary>
public enum ServiceMode
{
    /// <summary>The default, silently: no account, no credential model, loopback trust.</summary>
    Local,

    /// <summary>The team deployment (D47 §3): every route gated by minted keys, machine paths never served.</summary>
    Shared,
}

/// <summary>
/// The deployment judgement (D47 §3): which mode the configuration names, and whether this host may
/// start at all with the addresses it was given.
/// </summary>
/// <remarks>
/// The refusal is the sibling's fail-safe inversion (service design §5): a host bound beyond loopback
/// without shared mode's credential model does not warn — it does not start. The insecure shape is
/// unreachable where it would matter, which is the only place a warning is ever needed.
/// </remarks>
public static class Access
{
    public const string ModeVariable = "DAORIS_MODE";

    /// <summary>The circle a SHARED deployment serves (D48 §5). Meaningless on a local host, which
    /// holds every workspace the person wired — so setting it there is refused, not ignored.</summary>
    public const string WorkspaceVariable = "DAORIS_WORKSPACE";

    /// <summary>Absence means local, silently (D21). An unknown value errors naming what exists (D23).</summary>
    public static (ServiceMode Mode, string? Error) ParseMode(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "local" => (ServiceMode.Local, null),
            "shared" => (ServiceMode.Shared, null),
            var unknown => (ServiceMode.Local, $"Unknown {ModeVariable} '{unknown}' — one of: local, shared."),
        };

    /// <summary>
    /// Why this host must not start — or null when it may. <paramref name="urls"/> is the bind list as
    /// configured (semicolon-separated). There are exactly two trust shapes (D47 §7, as amended):
    /// local trusts the loopback and may bind nothing else; shared gates everything with minted keys
    /// and may bind anywhere.
    /// </summary>
    /// <param name="endpoints">
    /// The addresses Kestrel's own endpoint configuration names (`Kestrel:Endpoints:*:Url`). They
    /// OVERRIDE <paramref name="urls"/> at bind time, so a judgement that read only the urls passed a
    /// host that then bound wherever an environment variable, an argument or an appsettings.json said
    /// (REV3). Judged by the same rule.
    /// </param>
    public static string? RefuseStartup(ServiceMode mode, string urls, IEnumerable<string>? endpoints = null)
    {
        if (mode == ServiceMode.Local)
        {
            var beyond = urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Concat(endpoints ?? [])
                .FirstOrDefault(url => !IsLoopbackUrl(url));
            if (beyond is not null)
            {
                return $"Refusing to bind beyond loopback in local mode: {beyond}. Local trust is a "
                     + "loopback deployment (D21); set DAORIS_MODE=shared and mint keys to serve a network.";
            }
        }

        return null;
    }

    /// <summary>
    /// Which workspace this deployment IS — one circle for a shared host, none for a local one.
    /// </summary>
    /// <remarks>
    /// <para>Silence is <see cref="Workspaces.Default"/> for a shared host, because silence is
    /// `default` everywhere (D48 §2): a team that never named a circle runs exactly as it did before
    /// workspaces existed.</para>
    ///
    /// <para>A LOCAL host naming one is refused rather than ignored. A local deployment holds every
    /// circle the person wired, so an identity there would be a claim it cannot honour — and a
    /// parsed-and-unused input is a claim. This is the same fail-safe inversion as the loopback rule:
    /// the configuration that cannot mean what it says does not start.</para>
    /// </remarks>
    public static (string? Workspace, string? Error) ParseWorkspace(ServiceMode mode, string? value)
    {
        if (mode == ServiceMode.Local)
        {
            return string.IsNullOrWhiteSpace(value)
                ? (null, null)
                : (null,
                    $"{WorkspaceVariable} is a shared deployment's identity (D48 §5), and this host is local — "
                    + "a local deployment holds every workspace this machine wired, so it cannot be one of them. "
                    + $"Unset {WorkspaceVariable}, or set {ModeVariable}=shared to run the workspace's server.");
        }

        return (Workspaces.Normalize(value), null);
    }

    /// <summary>
    /// Why a deployment refuses a registration that declares a workspace other than its own — or null
    /// when it may take it.
    /// </summary>
    /// <remarks>
    /// One shared deployment serves one workspace (D48 §5), so a row declaring another is not a
    /// permission failure but a message delivered to the wrong building. The sentence names BOTH
    /// sides: a "no" that does not say which side is where leaves the person with the question they
    /// should be deciding. Silence is not a declaration — an ordinary registration says nothing about
    /// wiring, and the receiving deployment's own identity decides where it lands (D48 §2).
    /// </remarks>
    public static string? RefuseForeignWorkspace(string? hostWorkspace, string repository, string? stated) =>
        hostWorkspace is null || string.IsNullOrWhiteSpace(stated) || Workspaces.Same(hostWorkspace, stated)
            ? null
            : $"This deployment serves workspace `{hostWorkspace}`, and `{repository}` arrived declaring "
              + $"`{Workspaces.Normalize(stated)}` — one shared deployment serves one workspace, because a "
              + "sharing boundary inside one store is where a scoping bug becomes a disclosure. Wire it to "
              + $"`{hostWorkspace}` on that machine, or point `{Workspaces.Normalize(stated)}` at its own deployment.";

    /// <summary>
    /// Why a re-wire is refused — or null when it may move the row to <paramref name="stated"/>.
    /// </summary>
    /// <remarks>
    /// The registration door's boundary, plus one rule of its own: a re-wire must NAME a workspace.
    /// Silence is right for a registration, where the receiving host's wiring decides. For a re-wire
    /// it became `default`, which on a shared host moved the row out of the one circle it serves (REV3).
    /// </remarks>
    public static string? RefuseRewire(string? hostWorkspace, string repository, string? stated) =>
        string.IsNullOrWhiteSpace(stated)
            ? $"Re-wiring `{repository}` needs the workspace to wire it to, and this request named none."
            : RefuseForeignWorkspace(hostWorkspace, repository, stated);

    /// <summary>
    /// Whether a bind address stays on this machine. Anything unparseable — including Kestrel's `+`
    /// and `*` wildcards, which bind everything — is NOT loopback: when in doubt, the answer that
    /// refuses to serve a network is the safe one.
    /// </summary>
    private static bool IsLoopbackUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)) return false;
        if (string.Equals(parsed.Host, "localhost", StringComparison.OrdinalIgnoreCase)) return true;
        return IPAddress.TryParse(parsed.Host, out var address) && IPAddress.IsLoopback(address);
    }
}
