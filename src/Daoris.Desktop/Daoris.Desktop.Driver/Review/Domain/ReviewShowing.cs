using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What the shell serves to a set-up step's tab (REVIEWENV1d, D154 point 5; the review environment design §2.3 step 3): a
/// build's folder in the step's tree, at the rule's address only, under the build's own base, until the person's verdict.
/// </summary>
/// <param name="Quest">The set-up step whose tab it is.</param>
/// <param name="Title">The tab's title before its session navigates it (<see cref="ReviewDesk.TabTitle"/>).</param>
/// <param name="Folder">The build's folder: a full path in the step's tree, reached by no link.</param>
/// <param name="Address">The rule's address, an origin with no last slash: the one place it is served at.</param>
/// <param name="Base">The path under <paramref name="Address"/> it is served at, from <c>/</c>, ending in <c>/</c>.</param>
/// <param name="Look">Where the tab is taken once served: under <paramref name="Address"/> and <paramref name="Base"/>.</param>
public sealed record ReviewServe(string Quest, string Title, string Folder, string Address, string Base, string Look)
{
    /// <summary>The set-up it shows, as <c>machine/sequence</c>, which a verdict names; null where the set-up carried none.</summary>
    public string? SetUp { get; init; }

    /// <summary>
    /// The one pattern the tab's requests are held at, in CDP's <c>Fetch</c> wildcard: the address and the base, anything below.
    /// A request outside it, the app's calls to the development environment's data among them, goes where it would go for the
    /// person (design §2.3 step 3, measured in <c>docs/2026-10-09-review-serving-evidence.md</c>).
    /// </summary>
    public string Pattern => Address + Base + "*";

    /// <summary>
    /// Every pattern held: <see cref="Pattern"/>, and under a base of its own the base without its last slash too, since
    /// <c>/v3</c> is the same page as <c>/v3/</c> and would otherwise reach the person's server.
    /// </summary>
    public IReadOnlyList<string> Patterns => Base == "/" ? [Pattern] : [Pattern, Address + Base.TrimEnd('/')];
}

/// <summary>
/// A set-up waiting for the person on this machine (REVIEWENV1d; design §3.3), as the strip's chip says it: the step, what it
/// showed and where, and whether Daoris serves its tab now.
/// </summary>
public sealed record ReviewWaiting(string Quest, string Title, string Environment, string? Look, string? Shows, bool Served);

/// <summary>
/// The pure half of the serving (REVIEWENV1d): which folder of a step's tree may be served, the build's base, where the tab is
/// taken, and whether the person's verdict answered the set-up a tab shows.
/// </summary>
/// <remarks>
/// Finding the folder in the tree, <c>FolderIn</c>, looks at the tree, so it is Review's Infrastructure's, named through this type
/// as before (<see cref="ReviewServeFiles"/>; STRUCT3c). The showing itself, <see cref="ReviewDesk"/>, is the Application's.
/// </remarks>
public static partial class ReviewServed
{
    /// <summary>
    /// Whether <paramref name="folder"/> is named as a folder of a tree may be (design §2.3 step 3): from the tree's root with forward
    /// slashes, and no <c>.</c> or <c>..</c>.
    /// </summary>
    internal static bool IsFolderName(string folder) =>
        FolderShape().IsMatch(folder) && !folder.Split('/').Any(segment => segment is "." or "..");

