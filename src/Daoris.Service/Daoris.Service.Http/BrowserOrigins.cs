using System.Net;
using Daoris.Knowledge;

namespace Daoris.Knowledge.Http;

/// <summary>
/// Which pages a browser may call this host from, and the gate that keeps every other page off its writes (ORIGIN1);
/// and the names a local host answers under, which keep a page whose name was pointed at the loopback off it (ORIGIN2).
/// </summary>
/// <remarks>
/// <para><b>Why a gate, and not CORS alone.</b> CORS keeps a page from <i>reading</i> an answer. A <c>POST</c> with no
/// body, a text body or a form body is a simple request: a browser sends it without asking first, so the route runs
/// whatever CORS later says of its answer. A local host trusts the loopback (D47 §7, as amended), and a browser on this
/// machine is on the loopback, so before this gate any page on any site could press a door that binds no body: the
/// yes to a departure at <c>/accept</c> among them.</para>
///
/// <para><b>One list.</b> <see cref="Allowed"/> is what the CORS policy allows and what this gate allows: the
/// development UI's origin when <c>DAORIS_WEB_ORIGIN</c> names one, and on a local host the shell's page
/// (<see cref="DesktopPage.Origin"/>, D92). A local host also answers its own page, which it serves and which calls it
/// on its own origin (<see cref="IsOwnOrigin"/>).</para>
///
/// <para><b>The rule</b> (<see cref="Refused"/>), on <c>POST</c>, <c>PUT</c>, <c>PATCH</c> and <c>DELETE</c> only:</para>
/// <list type="bullet">
/// <item>An <c>Origin</c> decides when there is one: allowed, or the host's own, and the write is answered; any other,
/// <c>null</c> included, and it is refused. A browser sends <c>Origin</c> with every write, and a page cannot forge
/// it.</item>
/// <item>With no <c>Origin</c>, <c>Sec-Fetch-Site</c> decides: <c>cross-site</c> and <c>same-site</c> are refused, and
/// anything else is answered. <b><c>same-site</c> is refused</b> because on the loopback it means another port of
/// this machine: any local server's page, none of them Daoris's. Daoris's own pages are named by their origin, and a
/// browser that says which site a request came from also names its origin on a write, so a <c>same-site</c> write
/// with no origin has nothing that makes it ours.</item>
/// <item>An allowed origin is answered whatever <c>Sec-Fetch-Site</c> says, since <b>the shell's page is another site
/// by construction</b>: it lives on its engine's <c>https</c> app origin and calls the loopback over <c>http</c>,
/// which a browser counts <c>cross-site</c>. Refusing every <c>cross-site</c> write would refuse every press in the
/// window.</item>
/// <item>A request with neither header is a program, not a page (the CLI, the driver, the shell's modules, a
/// rehearsal), and is answered as before. A program sets any header it likes, so this gate tells pages apart and
/// never people from agents: that is PERSONDOOR1's (D156 §8, <i>Trusting the Origin header</i>).</item>
/// </list>
///
/// <para><b>The host's own origin</b> is the one the request itself names, <c>{scheme}://{Host}</c>, and only under a
/// loopback name. A name rebound to the loopback by a website's DNS would otherwise make that website's page its own
/// origin here.</para>
///
/// <para><b>The name's gate</b> (ORIGIN2, <see cref="HostCode"/>): a local host refuses every request, reads included,
/// whose <c>Host</c> is not a loopback name (<see cref="IsLoopbackName"/>), before CORS or this gate is asked. A rebound
/// page is same-origin with the host, so neither could tell it from the host's own page, and its reads would be
/// answered: the index, the quests, the roots a loopback caller is given. A shared host has no such gate: it is
/// reached by its deployment's own name, which this build cannot know, and its key gate keeps a page off it.</para>
///
/// <para><b>A shared host keeps the gate too</b>, in front of its key gate. A browser reaches it on the network, and
/// today a bearer key is what stops a page elsewhere, since a browser never sends one on its own; a browser credential
/// the remote gains later, with person sign-in (D47), would not be. Its callers are machines that send no origin, so
/// the gate costs them nothing.</para>
/// </remarks>
public static class BrowserOrigins
{
    /// <summary>The refusal's code, which a client reads instead of the sentence.</summary>
    public const string Code = "cross-site";

