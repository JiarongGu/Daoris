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
    /// configured (semicolon-separated); <paramref name="singleKeyConfigured"/> is whether the
    /// local-trust write key is set, which shared mode refuses: one credential model per deployment,
    /// because two would drift and the weaker one would win.
    /// </summary>
    public static string? RefuseStartup(ServiceMode mode, string urls, bool singleKeyConfigured)
    {
        if (mode == ServiceMode.Shared && singleKeyConfigured)
        {
            return "Shared mode uses minted keys (`keys mint`), not DAORIS_SERVICE_KEY — unset it. "
                 + "One credential model per deployment.";
        }

        if (mode == ServiceMode.Local)
        {
            var beyond = urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
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
    /// Whether a bind address stays on this machine. Anything unparseable — including Kestrel's `+`
    /// and `*` wildcards, which bind everything — is NOT loopback: when in doubt, the answer that
    /// refuses to serve a network is the safe one.
    /// </summary>
    public static bool IsLoopbackUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)) return false;
        if (string.Equals(parsed.Host, "localhost", StringComparison.OrdinalIgnoreCase)) return true;
        return IPAddress.TryParse(parsed.Host, out var address) && IPAddress.IsLoopback(address);
    }
}
