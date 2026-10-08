namespace Daoris.Knowledge;

/// <summary>Why a set-up step's session's word was not kept (REVIEWENV1b) — or <see cref="None"/> when it was.</summary>
public enum ReviewSayRefusal
{
    None,

    /// <summary>No session under that id of this machine's, or none named.</summary>
    NotFound,

    /// <summary>The session works no set-up step: only a set-up step's own session serves or says a set-up.</summary>
    NotASetUpStep,

    /// <summary>The step is open or declined: a set-up is said by the session working it.</summary>
    NotWorking,

    /// <summary>A part that is not one: a folder that is not in the tree, an address that is not one, words past a bound.</summary>
    BadShape,
}

/// <param name="Refusal"><see cref="ReviewSayRefusal.None"/> when it was kept.</param>
/// <param name="Message">The whole answer, phrased once here for every door.</param>
public sealed record ReviewSayOutcome(ReviewSayRefusal Refusal, string Message);

/// <summary>
/// A set-up step's session asks Daoris to serve its build, and says what it showed (REVIEWENV1b, D154 point 5; design
/// §2.6): kept on its own record, this machine's, until its driver posts each set-up with the commit it read.
/// </summary>
public sealed partial class SessionLedger
{
    /// <summary>
    /// Keep the folder a set-up step's session asks Daoris to serve to its tab, and the address it is served at: the folder a
    /// path in its tree, a folder there with no link on the way, the address an absolute <c>http</c> or <c>https</c> one.
    /// Whether the address is the review rule's, and the serving itself, are the shell's (REVIEWENV1d), so this build keeps the
    /// folder for the set-up it names, and says that nothing is served yet.
    /// </summary>
    public async Task<ReviewSayOutcome> ServeAsync(
        string? sessionId, string? folder, string? address, DateTimeOffset now, CancellationToken ct = default)
    {
        var (session, quest, refused) = await SetUpStepOfAsync(sessionId, ct).ConfigureAwait(false);
        if (refused is not null) return refused;

        var named = folder?.Trim() ?? "";
        if (QuestEvidence.JudgePath(named) is { } unfit)
        {
            return new(ReviewSayRefusal.BadShape,
                $"`{Clip(named)}` is not a folder in your tree: {unfit}. Name it from your tree's root with forward slashes, such as "
                + "`dist/app`. Nothing was kept.");
        }

        if (session!.Tree is { } tree && InTree(tree, named) is { } missing)
        {
            return new(ReviewSayRefusal.BadShape, $"`{named}` {missing}. Build first, then name the folder the build wrote. Nothing was kept.");
        }

        var at = address?.Trim() ?? "";
        if (!Reviews.IsAddress(at) || !Uri.TryCreate(at, UriKind.Absolute, out var uri) || uri.PathAndQuery is not ("/" or "") || uri.Fragment.Length > 0)
        {
            return new(ReviewSayRefusal.BadShape,
                $"`{Clip(at)}` is not an address to serve at: the environment's address, an absolute http or https origin such as "
                + "`http://localhost:4200`. Nothing was kept.");
        }

        await sessions.ExclusiveAsync(async inside =>
        {
            var review = await sessions.ReviewOfAsync(session.Id, inside).ConfigureAwait(false);
            return await sessions.KeepReviewAsync(session.Id, review with { Serving = new ReviewServing(named, at, now) }, inside)
                .ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        return new(ReviewSayRefusal.None,
            $"Kept: `{named}` in your tree is the build to serve to your tab at <{at}>, and the set-up you say next names it. This "
            + "build of Daoris does not serve a folder to a tab itself yet, so nothing is served: show the work the way the "
            + $"procedure gives, without stopping or taking over anything of the person's, and say how in `shows` when you say "
            + $"the set-up (review_ready) on quest `#{quest!.Id}`.");
    }

    /// <summary>
    /// Keep a set-up a set-up step's session says (design §2.6): where its tab is, what it showed, how to show it again, and a
    /// review run's command where there was one. Its driver reads the commit the step's tree holds when the session ends, and
    /// at the end of each later turn, and posts each set-up with it; a session never reports a commit. Said again in a later
    /// turn, it is a new set-up.
    /// </summary>
    public async Task<ReviewSayOutcome> ReadyAsync(
        string? sessionId, string? look, string? shows, string? again, string? run, DateTimeOffset now, CancellationToken ct = default)
    {
        var (session, quest, refused) = await SetUpStepOfAsync(sessionId, ct).ConfigureAwait(false);
        if (refused is not null) return refused;

        var where = look?.Trim() ?? "";
        var what = shows?.Trim() ?? "";
        var how = again?.Trim() ?? "";
        var command = string.IsNullOrWhiteSpace(run) ? null : run.Trim();
        string? unfit =
            !Reviews.IsAddress(where) ? $"`look` is the address your tab is at, an absolute http or https address, and `{Clip(where)}` is not one"
            : what.Length == 0 ? "`shows` says what you showed and what to look at"
            : what.Length > Reviews.ShowsLimit ? $"`shows` is at most {Reviews.ShowsLimit} characters, and this is {what.Length}"
            : how.Length == 0 ? "`again` says how to show it again by hand: the address and the clicks"
            : how.Length > Reviews.AgainLimit ? $"`again` is at most {Reviews.AgainLimit} characters, and this is {how.Length}"
            : command is not null && (command.Length > Reviews.RunLimit || command.Contains('\n') || command.Contains('\r'))
                ? $"`run` is the one command the procedure gives, on one line, at most {Reviews.RunLimit} characters"
            : null;
        if (unfit is not null) return new(ReviewSayRefusal.BadShape, $"{unfit}. Nothing was kept.");

        var number = await sessions.ExclusiveAsync(async inside =>
        {
            var review = await sessions.ReviewOfAsync(session!.Id, inside).ConfigureAwait(false);
            var said = new ReviewSaid(review.Said.Count == 0 ? 1 : review.Said.Max(each => each.Number) + 1, where, what, how, now)
            {
                Run = command,
                Served = review.Serving?.Folder,
            };
            await sessions.KeepReviewAsync(session.Id, review with { Said = [.. review.Said, said] }, inside).ConfigureAwait(false);
            return said.Number;
        }, ct).ConfigureAwait(false);

        return new(ReviewSayRefusal.None, quest!.Status == QuestStatus.Done
            ? $"Said set-up {number} on quest `#{quest.Id}`. When this turn ends, Daoris reads the commit your tree holds and "
              + $"posts it with that commit, and the step waits for the person's review in `{quest.SetUpIn}` again."
            : $"Said set-up {number} on quest `#{quest.Id}`. Close your quest done now (quest_respond): when this session ends, "
              + $"Daoris reads the commit your tree holds and posts the set-up with it, and the step waits for the person's review "
              + $"in `{quest.SetUpIn}`. Leave the tab where it is.");
    }

    /// <summary>
    /// The session and the set-up step it works, or why it may not serve or say a set-up: this machine's session, on a set-up
    /// step, which is taken or done.
    /// </summary>
    private async Task<(Session? Session, Quest? Quest, ReviewSayOutcome? Refused)> SetUpStepOfAsync(string? sessionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionId)
            || await sessions.FindAsync(sessionId, ct).ConfigureAwait(false) is not { Origin: null } session)
        {
            return (null, null, new(ReviewSayRefusal.NotFound,
                "This connector speaks for no session the driver started here, so there is no set-up step to say it for."));
        }

        var quest = session.Quest is { } id ? await quests.FindAsync(id, ct).ConfigureAwait(false) : null;
        if (quest is not { SetUpIn: not null })
        {
            return (session, quest, new(ReviewSayRefusal.NotASetUpStep,
                $"Session `{session.Id}` works {(quest is null ? "no quest" : $"quest `#{quest.Id}`")}, which is no set-up step: only a "
                + "set-up step's own session sets work up for the person's review. Nothing was kept."));
        }

        if (quest.Status is QuestStatus.Open or QuestStatus.Declined)
        {
            return (session, quest, new(ReviewSayRefusal.NotWorking,
                $"Set-up step `#{quest.Id}` is {quest.Status}: a set-up is said by the session working it"
                + (quest.Status == QuestStatus.Open ? ", once it has taken it." : ".") + " Nothing was kept."));
        }

        return (session, quest, null);
    }

    /// <summary>
    /// Why <paramref name="folder"/> is not a folder in <paramref name="tree"/> reached by no link, or null when it is (design
    /// §2.3: inside the step's tree, no <c>..</c>, no link). Each segment is looked at, so a link on the way is found.
    /// </summary>
    private static string? InTree(string tree, string folder)
    {
        var path = tree;
        foreach (var segment in folder.Split('/'))
        {
            path = Path.Combine(path, segment);
            var info = new DirectoryInfo(path);
            if (!info.Exists) return "is not a folder in your tree";
            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint) || info.LinkTarget is not null) return "is reached through a link, and a folder served is your tree's own";
        }

        return null;
    }

    private static string Clip(string text) => text.Length <= 120 ? text : text[..120] + "…";
}
