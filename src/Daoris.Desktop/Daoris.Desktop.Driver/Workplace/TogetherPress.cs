namespace Daoris.Driver;

/// <summary>The head the press brings sessions into, with what its records say of it.</summary>
/// <param name="Line">The line's first record, which the operation is bound to.</param>
/// <param name="Record">The head's record now, which the words are said to.</param>
public sealed record TogetherHeadCandidate(string Line, string Record, string Repository, string? Workspace, string Tree, string Branch)
{
    /// <summary>The say door would keep a word for it: running, ended or parked here, not winding up past its words.</summary>
    public bool TakesWords { get; init; } = true;

    /// <summary>It runs on another machine, where its tree is not here.</summary>
    public bool RunsElsewhere { get; init; }
}

/// <summary>A session ticked to bring into the head, with what its records say of it.</summary>
public sealed record TogetherCandidate(string Session, string Repository, string Tree, string Branch, string Base)
{
    public string? Quest { get; init; }
    public string? Unit { get; init; }
    public string? Submission { get; init; }

    /// <summary>Another person's session (D137): never brought in from here.</summary>
    public bool Teammate { get; init; }

    /// <summary>One of its parts is running.</summary>
    public bool Live { get; init; }

    /// <summary>A start waits in its tree.</summary>
    public bool StartWaiting { get; init; }

    /// <summary>Its workflow steps on past its landing, the only maker of its unit's integration request.</summary>
    public bool StepsOn { get; init; }
}

/// <summary>What a press came to: the operation recorded and told, or the sentence it was refused with.</summary>
public sealed record TogetherPressed(TogetherOperation? Operation, string? Refused);

