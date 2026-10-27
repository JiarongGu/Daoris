namespace Daoris.Driver;

/// <summary>
/// The words of the <c>trees</c> reads (RPC1b2, RPCOPEN1, the terminal RPC design §7.3): the clean-up's and the sync's lists and each
/// plan, one function per read, shared by the alone path and the routes that answer the read (<c>said</c>), so the verb that is
/// the running Daoris's client prints what the alone path prints. Each takes the record the read's core returns; nothing here
/// runs git or asks the service, except <see cref="LandPlanAsync"/>, which asks the landing's own two reads as the verb does.
/// </summary>
public static class TreesPlanWords
{
    /// <summary>
    /// <c>trees clean</c>'s list: each session branch with what it holds and the doors offered beside a kept one (LAND4), the branches
    /// landings made, and what <c>--yes</c> would remove; or that there is nothing to clean.
    /// </summary>
    /// <param name="records">The sessions' records, read only where a kept branch's row could offer its landing; none otherwise.</param>
    public static TreesSaid CleanPlan(SweepPlan plan, IReadOnlyList<SessionRecord> records, TreesAsk ask)
    {
        if (plan.Sessions.Count == 0 && plan.Landed.Count == 0) return new([TreesCommand.NothingToClean(ask)], [], 0);
        var said = new List<string>();
        foreach (var item in plan.Sessions)
        {
            said.Add($"  {(item.Removable ? "goes " : "kept ")} {TreesWords.Describe(item)}");
            if (SessionBranchDoors.LandingOffered(item, SessionBranchDoors.SessionOfTree(records, item.Tree)) is { } landing)
            {
                said.Add($"         {landing}");
            }

            if (SessionBranchDoors.RemovalOffered(item) is { } offer) said.Add($"         {offer}");
        }

        // The branches landings made (WSR5), in a group of their own: each goes where its work reached the line.
        if (plan.Landed.Count > 0) said.Add("  landed branches — each goes once its work reads on the line:");
        foreach (var item in plan.Landed) said.Add($"  {(item.Removable ? "goes " : "kept ")} {LandedWords.Describe(item)}");
        var going = plan.Sessions.Count(item => item.Removable) + plan.Landed.Count(item => item.Removable);
        said.Add(going == 0
            ? "trees: nothing to clean — every branch listed holds something of its own."
            : $"trees: {going} branch(es) would go, a session's with its tree. `{TreesCommand.Press(ask)}` removes them.");
        return new(said, [], 0);
    }

    /// <summary>
    /// <c>trees sync</c>'s list: what was not fetched, said once and first (WSR7), each line, branch and landed branch, the repositories
    /// left apart, and what <c>--yes</c> would change.
    /// </summary>
    public static TreesSaid SyncPlan(SyncPlan plan, TreesAsk ask, DateTimeOffset now)
    {
        var said = new List<string>(SyncWords.NotFetched(plan.Lines, now));
        foreach (var pull in plan.Lines) said.Add($"  {(pull.Moves ? "moves" : "stays")}  {SyncWords.Describe(pull)}");
        foreach (var item in plan.Rebases) said.Add($"  {(item.Replays ? "moves" : "stays")}  {SyncWords.Describe(item)}");
        if (plan.Deletes.Count > 0) said.Add("  landed branches whose work reached the line:");
        foreach (var item in plan.Deletes) said.Add($"  {(item.Removable ? "goes " : "kept ")}  {LandedWords.Describe(item)}");
        if (SyncWords.Apart(plan.Apart) is { } apart) said.Add(apart);
        var acts = plan.Lines.Count(pull => pull.Moves) + plan.Rebases.Count(item => item.Replays) + plan.Deletes.Count(item => item.Removable);
        said.Add(plan.Looked.Count == 0 ? TreesCommand.NoneHeld(ask)
            : acts == 0 ? "trees: everything here is up to date."
            : $"trees: {acts} thing(s) would change. `{TreesCommand.Press(ask)}` does them — Daoris fetches, and never pushes.");
        return new(said, [], 0);
    }

    /// <summary>What accepting a session would do, in one line — who pushes it where a plugin does, and what would refuse it (D100).</summary>
    public static string Planned(string session, LandingPlan plan)
    {
        // ACCEPTALL1g: a branch that already stands (an entry's or a chain's) is moved on, and its pull request grows, as the review says.
        var line = plan.Stands
            ? $"trees: accepting `{session}` would add its work to `{plan.Target}`, a branch that already stands, moving it on ({plan.Source})"
            : $"trees: accepting `{session}` would {(plan.Form == LandingForm.Branch ? "put its work on the branch" : "merge its work into")} `{plan.Target}` ({plan.Source})";
        if (plan.Plugin is { } plugin) line += plan.Stands ? $", then plugin `{plugin}` would push it to grow its pull request" : $", then plugin `{plugin}` would push it and open the pull request";
        line += ".";
        // LAND2b: the rule's switch lands it at its quest's done with no press, and a plugin it cannot use does not stop the branch.
        if (plan.AutoAccept) line += " The rule accepts automatically, so its quest's done lands it with no press.";
        return plan.Problem is { } problem
            ? $"{line} {(plan.AutoAccept ? "Its push would not be tried now" : "It would be refused now")}: {problem}"
            : line;
    }

