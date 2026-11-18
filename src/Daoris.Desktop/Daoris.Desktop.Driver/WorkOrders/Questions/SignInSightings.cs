using System.Runtime.CompilerServices;
using Daoris.Ports.Browser;

namespace Daoris.Driver;

/// <summary>
/// What Daoris's browser shows of the sign-ins parks wait on (<see cref="ParkQuestion.Site"/>): a page seen at a sign-in address
/// since the question stood, and later on the site's own host at an address that is no sign-in page, is the sign-in done there.
/// </summary>
/// <remarks>
/// <para><b>Through a sign-in, then back.</b> A page on the site alone proves nothing: the address the person opened stands there
/// before the site sends it to its sign-in, so only a page that went through one counts, kept by its target, which stays while the
/// page navigates. A sign-in on another host (a company's identity provider) counts as the site's own.</para>
/// <para><b>Kept beside the browser</b> (<see cref="Of"/>): the watch builds a driver for each look, and the browser outlives them
/// all, so what one look saw is there for the next. Forgotten as its question goes: answered, or its run opened.</para>
/// <para><b>Words only, no model</b>: an address's host and path are read for the words a sign-in page is named by.</para>
/// </remarks>
internal sealed class SignInSightings
{
    private static readonly ConditionalWeakTable<IInAppBrowser, SignInSightings> Held = new();

    /// <summary>The words a sign-in page's host or path is named by, read with case, hyphens and underscores aside.</summary>
    private static readonly HashSet<string> SignInWords = new(StringComparer.Ordinal)
    {
        "login", "logon", "signin", "auth", "oauth", "oauth2", "authorize", "authenticate", "authentication", "sso", "saml", "saml2",
        "adfs", "idp", "cas",
    };

    private readonly Lock _gate = new();

    /// <summary>The pages seen at a sign-in address since each question stood, by its key.</summary>
    private readonly Dictionary<string, HashSet<string>> _through = new(StringComparer.Ordinal);

    /// <summary>The sightings kept beside <paramref name="browser"/>, made at its first look.</summary>
    public static SignInSightings Of(IInAppBrowser browser) => Held.GetValue(browser, _ => new SignInSightings());

    /// <summary>The producer's key of the word that settles <paramref name="question"/>: one word for one question, whatever looks see it.</summary>
    public static string Key(ParkQuestion question) => $"sign-in:{question.Session}:{question.Id}";

    /// <summary>Daoris's word to the park whose sign-in the browser saw: what its next turn reads.</summary>
    public static string Word(ParkQuestion question) =>
        $"Daoris's browser saw the person sign in at {HostOf(question.Site) ?? question.Site}: the sign-in your question `#{question.Id}` "
        + "waited on is done. Go on with the work from where it waited, through Daoris's browser as before.";

    /// <summary>
    /// One reading of the browser against the questions waiting on a sign-in: those it shows done, each once its page went through a
    /// sign-in address and came back to its site. A question no longer waiting is forgotten.
    /// </summary>
    public IReadOnlyList<ParkQuestion> Signed(IReadOnlyList<ParkQuestion> waiting, IReadOnlyList<BrowserPage> pages)
    {
        var signing = waiting.Where(question => HostOf(question.Site) is not null).ToList();
        var signed = new List<ParkQuestion>();
        lock (_gate)
        {
            foreach (var gone in _through.Keys.Except(signing.Select(Key), StringComparer.Ordinal).ToList()) _through.Remove(gone);
            foreach (var question in signing)
            {
                var key = Key(question);
                if (!_through.TryGetValue(key, out var through)) _through[key] = through = new HashSet<string>(StringComparer.Ordinal);
                var back = pages.Any(page => through.Contains(page.Target) && !SignInAddress(page.Address) && OnSite(page.Address, question.Site!));
                foreach (var page in pages.Where(page => SignInAddress(page.Address))) through.Add(page.Target);
                if (back) signed.Add(question);
            }
        }

        return signed;
    }

    /// <summary>Whether an address is a sign-in page's: its host or its path names a sign-in (<c>login</c>, <c>sign-in</c>, <c>oauth2</c>, <c>adfs</c>).</summary>
    public static bool SignInAddress(string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Host.Length == 0) return false;
        var words = (uri.Host + "/" + uri.AbsolutePath).ToLowerInvariant().Replace("-", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal);
        return words.Split(['.', '/'], StringSplitOptions.RemoveEmptyEntries).Any(SignInWords.Contains);
    }

    /// <summary>Whether an address is on the site's host, a leading <c>www.</c> aside.</summary>
    private static bool OnSite(string address, string site) =>
        HostOf(address) is { } host && string.Equals(host, HostOf(site), StringComparison.OrdinalIgnoreCase);

    private static string? HostOf(string? address) =>
        address is not null && Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Host.Length > 0
            ? uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host
            : null;
}
