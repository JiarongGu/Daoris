using System.Net.WebSockets;
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
}

/// <summary>
/// Daoris's browser as a set-up step's review uses it (REVIEWENV1d): the shell implements it, over its own CDP connection to its
/// own browser; a host with no shell has none, and the planner sits a local set-up step there (REVIEWENV1c).
/// </summary>
/// <remarks>
/// A failure is thrown as <see cref="InvalidOperationException"/>, <see cref="TimeoutException"/>, an HTTP or socket failure, or
/// <see cref="IOException"/>, with its reason: <see cref="ReviewDesk"/> says each where the person reads it.
/// </remarks>
public interface IReviewTabs
{
    /// <summary>
    /// Bring the browser up and open the quest's tab, titled, in front; or bring the one already open forward (design §2.3 step
    /// 1). A tab an agent opens over CDP has no window (the in-app browser design, §3.2), so the step works in this one.
    /// </summary>
    Task OpenAsync(string quest, string title, CancellationToken ct = default);

    /// <summary>
    /// Hold request interception on the quest's tab alone, opening one where none is, at <see cref="ReviewServe.Pattern"/> only,
    /// answering from the folder, then take the tab to <see cref="ReviewServe.Look"/> and bring it forward. A serving already held
    /// for the quest is replaced. It lasts until <see cref="Stop"/>, the tab's closing, or the shell's exit.
    /// </summary>
    Task ServeAsync(ReviewServe serve, CancellationToken ct = default);

    /// <summary>Let the quest's tab go unserved, leaving it open; nothing where none is served.</summary>
    void Stop(string quest);