    /// <summary>
    /// A session's entry on the due list (LAND2b, design §8), for <c>trees land --plan</c>: when it became due, and its last try
    /// by its code; null where it was never due.
    /// </summary>
    public static string? Due(AutoLanding? entry)
    {
        if (entry is null) return null;
        var due = $"trees: due to land automatically since {Minute(entry.DueAt)} UTC";
        if (entry.Last is not { } last) return $"{due}; not tried yet — the loop's next look tries it.";
        var tried = $"{due}; last tried {Minute(last.At)} UTC: {last.Code}"
                    + (last.Branch is { } branch ? $" (`{branch}`)" : "");
        return entry.Closed is null
            ? $"{tried}. It waits: " + last.Code switch
            {
                AutoLandingCode.Held => "its quest's release lands it.",
                // FABRIC-CLOSE1h2: its branch is made and its push held back, so the release hands the branch on; nothing lands again.
                AutoLandingCode.PushHeld => "its quest's release hands its branch on, or `trees hand` does now.",
                _ => "a change in its tree, or your press lands it.",
            }
            : $"{tried}.";
    }

    private static string Minute(DateTimeOffset at) =>
        at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// <c>trees land &lt;session&gt; --plan</c> for a session whose review reads as landed (REVIEW2, D113): why, and where it is due,
    /// which lands nothing again.
    /// </summary>
    public static TreesSaid LandedPlan(string session, LandedReview before, AutoLanding? due)
    {
        var said = new List<string> { $"trees: {LandedReviewWords.Describe(session, before)}" };
        if (Due(due) is { } line) said.Add(line);
        return new(said, [], 0);
    }

    /// <summary>
    /// <c>trees land &lt;session&gt; --plan</c>: where accepting would put the work, what the line already holds of it, the work it holds by
    /// content (SQUASHTIDY1f), what the gate says before the press, the review's reading of an earlier landing, and where it is due.
    /// </summary>
    /// <param name="pluginLines">A plugin's own lines said while the plan was made, first, as the alone path says them as they come.</param>
    public static async Task<TreesSaid> LandPlanAsync(
        SessionTrees landing, string home, string session, string tree, LandingPlan plan, LandingGate gate, LandedReview? before,
        IReadOnlyList<string> pluginLines, CancellationToken ct = default)
    {
        var said = new List<string>(pluginLines) { Planned(session, plan) };
        // LANDSYNC1: what the press would leave out because the line holds it, judged against the last fetch.
        said.AddRange(LandingSyncWords.Plan(plan));
        // SQUASHTIDY1f: work the line holds by content, which the press refuses, said before it in the press's own sentence.
        if (await landing.DiscardOfferAsync(tree, ct).ConfigureAwait(false) is { } carried) said.Add($"trees: {carried.NotLanded}");
        // What the gate says before the press (the second-agent design §8.6, the review design §3.5): what holds it, or what let it go.
        said.AddRange(LandingGateWords.Plan(gate));
        // Landed before, its branch gone since, its tree still here: said, as the review's note says it.
        if (before is not null) said.Add($"trees: {LandedReviewWords.Describe(session, before)}");
        // Due to land at its quest's done (LAND2b): what its tries came to, which the review's note shows.
        if (Due(new AutoLandings(home).Of(session)) is { } due) said.Add(due);
        return new(said, [], 0);
    }

    /// <summary>What handing a landed branch on would do, in one line — and what would refuse it (WSR5b).</summary>
    public static string HandPlanned(HandPlan plan)
    {
        var line = plan.Plugin is { } plugin
            ? $"trees: handing on `{plan.Branch}` ({plan.Repository}) would give plugin `{plugin}` its {plan.Commits} commit(s) to push and open the pull request for."
            : $"trees: `{plan.Branch}` ({plan.Repository}) has no plugin named to hand it to.";
        if (plan.PullRequest is { } pr) line += $" Its last pull request: {pr}";
        return plan.Problem is { } problem ? $"{line} It would be refused now: {problem}" : line;
    }

    /// <summary><c>trees hand --plan</c>: the plan's one line, after the plugin's own lines.</summary>
    public static TreesSaid HandPlan(HandPlan plan, IReadOnlyList<string> pluginLines) => new([.. pluginLines, HandPlanned(plan)], [], 0);

    /// <summary>
    /// What an entry's Accept would do, in lines (ACCEPTALL1e): where its parts land, then each part's own words: what it shares,
    /// what it leaves out, what it conflicts with, what holds it and what a later part superseded; the page's plan in a terminal's words.
    /// </summary>
    public static IEnumerable<string> EntryPlanned(SessionEntry entry, EntryPlan plan, IReadOnlyDictionary<string, string> held)
    {
        var count = plan.Parts.Count(part => part.SameTreeAs is null);
        yield return plan.Parts.Count == 0
            ? $"trees: `{entry.Key}` ({entry.Repository}) has no part with work to land here."
            : $"trees: accepting `{entry.Key}` ({entry.Repository}) would {(plan.Form == LandingForm.Branch ? "put its work on the branch" : "merge its work into")} "
              + $"`{plan.Target}`, {count} tree(s), oldest first.";
        foreach (var part in plan.Parts)
        {
            if (part.SameTreeAs is { } same) yield return $"  {part.Session}  shares `{same}`'s tree, which that part's landing carries.";
            if (part.Plan?.LeftOut is { Count: > 0 } left) yield return $"  {part.Session}  leaves out {left.Count} commit(s) the line holds.";
            if (part.Plan?.Conflict is { } conflict) yield return $"  {part.Session}  would conflict: {conflict.Says}";
            if (held.TryGetValue(part.Session, out var why)) yield return $"  {part.Session}  is held: {why}";
            if (part.Superseded is { } by)
            {
                yield return $"  {part.Session}  was redone by `{by.By}`: {(by.Conflicts ? "it conflicts after that part's work" : "it adds nothing after it")} "
                    + $"(`--leave-out {part.Session}` leaves it out): {by.Says}";
            }
        }
    }

    /// <summary><c>trees land --entry</c> or <c>--ask --plan</c>: each entry's plan in lines, in the order the entries came.</summary>
    public static TreesSaid EntryPlan(IEnumerable<string> lines) => new([.. lines], [], 0);
}
