using System.Net.WebSockets;

namespace Daoris.Driver;

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
///
/// <para>The browser is its port (<see cref="IReviewTabs"/>), and the folders it looks at are <see cref="ReviewServed"/>'s.</para>
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
    /// Answers the lines the look says, each classed as it is made (NOTIFY2h2): a tab let go on the person's answer their own act, any
    /// other what the strip says.
    /// </summary>
    /// <param name="said">The look's classing; none classes and keeps nothing.</param>
    public async Task<IReadOnlyList<string>> LookAsync(
        IReadOnlyList<QuestView> quests, Func<string, CancellationToken, Task<QuestView?>> find, CancellationToken ct, LookSaid? said = null)
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

        var lines = new LookLines(said ?? LookSaid.None, "review");
        foreach (var serve in tabs.Serving)
        {
            var quest = quests.FirstOrDefault(each => string.Equals(each.Id, serve.Quest, StringComparison.OrdinalIgnoreCase))
                        ?? await find(serve.Quest, ct).ConfigureAwait(false);
            var answered = quest is not null && quest.Status != "Declined" ? ReviewServed.Answered(quest, serve.SetUp) : null;
            var why = quest is null ? "its quest is gone from this machine's service"
                : quest.Status == "Declined" ? "it was declined"
                : answered is { } verdict ? $"you said `{verdict}`"
                : !ReviewServed.Stands(serve.Folder) ? "the tree that held its build is gone"
                : null;
            if (why is null) continue;

            tabs.Stop(serve.Quest);
            lines.Of(answered is null ? LookClass.Progress : LookClass.Own, $"review  #{serve.Quest}: its tab is no longer served, since {why}; nothing of yours was touched.");
        }

        return lines.Said;
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

        if (tree is null || !ReviewServed.Stands(tree))
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

        var html = ReviewServed.IndexHead(folder);
        var @base = ReviewServed.BaseOf(html, address);
        return (new ReviewServe(quest.Id, TabTitle(quest.Id), folder, address, @base, ReviewServed.LookUnder(address, @base, setUp.Look))
        {
            SetUp = ReviewServed.Reference(setUp),
        }, "");
    }

    /// <summary>A failure of the browser's half, to say; a cancellation of the caller's own is never one.</summary>
    private static bool Failed(Exception error, CancellationToken ct) =>
        error is InvalidOperationException or TimeoutException or HttpRequestException or WebSocketException or IOException
        || (error is OperationCanceledException && !ct.IsCancellationRequested);

    private static string Said(Exception error) => error.Message.TrimEnd().TrimEnd('.');
}