    /// <summary>The refusal's sentence.</summary>
    public const string Sentence =
        "This request came from a web page on another site, and only Daoris's own page or a program may change what "
        + "this service keeps. Nothing was changed.";

    /// <summary>The machine log's event for a refusal: one warning per request, by its route's pattern.</summary>
    public const string Event = "origin.refused";

    /// <summary>The words a browser's <c>Sec-Fetch-Site</c> may say; the log writes one of them, or nothing.</summary>
    private static readonly string[] Sites = ["cross-site", "same-site", "same-origin", "none"];

    /// <summary>
    /// The origins a page may call this host from: the CORS policy's list and this gate's, so there is one. Named, never
    /// wildcarded, since a wildcard would quietly make a local-only index readable by any page the browser has open.
    /// </summary>
    public static IReadOnlyList<string> Allowed(ServiceMode mode, string? webOrigin)
    {
        var origins = new List<string>();
        if (!string.IsNullOrWhiteSpace(webOrigin)) origins.Add(webOrigin);
        // No website can present this origin: only the shell's engine serves that host name (D92).
        if (mode == ServiceMode.Local) origins.Add(DesktopPage.Origin);
        return origins;
    }

    /// <summary>Whether <paramref name="method"/> changes something: the methods a gate on writes judges.</summary>
    public static bool IsWrite(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

    /// <summary>
    /// Which header refuses <paramref name="request"/>: <c>origin</c> or <c>site</c>, or null when it is answered.
    /// </summary>
    /// <param name="ownOrigin">Whether the host's own origin is allowed: a host that serves its page.</param>
    public static string? Refused(HttpRequest request, IReadOnlyList<string> allowed, bool ownOrigin)
    {
        if (!IsWrite(request.Method)) return null;

        if (request.Headers.Origin.Count > 0)
        {
            var origin = request.Headers.Origin.ToString();
            if (allowed.Contains(origin, StringComparer.OrdinalIgnoreCase)) return null;
            if (ownOrigin && IsOwnOrigin(request, origin)) return null;
            return "origin";
        }

        return SiteOf(request) is "cross-site" or "same-site" ? "site" : null;
    }

    /// <summary>What the browser said of the request's site, when it said one of its own words.</summary>
    public static string? SiteOf(HttpRequest request)
    {
        var said = request.Headers["Sec-Fetch-Site"].ToString();
        return Sites.FirstOrDefault(site => string.Equals(site, said, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Whether <paramref name="origin"/> is the address the request was sent to, under a loopback name: the page this
    /// host served, calling it on its own origin.
    /// </summary>
    public static bool IsOwnOrigin(HttpRequest request, string origin) =>
        request.Host.HasValue && IsLoopbackName(request.Host.Host)
        && string.Equals(origin, $"{request.Scheme}://{request.Host.Value}", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether <paramref name="name"/>, a request's host without its port, is one of this machine's loopback names:
    /// <c>localhost</c> in any case, or a loopback address (127/8, <c>[::1]</c>). The one definition the host's own
    /// origin and the name's gate both read (ORIGIN1, ORIGIN2). A name under <c>.localhost</c> is not one: no caller
    /// uses one, and the shell's page names <c>daoris.localhost</c> as its origin, never as the host it calls.
    /// </summary>
    public static bool IsLoopbackName(string name)
    {
        var bare = name.Trim('[', ']');
        return string.Equals(bare, "localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(bare, out var address) && IPAddress.IsLoopback(address));
    }

    /// <summary>The name's refusal's code (ORIGIN2), which a client reads instead of the sentence.</summary>
    public const string HostCode = "host";

    /// <summary>The name's refusal's sentence (ORIGIN2).</summary>
    public const string HostSentence =
        "This request named this service by a name that is not this machine's own. A local service answers only "
        + "`localhost`, `127.0.0.1` or `[::1]`, so that a website whose name was pointed at this machine reads nothing "
        + "here. Nothing was read or changed.";

    /// <summary>The machine log's event for the name's refusal: one warning per request, by its route's pattern.</summary>
    public const string HostEvent = "host.refused";
}
