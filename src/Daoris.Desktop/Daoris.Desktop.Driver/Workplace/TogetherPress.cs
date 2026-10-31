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
internal sealed class TogetherPress(string home, RebaseTelling.Say say, Func<DateTimeOffset> now)
{
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
    internal static string Words(TogetherOperation operation) =>
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
