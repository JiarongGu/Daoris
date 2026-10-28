using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What the review's serving looks at and decides (REVIEWENV1d; design §2.3 step 3): which folder of a step's tree may be served
/// and found there, never through a link, the build's base and the head of its <c>index.html</c>, where the tab is taken, whether
/// a tree still stands, and whether the person's verdict answered the set-up a tab shows.
/// </summary>
/// <remarks>
/// A top file, not the Domain's (FLAT1b-ii): finding the folder in the tree and reading the build's head look at the disk, so the
/// type holds I/O; the records the showing is made of stay in <c>Domain/ReviewShowing.cs</c>. It is <c>partial</c> only because
/// its patterns are source-generated. The showing itself is <see cref="ReviewDesk"/>.
/// </remarks>
public static partial class ReviewServed
{
    /// <summary>
    /// The folders a review step in progress shows from (REVIEWENV1d): the tree of the session that said each set-up waiting
    /// for the person's verdict, read from the service's records as the review's gate reads them, whether or not this loop has
    /// a desk, since <i>Show it again</i> serves from that tree; and, where the shell has a desk, each build it serves to a tab
    /// now. The branch tidy keeps each tree it lies in as a session's in use is (<c>BranchTidying.LookAsync</c>'s <c>reviewing</c>).
    /// </summary>
    public static async Task<IReadOnlyCollection<string>> ReviewingAsync(ReviewDesk? desk, ServiceClient service, CancellationToken ct)
    {
        var paths = desk?.Tabs.Serving.Select(serve => serve.Folder).ToList() ?? [];
        var waiting = (await service.EveryQuestAsync(ct).ConfigureAwait(false))
            .Where(quest => quest.SetUpIn is not null && quest.Status != "Declined")
            .Select(quest => (Quest: quest, Newest: Newest(quest)))
            .Where(each => each.Newest is { Session.Length: > 0 }
                           && Answered(each.Quest, Reference(each.Newest)) is null)
            .Select(each => each.Newest!.Session!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (waiting.Count > 0)
        {
            paths.AddRange((await service.SessionRecordsAsync(ct).ConfigureAwait(false))
                .Where(record => waiting.Contains(record.Id) && record.Tree is { Length: > 0 })
                .Select(record => record.Tree!));
        }

        return paths;
    }

    /// <summary>
    /// The full path of <paramref name="folder"/> in <paramref name="tree"/>, or null with why not (design §2.3 step 3): a path
    /// from the tree's root with forward slashes, no <c>.</c> or <c>..</c>, and every folder on the way a folder of its own, never
    /// a link, as the service judges <c>review_serve</c>'s folder and a <c>${proof}</c> name is read (D146 §11).
    /// </summary>
    public static string? FolderIn(string tree, string folder, out string? why)
    {
        if (!IsFolderName(folder))
        {
            why = "is not a folder of its tree named from the tree's root with forward slashes";
            return null;
        }

        var root = Path.GetFullPath(tree);
        var at = root;
        foreach (var segment in folder.Split('/'))
        {
            at = Path.Combine(at, segment);
            var found = new DirectoryInfo(at);
            if (!found.Exists)
            {
                why = "is not a folder in its tree";
                return null;
            }

            if (found.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                why = "is reached through a link, and a folder served is the tree's own";
                return null;
            }
        }

        var full = Path.GetFullPath(at);
        if (!full.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            why = "is not inside its tree";
            return null;
        }

        why = null;
        return full;
    }

    /// <summary>Whether a tree or a build's folder still stands on this machine.</summary>
    public static bool Stands(string folder) => Directory.Exists(folder);

    /// <summary>The first part of a build's <c>index.html</c> in <paramref name="folder"/>, where its <c>&lt;base&gt;</c> is; null where there is none.</summary>
    public static string? IndexHead(string folder)
    {
        var index = Path.Combine(folder, "index.html");
        return File.Exists(index) ? ReadHead(index) : null;
    }

    /// <summary>The first part of a build's <c>index.html</c>, where its <c>&lt;base&gt;</c> is: a head, never a whole bundle.</summary>
    private static string? ReadHead(string path)
    {
        try
        {
            using var reader = new StreamReader(path);
            var buffer = new char[64 * 1024];
            return new string(buffer, 0, reader.ReadBlock(buffer, 0, buffer.Length));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether <paramref name="folder"/> is named as a folder of a tree may be (design §2.3 step 3): from the tree's root with forward
    /// slashes, and no <c>.</c> or <c>..</c>.
    /// </summary>
    internal static bool IsFolderName(string folder) =>
        SafeNames.IsRepositoryPath(folder) && FolderShape().IsMatch(folder);

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