    /// <summary>What it serves now, one per quest: a tab the person closed, or a browser that went, is served no more.</summary>
    IReadOnlyList<ReviewServe> Serving { get; }
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
public static partial class ReviewServed
{
    /// <summary>
    /// The full path of <paramref name="folder"/> in <paramref name="tree"/>, or null with why not (design §2.3 step 3): a path
    /// from the tree's root with forward slashes, no <c>.</c> or <c>..</c>, and every folder on the way a folder of its own, never
    /// a link, as the service judges <c>review_serve</c>'s folder and a <c>${proof}</c> name is read (D146 §11).
    /// </summary>
    public static string? FolderIn(string tree, string folder, out string? why)
    {
        if (!FolderShape().IsMatch(folder) || folder.Split('/').Any(segment => segment is "." or ".."))
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

/// <summary>
/// The review's showing, as the driver keeps it (REVIEWENV1d, D154 point 5; design §2.3 steps 1–5, §3.3): a set-up step's tab
/// opened before its session, the folder its session named served there once its set-up is posted, kept served from look to
/// look until the person's verdict, and served again at their <i>Show it again</i>. Daoris holds it, not the session: a session's
/// own interception ends with it, and the next reload would quietly load the person's own server (measured,
/// <c>docs/2026-10-09-review-serving-evidence.md</c> §1 row 4).
/// </summary>
/// <remarks>
/// <para><b>One per shell</b>, shared by every look's driver as the browser is, since a serving outlives the look and the
/// session that started it. The headless host has none: it has no browser, and its planner sits a local set-up step.</para>
///
/// <para><b>From the posted set-up, not from the session's word.</b> The folder is the one the service kept from the session's
/// <c>review_serve</c> and posted with the commit Daoris read; the address is the rule's, never the session's; the tab is taken
/// only to a place under it. Nothing of the person's is stopped by any of it.</para>
/// </remarks>
public sealed class ReviewDesk(IReviewTabs tabs)
{
    private readonly object _gate = new();
    private IReadOnlyList<ReviewWaiting> _waiting = [];

    /// <summary>The browser's half, for the shell's own reads.</summary>
    public IReviewTabs Tabs => tabs;

    /// <summary>
    /// A set-up step's tab's title before its session navigates it, which its instruction names so the session finds it among
    /// the browser's tabs (design §2.2): one spelling for both.
    /// </summary>
    public static string TabTitle(string quest) => $"Review #{quest.TrimStart('#')}";

    /// <summary>
    /// The set-ups waiting for the person here as the last look read them, each with whether its tab is served now: the strip's
    /// chip (design §3.3). Read fresh from the browser's half each time, so a tab the person closed says so at once.
    /// </summary>
    public IReadOnlyList<ReviewWaiting> Waiting
    {
        get
        {
            var serving = tabs.Serving.Select(serve => serve.Quest).ToHashSet(StringComparer.OrdinalIgnoreCase);
            lock (_gate) return [.. _waiting.Select(row => row with { Served = serving.Contains(row.Quest) })];
        }
    }

    /// <summary>
    /// A local set-up step's tab, opened as its start begins and before its session, so its instruction can name it (design §2.3
    /// step 1). Null where it opened, or where the environment is deployed and shows nothing here; otherwise why the start is held.
    /// </summary>
    public async Task<string?> OpenForStepAsync(QuestView quest, ReviewEnvironment environment, CancellationToken ct)
    {
        if (environment.Kind != "local") return null;
        try
        {
            await tabs.OpenAsync(quest.Id, TabTitle(quest.Id), ct).ConfigureAwait(false);
            return null;
        }
        catch (Exception error) when (Failed(error, ct))
        {
            return $"set-up step `#{quest.Id}` shows its chain's work in Daoris's browser, and its tab did not open: {Said(error)}.";
        }
    }

    /// <summary>
    /// Serve a set-up step's newest set-up said by <paramref name="session"/> to its tab, once the set-up is posted (design §2.3
    /// steps 3–4): the folder it named, in <paramref name="tree"/>, at the rule's address. Answers the sentence the step's
    /// conversation is told, or null where nothing is shown here: no set-up step, a deployed environment, or no set-up said.
    /// </summary>
    public async Task<string?> ServeSetUpAsync(
        QuestView? quest, string session, string tree, ReviewEnvironment? environment, CancellationToken ct)
    {
        if (quest?.SetUpIn is null || environment is not { Kind: "local" }) return null;
        if (quest.SetUps.LastOrDefault(setUp => string.Equals(setUp.Session, session, StringComparison.OrdinalIgnoreCase)) is not { } said)
        {
            return null;
        }

        var (serve, why) = Plan(quest, said, environment, tree);
        if (serve is null)
        {
            // An older build is not what this set-up shows, so it is not left served under it.
            tabs.Stop(quest.Id);
            return $"its build is not served to its tab: {why}.";
        }

        try
        {
            await tabs.ServeAsync(serve, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (Failed(error, ct))
        {
            return $"its build is not served to its tab: {Said(error)}. *Show it again* serves it once Daoris's browser answers.";
        }

        return $"Daoris serves `{said.Served}` from its tree to its tab at <{serve.Address}{serve.Base}> until you review it, so a "
            + "reload there shows this build, and never your own server.";
    }

    /// <summary>
    /// Each look (design §2.3, *kept shown, then stopped*): the set-ups waiting for the person here, from the quests the look read;
    /// and each tab served whose set-up the person answered, whose quest is declined or gone, or whose tree went, let go. A served
    /// quest off <paramref name="quests"/>, the outstanding list, is read whole by <paramref name="find"/> before it is let go.
    /// Answers the lines the look says.
    /// </summary>
    public async Task<IReadOnlyList<string>> LookAsync(
        IReadOnlyList<QuestView> quests, Func<string, CancellationToken, Task<QuestView?>> find, CancellationToken ct)
    {
        var waiting = new List<ReviewWaiting>();
        foreach (var quest in quests)
        {
            if (quest is not { SetUpIn: { } environment } || quest.Status == "Declined") continue;
            // A local set-up shown on another machine crosses without its look: its tab is that machine's.
            if (ReviewServed.Newest(quest) is not { Local: true, Look: { } look } newest) continue;
            if (ReviewServed.Answered(quest, ReviewServed.Reference(newest)) is not null) continue;
            waiting.Add(new ReviewWaiting(quest.Id, quest.Name, environment, look, newest.Shows, Served: false));
        }

        lock (_gate) _waiting = waiting;

        var lines = new List<string>();
        foreach (var serve in tabs.Serving)
        {
            var quest = quests.FirstOrDefault(each => string.Equals(each.Id, serve.Quest, StringComparison.OrdinalIgnoreCase))
                        ?? await find(serve.Quest, ct).ConfigureAwait(false);
            var why = quest is null ? "its quest is gone from this machine's service"
                : quest.Status == "Declined" ? "it was declined"
                : ReviewServed.Answered(quest, serve.SetUp) is { } verdict ? $"you said `{verdict}`"
                : !Directory.Exists(serve.Folder) ? "the tree that held its build is gone"
                : null;
            if (why is null) continue;

            tabs.Stop(serve.Quest);
            lines.Add($"review  #{serve.Quest}: its tab is no longer served, since {why}; nothing of yours was touched.");
        }

        return lines;
    }

    /// <summary>
    /// *Show it again* from the strip (design §3.3): the quest, the rule standing for its repository here, and the tree of the
    /// session that said its newest set-up, read from the service, then served as <see cref="ShowAgainAsync(QuestView?, ReviewEnvironment?, string?, CancellationToken)"/> does.
    /// </summary>
    public async Task<(bool Ok, string Message)> ShowAgainAsync(ServiceClient service, DriverConfig config, string quest, CancellationToken ct)
    {
        var id = quest.Trim().TrimStart('#');
        if (await service.FindQuestAsync(id, ct).ConfigureAwait(false) is not { } found)
        {
            return (false, $"No quest `#{id}` on this machine's service, so there is nothing to show again.");
        }

        var tree = ReviewServed.Newest(found)?.Session is { Length: > 0 } session
            ? await service.SessionTreeAsync(session, ct).ConfigureAwait(false)
            : null;
        return await ShowAgainAsync(found, ReviewSetUps.Environment(config, found, found.Workspace), tree, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// *Show it again*'s first half (design §3.3), which needs no session and no model: the newest set-up's folder served to the
    /// step's tab again, opened where the person closed it, at where the set-up left it. Refused, saying why, wherever it would show
    /// something other than that build at that address: nothing shown, no folder named, a deployed environment, no rule here, a
    /// set-up shown on another machine, or a tree that is gone.
    /// </summary>
    public async Task<(bool Ok, string Message)> ShowAgainAsync(
        QuestView? quest, ReviewEnvironment? environment, string? tree, CancellationToken ct)
    {
        if (quest is null) return (false, "No such quest on this machine's service, so there is nothing to show again.");
        if (quest.SetUpIn is not { } named) return (false, $"`#{quest.Id}` is no set-up step, so nothing of it was shown to show again.");
        if (ReviewServed.Newest(quest) is not { } newest)
        {
            return (false, $"set-up step `#{quest.Id}` has shown nothing yet in `{named}`, so there is nothing to show again.");
        }

        if (environment is null)
        {
            return (false, $"no review rule here names `{named}` for `{quest.To}`, so where it is shown is not known here: "
                + $"`daoris driver review {quest.To} {named} …` declares it.");
        }

        if (environment.Kind != "local")
        {
            return (false, $"`{environment.Name}` is a deployed environment: its set-up is shown there"
                + (newest.Look is { } there ? $", at {there}" : "") + ", and Daoris serves nothing for it.");
        }

        if (newest.Look is null)
        {
            return (false, $"set-up step `#{quest.Id}` was shown on another machine (`{newest.Machine}`), whose browser holds its tab.");
        }

        if (tree is null || !Directory.Exists(tree))
        {
            return (false, $"set-up step `#{quest.Id}`'s tree, which held its build, is gone, so there is no build to show again.");
        }

        var (serve, why) = Plan(quest, newest, environment, tree);
        if (serve is null) return (false, $"set-up step `#{quest.Id}` is not shown again: {why}.");

        try
        {
            await tabs.ServeAsync(serve, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (Failed(error, ct))
        {
            return (false, $"set-up step `#{quest.Id}` is not shown again: {Said(error)}.");
        }

        return (true, $"Showing set-up step `#{quest.Id}` again in Daoris's browser, at {serve.Look}: its build is served there until you "
            + "review it.");
    }

    /// <summary>What is served for <paramref name="setUp"/>, or null with why nothing is, a clause each caller leads.</summary>
    private static (ReviewServe? Serve, string Why) Plan(QuestView quest, QuestSetUpView setUp, ReviewEnvironment environment, string tree)
    {
        if (environment.Address is not { } declared) return (null, $"`{environment.Name}` declares no address to serve it at");

        var address = ReviewServed.Origin(declared);
        if (setUp.Served is not { Length: > 0 } named)
        {
            return (null, "the session named no folder with `review_serve`, so Daoris has no build to keep showing at "
                + $"{address}, and a reload there shows your own server");
        }

        if (ReviewServed.FolderIn(tree, named, out var unfit) is not { } folder) return (null, $"`{named}` {unfit}");

        var index = Path.Combine(folder, "index.html");
        var html = File.Exists(index) ? ReadHead(index) : null;
        var @base = ReviewServed.BaseOf(html, address);
        return (new ReviewServe(quest.Id, TabTitle(quest.Id), folder, address, @base, ReviewServed.LookUnder(address, @base, setUp.Look))
        {
            SetUp = ReviewServed.Reference(setUp),
        }, "");
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

    /// <summary>A failure of the browser's half, to say; a cancellation of the caller's own is never one.</summary>
    private static bool Failed(Exception error, CancellationToken ct) =>
        error is InvalidOperationException or TimeoutException or HttpRequestException or WebSocketException or IOException
        || (error is OperationCanceledException && !ct.IsCancellationRequested);

    private static string Said(Exception error) => error.Message.TrimEnd().TrimEnd('.');
}