/// <summary>
/// <i>Bring them together…</i> (LINESTOGETHER1d, the review-flow design §4): the press judges its refusals under the door's lock order,
/// pins every tip, records the operation at <i>asked</i>, then says the fixed words to the head through the say door under the
/// operation's key. Words that reach nobody let it go, so nothing stays bound.
/// </summary>
public sealed class TogetherPress(string home, RebaseTelling.Say say, Func<DateTimeOffset> now)
{
    /// <summary>
    /// The head and the ticked members as their records and their trees say: the head is the line's head of <paramref name="session"/>,
    /// bound to the line's first record; each member's base is where its work leaves the head's. The sentence where one is not here.
    /// </summary>
    /// <param name="stepsOn">Whether a member's workflow steps on past its landing; none asked reads as not.</param>
    public static async Task<(TogetherHeadCandidate? Head, IReadOnlyList<TogetherCandidate> Members, string? Refused)> CandidatesAsync(
        IReadOnlyList<SessionRecord> records, string session, IReadOnlyCollection<string> members, CancellationToken ct,
        Func<string, CancellationToken, Task<bool>>? stepsOn = null)
    {
        var byId = records.GroupBy(each => each.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        if (!byId.TryGetValue(session, out var pressed)) return (null, [], "Nothing was brought together: the head's record is not here.");
        var at = pressed.Head is { } named && byId.TryGetValue(named, out var ahead) ? ahead : pressed;
        if (at.Tree is not { Length: > 0 } tree || await BranchAsync(tree, ct).ConfigureAwait(false) is not { } branch)
        {
            return (null, [], "Nothing was brought together: the head's tree is not here, or its branch could not be read.");
        }

        var first = at;
        for (var hops = 0; first.From is { } from && byId.TryGetValue(from, out var before) && hops < 256; hops++) first = before;
        var headTip = await TipAsync(tree, ct).ConfigureAwait(false);
        var head = new TogetherHeadCandidate(first.Id, at.Id, at.Repository, at.Workspace, tree, branch)
        {
            TakesWords = at.State is not ("stood-down" or "declined"), RunsElsewhere = at.Teammate,
        };
        var list = new List<TogetherCandidate>(members.Count);
        foreach (var id in members)
        {
            if (!byId.TryGetValue(id, out var member)) return (null, [], $"Nothing was brought together: `{id}` has no record here.");
            var memberTree = member.Tree ?? "";
            var @base = headTip is not null && Directory.Exists(memberTree) ? await MergeBaseAsync(memberTree, headTip, ct).ConfigureAwait(false) : null;
            var (live, waiting) = MemberReads(records, member);
            list.Add(new TogetherCandidate(member.Id, member.Repository, memberTree,
                Directory.Exists(memberTree) ? await BranchAsync(memberTree, ct).ConfigureAwait(false) ?? "" : "", @base ?? member.BaseCommit ?? "")
            {
                Quest = member.Quest, Teammate = member.Teammate, Live = live, StartWaiting = waiting,
                StepsOn = stepsOn is not null && await stepsOn(member.Id, ct).ConfigureAwait(false),
            });
        }

        return (head, list, null);
    }

    /// <summary>
    /// Whether a part is running anywhere on the member's line of work (the member's record, the ones it continues and the ones that
    /// continue it), and whether a start of it is queued: read from every record of the line, never the ticked one alone.
    /// </summary>
    public static (bool Live, bool StartWaiting) MemberReads(IReadOnlyList<SessionRecord> records, SessionRecord member)
    {
        var line = new Dictionary<string, SessionRecord>(StringComparer.OrdinalIgnoreCase) { [member.Id] = member };
        var byId = records.GroupBy(each => each.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        for (var before = member; before.From is { } from && byId.TryGetValue(from, out var earlier) && line.TryAdd(earlier.Id, earlier); before = earlier) { }
        for (var grew = true; grew;)
        {
            grew = false;
            foreach (var each in records)
            {
                if (each.From is { } from && line.ContainsKey(from) && line.TryAdd(each.Id, each)) grew = true;
            }
        }

        var waiting = line.Values.Any(each => each.State == "queued");
        return (line.Values.Any(each => each.Live && each.State != "queued"), waiting);
    }

    /// <summary><i>Ask again</i> after a failed check: the words said once more under a key of their own; the sentence where it cannot be.</summary>
    public async Task<string?> AskHeadAgainAsync(string session, CancellationToken ct)
    {
        var store = new TogetherOperations(home);
        if (HeadOf(store, session) is not { } operation) return "Nothing was asked again: nothing is being brought into this session.";
        if (operation.Phase != TogetherPhase.Checking || operation.Why is null) return "Nothing was asked again: its check has not failed.";
        if (store.AskedAgain(operation.Id, now()) is not { } again) return "Nothing was asked again: it moved meanwhile.";
        SayAnswer? said;
        try
        {
            said = await say(again.Head.Record, Words(again), again.Key, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            said = new SayAnswer(false, error.Message);
        }

        return said is { Kept: true } ? null
            : $"The head could not be told again ({(said?.Message ?? "nothing here reaches its conversation").TrimEnd('.')}); let them go, or ask again later.";
    }

    /// <summary><i>Let them go</i> under the door's locks, before the landing; what the head made stays. The sentence where it cannot be.</summary>
    public async Task<string?> LetGoAsync(string session, CancellationToken ct)
    {
        var store = new TogetherOperations(home);
        if (HeadOf(store, session) is not { } operation) return "Nothing was let go: nothing is being brought into this session.";
        var (locks, held) = await TogetherLocks.TakeAsync(home, null, operation.Repository,
            [operation.Head.Tree, .. operation.Members.Select(each => each.Tree)], Wait, ct).ConfigureAwait(false);
        if (locks is null) return $"Nothing was let go: {held}.";
        using (locks)
        {
            return store.LetGo(operation.Id, now()) is null ? $"Nothing was let go: it is {operation.Phase} now, past letting go." : null;
        }
    }

    private static TogetherOperation? HeadOf(TogetherOperations store, string session) =>
        store.All().LastOrDefault(each => each.IsOpen && (string.Equals(each.Head.Record, session, StringComparison.OrdinalIgnoreCase)
            || string.Equals(each.Head.Line, session, StringComparison.OrdinalIgnoreCase)));

    private static async Task<string?> BranchAsync(string tree, CancellationToken ct)
    {
        if (!Directory.Exists(tree)) return null;
        var (code, branch, _) = await WorkingTree.GitAsync(tree, ["--no-optional-locks", "rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
        return code == 0 && branch.Trim() is { Length: > 0 } name ? name : null;
    }

    private static async Task<string?> MergeBaseAsync(string tree, string other, CancellationToken ct)
    {
        var (code, found, _) = await WorkingTree.GitAsync(tree, ["--no-optional-locks", "merge-base", "HEAD", other], ct).ConfigureAwait(false);
        return code == 0 && found.Trim() is { Length: > 0 } commit ? commit : null;
    }

    /// <summary>How long the press waits for a landing of a tree it binds.</summary>
    internal static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    public async Task<TogetherPressed> PressAsync(TogetherHeadCandidate head, IReadOnlyList<TogetherCandidate> members, CancellationToken ct)
    {
        if (RecordsRefusal(head, members) is { } refused) return new(null, refused);

        TogetherOperation operation;
        var (locks, held) = await TogetherLocks.TakeAsync(home, head.Workspace, head.Repository, [head.Tree, .. members.Select(each => each.Tree)], Wait, ct)
            .ConfigureAwait(false);
        if (locks is null) return new(null, $"Nothing was brought together: {held}.");
        using (locks)
        {
            if (await TipAsync(head.Tree, ct).ConfigureAwait(false) is not { } headTip)
            {
                return new(null, "Nothing was brought together: the head's tree is not here, or its commit could not be read.");
            }

            var pinned = new List<TogetherMember>();
            foreach (var member in members)
            {
                if (await GitRefusalAsync(member, ct).ConfigureAwait(false) is { } why) return new(null, $"Nothing was brought together: `{member.Session}` {why}.");
                pinned.Add(new TogetherMember(member.Session, member.Tree, member.Branch, member.Base, (await TipAsync(member.Tree, ct).ConfigureAwait(false))!)
                {
                    Quest = member.Quest,
                    Unit = member.Unit,
                    Submission = member.Submission,
                });
            }

            try
            {
                var (opened, already) = new TogetherOperations(home).Open(
                    head.Repository, new TogetherHead(head.Line, head.Record, head.Tree, head.Branch, headTip), pinned, now());
                if (opened is null) return new(null, $"Nothing was brought together: {already}");
                operation = opened;
            }
            catch (IOException error)
            {
                return new(null, $"Nothing was brought together: its record could not be kept ({error.Message}).");
            }
        }

        SayAnswer? said;
        try
        {
            said = await say(head.Record, Words(operation), operation.Key, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            said = new SayAnswer(false, error.Message);
        }

        if (said is { Kept: true }) return new(operation, null);
        new TogetherOperations(home).LetGo(operation.Id, now());
        return new(null, $"Nothing was brought together: the head could not be told ({(said?.Message ?? "nothing here reaches its conversation").TrimEnd('.')}).");
    }

    /// <summary>The fixed words, shown as the rebase words are: each member's branch, base and tip, oldest first.</summary>
    public static string Words(TogetherOperation operation) =>
        "Bring the work of these sessions into yours. "
        + string.Join(" ", operation.Members.Select(member =>
            $"For `{member.Session}`, replay `{member.Branch}`'s commits from `{member.Base}` to `{member.Tip}` onto your branch, oldest first."))
        + " Settle each conflict in the commit that meets it, by what its session meant, naming the commit it settles in a "
        + "`Supersedes: <commit>` trailer; make no merge commit; say when done.";

    /// <summary>What the records refuse before any lock: another repository, a teammate, a live part, a waiting start, a workflow stepping on, a head.</summary>
    private static string? RecordsRefusal(TogetherHeadCandidate head, IReadOnlyList<TogetherCandidate> members)
    {
        if (members.Count == 0) return "Nothing was brought together: no session was ticked.";
        if (!head.TakesWords) return "Nothing was brought together: the head takes no words now.";
        if (head.RunsElsewhere) return "Nothing was brought together: the head runs elsewhere, where its tree is not here.";
        foreach (var member in members)
        {
            var why = !string.Equals(member.Repository, head.Repository, StringComparison.OrdinalIgnoreCase) ? "is another repository's session"
                : member.Teammate ? "is a teammate's session, brought in only from their own machine"
                : member.Live ? "has a part running: bring it in once it ends"
                : member.StartWaiting ? "has a start waiting in its tree"
                : member.StepsOn ? "has a workflow that steps on past its landing, the only maker of its unit's request"
                : null;
            if (why is not null) return $"Nothing was brought together: `{member.Session}` {why}.";
        }

        return null;
    }

    /// <summary>What git refuses under the locks: a lost tree, an unread tip, uncommitted work, a merge commit, a landing or a request.</summary>
    private async Task<string?> GitRefusalAsync(TogetherCandidate member, CancellationToken ct)
    {
        if (!Directory.Exists(member.Tree) || await TipAsync(member.Tree, ct).ConfigureAwait(false) is not { } tip)
        {
            return "has no tree here, or its tip could not be read";
        }

        var (statusCode, status, _) = await WorkingTree.GitAsync(member.Tree, ["--no-optional-locks", "status", "--porcelain"], ct).ConfigureAwait(false);
        if (statusCode != 0) return "has a tree whose state could not be read";
        if (status.Trim().Length > 0) return "has uncommitted work in its tree";
        var (mergesCode, merges, _) = await WorkingTree.GitAsync(
            member.Tree, ["--no-optional-locks", "rev-list", "--merges", $"{member.Base}..{tip}"], ct).ConfigureAwait(false);
        if (mergesCode != 0) return "has commits that could not be read";
        if (merges.Trim().Length > 0) return "has a merge commit among its commits, which a replay would drop";
        if (new LandedBranches(home).All().Any(each => string.Equals(each.Session, member.Session, StringComparison.OrdinalIgnoreCase)
                && string.Equals(each.Tip, tip, StringComparison.OrdinalIgnoreCase)))
        {
            return "has landed this work already";
        }

        try
        {
            if (new IntegrationStore(home).OpenFor(member.Repository, member.Branch) is not null) return "has an integration request open";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            return "has integration requests that could not be read";
        }

        return null;
    }

    private static async Task<string?> TipAsync(string tree, CancellationToken ct)
    {
        if (!Directory.Exists(tree)) return null;
        var (code, head, _) = await WorkingTree.GitAsync(tree, ["--no-optional-locks", "rev-parse", "--verify", "--quiet", "HEAD^{commit}"], ct)
            .ConfigureAwait(false);
        return code == 0 && head.Trim() is { Length: > 0 } tip ? tip : null;
    }
}