    /// <summary>
    /// The path a build is served at under <paramref name="address"/>: its <c>index.html</c>'s own <c>&lt;base href&gt;</c> where it
    /// names a path, or the same address and a path, else <c>/</c>. The build's own declaration of where it runs, which the
    /// browser itself reads to ask for its files, so an app built for <c>/v3/</c> asks for <c>/v3/main.js</c> and is answered from
    /// the folder's <c>main.js</c>. Another origin, a relative base, or a path with <c>..</c>, a query or a fragment is the root.
    /// </summary>
    public static string BaseOf(string? html, string address)
    {
        if (html is null || BaseHref().Match(html) is not { Success: true } found) return "/";
        var href = (found.Groups["d"].Success ? found.Groups["d"] : found.Groups["s"].Success ? found.Groups["s"] : found.Groups["b"]).Value.Trim();

        string path;
        if (href.StartsWith("//", StringComparison.Ordinal)) return "/";
        if (href.StartsWith('/'))
        {
            path = href;
        }
        else if (Uri.TryCreate(href, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https")
        {
            if (!string.Equals(absolute.GetLeftPart(UriPartial.Authority), Origin(address), StringComparison.OrdinalIgnoreCase)) return "/";
            path = absolute.AbsolutePath;
        }
        else
        {
            return "/";
        }

        if (!BasePath().IsMatch(path) || path.Split('/').Any(segment => segment is "." or "..")) return "/";
        return path.EndsWith('/') ? path : path + "/";
    }

    /// <summary>
    /// Where the tab is taken once served: the set-up's <c>look</c> where it is under the address and the base, else the base
    /// itself. A tab is never sent to another site from here, a sign-in page above all, in a browser the person is signed in to.
    /// </summary>
    public static string LookUnder(string address, string @base, string? look) =>
        look is not null
        && Uri.TryCreate(look, UriKind.Absolute, out var at)
        && at.Scheme is "http" or "https"
        && string.IsNullOrEmpty(at.UserInfo)
        && string.Equals(at.GetLeftPart(UriPartial.Authority), address, StringComparison.OrdinalIgnoreCase)
        && at.AbsolutePath.StartsWith(@base, StringComparison.Ordinal)
            ? at.AbsoluteUri
            : address + @base;

    /// <summary>An address as the browser asks for it, scheme, host and a port only where it is not the scheme's own.</summary>
    public static string Origin(string address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var at) ? at.GetLeftPart(UriPartial.Authority) : address.TrimEnd('/');

    /// <summary>A set-up step's newest set-up, the one a verdict answers; null where none was shown.</summary>
    public static QuestSetUpView? Newest(QuestView quest) => quest.SetUps.Count == 0 ? null : quest.SetUps[^1];

    /// <summary>A set-up's reference, <c>machine/sequence</c>, as a verdict names it; null where it carries none.</summary>
    public static string? Reference(QuestSetUpView setUp) =>
        setUp is { Machine: { Length: > 0 } machine, Sequence: { } sequence } ? $"{machine}/{sequence}" : null;

    /// <summary>
    /// What the person said that ends the showing of <paramref name="setUp"/>: their skip of the review, or a <c>reviewed</c> or a
    /// <c>not-yet</c> on that set-up (a reference that is null takes either). Null while it still waits for them. A verdict on an
    /// older set-up is not an answer to a newer one.
    /// </summary>
    public static string? Answered(QuestView quest, string? setUp)
    {
        foreach (var verdict in quest.Verdicts)
        {
            if (verdict.Said == "skipped") return verdict.Said;
            if (verdict.Said is "reviewed" or "not-yet"
                && (setUp is null || $"{verdict.SetUpMachine}/{verdict.SetUpSequence}" == setUp))
            {
                return verdict.Said;
            }
        }

        return null;
    }

    [GeneratedRegex(@"^[^/\\:*?""<>|\x00-\x1f]+(?:/[^/\\:*?""<>|\x00-\x1f]+)*$")]
    private static partial Regex FolderShape();

    [GeneratedRegex(@"<base\b[^>]*?\bhref\s*=\s*(?:""(?<d>[^""]*)""|'(?<s>[^']*)'|(?<b>[^\s>]+))", RegexOptions.IgnoreCase)]
    private static partial Regex BaseHref();

    [GeneratedRegex(@"^/(?:[A-Za-z0-9._~!$&'()*+,;=:@%-]+/?)*$")]
    private static partial Regex BasePath();
}
