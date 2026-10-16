namespace Daoris.Driver;

/// <summary>What a branch's work is, as far as git and the record can say (FABRIC-END2a, the fabric events design §B2).</summary>
internal enum EndState
{
    /// <summary>Nothing was handed on and the line does not hold the tip: at most the agent's word that it is done.</summary>
    Claim,

    /// <summary>The work left the tree into the person's flow (a landing's branch, a pull request, a branch of the person's) and the line does not hold it yet.</summary>
    Handed,

    /// <summary>The line holds the committed tip, proven by git.</summary>
    Ended,

    /// <summary>A read the judgement needed failed: absent is never zero (D57), so nothing is said.</summary>
    Unknown,
}

/// <summary>The proof an end rests on: the codes the clean-up and the sweep already write (<see cref="LandedKind"/>, <see cref="SweepKind"/>).</summary>
internal static class EndBy
{
    /// <summary>The line holds a session branch that never moved from where it grew: nothing beyond the line.</summary>
    public const string Empty = "empty";

    /// <summary>Every commit of it is on the line, by ancestry.</summary>
    public const string Merged = LandedKind.Merged;

    /// <summary>Every file it changed reads on the line as it left it, by content.</summary>
    public const string OnLine = LandedKind.OnLine;

    /// <summary>The landing's pull request completed, and git confirms it here.</summary>
    public const string PullRequest = LandedKind.PullRequest;

    /// <summary>A session branch inside the branch a landing made, whose own end was proven.</summary>
    public const string Landed = "landed";

    /// <summary>A session branch a completed pull request carried.</summary>
    public const string Carried = "carried";
}

/// <summary>
/// How a piece of work ends (the design's §B3): resolved as <i>Accept</i> resolves it, or from what the landing recorded. There is no
/// <c>end</c> setting. The cycle is an input to <see cref="EndJudgement"/>, so a workspace's or a repository's own definition of its
/// end plugs in where it is resolved (FABRIC-CHAIN1).
/// </summary>
/// <param name="Form"><see cref="LandingForm.Merge"/>, <see cref="LandingForm.Branch"/>, or <see cref="None"/> where nothing was ever handed by Daoris.</param>
/// <param name="Plugin">A plugin pushes the branch and opens its pull request, so its completed answer is a proof.</param>
/// <param name="Squash">The landing branch is one commit, the recorded squash tip vouching for the session's commits.</param>
/// <param name="Workflow">The named workflow whose landing decided it (WORKFLOW1f); null under Current.</param>
internal sealed record EndCycle(string Form, bool Plugin = false, bool Squash = false, LandingWorkflow? Workflow = null)
{
    /// <summary>The form of work no landing handed: the person merges by hand.</summary>
    public const string None = "none";

    /// <summary>No landing: the person's own git ends it.</summary>
    public static readonly EndCycle NoLanding = new(None);

    /// <summary>Its name as the design's table says it.</summary>
    public string Name => Form switch
    {
        LandingForm.Merge => "merge",
        LandingForm.Branch when Squash => "squash",
        LandingForm.Branch when Plugin => "branch with a plugin",
        LandingForm.Branch => "branch",
        _ => "no landing",
    };

    /// <summary>A landing's rule as it stands, from the version's landing step or the repository's rule.</summary>
    public static EndCycle Of(Landing landing, LandingWorkflow? workflow = null)
    {
        var branch = landing.Rule.Form == LandingForm.Branch;
        return new(landing.Rule.Form, branch && landing.Rule.Plugin is not null, branch && landing.Rule.Squash, workflow);
    }

    /// <summary>
    /// The cycle for work not yet handed, through the effective process <i>Accept</i> reads (<c>TreeLands.LandAsync</c>): under a
    /// named workflow its bound version's landing, else the repository's rule, else its workspace's (<see cref="LandingRules.Choose"/>).
    /// </summary>
    public static EndCycle Accepts(DriverConfig config, string repository, string? workspace, WorkflowProcess? process = null)
    {
        var named = process is { Named: true } ? process : null;
        return Of(named?.Landing ?? LandingRules.Choose(config, repository, workspace), named?.Record);
    }

    /// <summary>
    /// The cycle for handed work, from what its landing recorded: a recorded landing is the branch form (the merge form keeps no
    /// record), its plugin and its pull request, <see cref="LandedBranch.Squashed"/>, and the workflow's version.
    /// </summary>
    public static EndCycle Recorded(LandedBranch entry) => new(
        LandingForm.Branch, entry.Plugin is not null || entry.Rule?.Plugin is not null || entry.PullRequestState is not null,
        entry.Squashed is not null, entry.Workflow);
}

/// <summary>Which branch of the unit is judged (§B1): the session's own, or the landing branch that carries it.</summary>
internal enum EndRole
{
    Session,
    Landing,
}

/// <summary>
/// The branch judged: the commit it is judged at (every proof asks that one id), and what the record knows of it.
/// </summary>
/// <param name="Tip">The committed tip judged. For a chain's advance, its <see cref="LandedAdvance.To"/>.</param>
/// <param name="Landing">The landing entry that made the branch (role Landing) or carries it (role Session); null where none does.</param>
internal sealed record EndSubject(string Branch, string Tip, EndRole Role, LandedBranch? Landing = null)
{
    /// <summary>Whether the branch moved from where it grew; false names an end <see cref="EndBy.Empty"/>.</summary>
    public bool Worked { get; init; } = true;

    /// <summary>The agent's word that it is done: stated so that it is visibly not read.</summary>
    public bool Claimed { get; init; }

    /// <summary>Whether its tree holds uncommitted work. It does not stop the committed tip's end; a reader says both (§B7).</summary>
    public bool? Uncommitted { get; init; }
}

/// <summary>One proof's read: found where, found nowhere, or not read.</summary>
internal readonly record struct Held(bool Read, string? Where)
{
    public static readonly Held Unread = new(false, null);

    public static readonly Held None = new(true, null);

    public static Held At(string where) => new(true, where);

    public bool Found => Where is not null;
}

/// <summary>What git and the record said, gathered through the proofs' own seams, for <see cref="EndJudgement.Judge"/> to read.</summary>
/// <param name="Line">The repository's line (D86) the cycle named, or null where none is set.</param>
internal sealed record EndFacts(string? Line)
{
    /// <summary>The form of the line (<c>main</c>, <c>origin/main</c>) whose history holds the tip.</summary>
    public Held Ancestry { get; init; } = Held.Unread;

    /// <summary>Where the branch's work reads the same by content: a form of the line, or a branch of the person's.</summary>
    public Held Content { get; init; } = Held.None;

    /// <summary>What the landing's kept pull request answer proves here (<see cref="PullRequestAsks.VerdictAsync"/>); null where none is kept.</summary>
    public PullRequestVerdict? Verdict { get; init; }

    /// <summary>Whether a landing branch still stands holding the commit its landing made it at; null where it could not be read.</summary>
    public bool? Standing { get; init; } = true;

    /// <summary>Whether a branch of the person's (not a form of the line) holds every commit (D88); null where git could not say.</summary>
    public bool? OnPersonsBranch { get; init; }

    /// <summary>The landing branch of a completed pull request that carried the tip, line and merge commit confirmed here.</summary>
    public string? Carrier { get; init; }

    /// <summary>Whether the session's tip is at or under what the landing made: its branch, or the squash tip it recorded.</summary>
    public bool? Inside { get; init; }

    /// <summary>The carrying landing branch's own judgement, for a session branch.</summary>
    public EndVerdict? LandingEnd { get; init; }
}

/// <summary>
/// What the judgement says and the evidence for it: the proof's code, where it held, the commit and the line judged (the design's §B5
/// names each, so a stored end can stop holding when one moves).
/// </summary>
internal sealed record EndVerdict(EndState State, string? By, string? Where, string Evidence)
{
    /// <summary>The commit judged.</summary>
    public string? Tip { get; init; }

    /// <summary>The line the cycle named.</summary>
    public string? Line { get; init; }

    /// <summary>Its tree holds uncommitted work: the committed work may be on the line while the tree is not done.</summary>
    public bool Uncommitted { get; init; }

    public bool Ended => State == EndState.Ended;
}

/// <summary>
/// The one judgement of an end (FABRIC-END2a; the fabric events design §B3 and §B7): the cycles' proofs, which four places judged
/// alone (the clean-up, the sweep, the pull request's asks and the look's tidy), behind one answer: claim, handed, ended or unknown.
/// <see cref="Judge"/> is pure over <see cref="EndFacts"/>; <see cref="GatherAsync"/> reads them through the proofs' own seams.
/// </summary>
/// <remarks>
/// <para>Nothing is stored and nothing removed here (§B8), and no reader calls it yet: each caller keeps its own judgement until its
/// row moves it (END2b, END4).</para>
/// <para>🔴 <b>Never ended by a guess</b> (§B7): the agent's done, a remote branch reading <c>[gone]</c>, patch equivalence, a plugin's
/// word without its merge commit on the line here, an unread answer, a work only another branch of the person's holds. A dirty tree
/// does not make an end a guess, it is carried beside it.</para>
/// </remarks>
internal static class EndJudgement
{
    /// <summary>The answer for one branch, given the facts the proofs found.</summary>
    internal static EndVerdict Judge(EndCycle cycle, EndSubject subject, EndFacts facts)
    {
        var said = subject.Role == EndRole.Landing ? JudgeLanding(cycle, subject, facts) : JudgeSession(cycle, subject, facts);
        return said with { Tip = subject.Tip, Line = facts.Line, Uncommitted = subject.Uncommitted == true };
    }

    private static EndVerdict JudgeLanding(EndCycle cycle, EndSubject subject, EndFacts facts)
    {
        if (facts.Standing != true)
        {
            return new(EndState.Unknown, null, null, facts.Standing is null
                ? "git could not say whether the branch still holds what its landing made"
                : "it is no longer the branch its landing made, so its end is not judged");
        }

        if (facts.Ancestry.Found) return new(EndState.Ended, EndBy.Merged, facts.Ancestry.Where, $"every commit of it is on `{facts.Ancestry.Where}`");
        if (facts.Content is { Found: true } content && IsLine(content.Where!, facts.Line))
        {
            return new(EndState.Ended, EndBy.OnLine, content.Where, $"every file it changed reads on `{content.Where}` as it left it");
        }

        // A plugin's word is a proof only where git holds the merge commit on the line here and the branch is not beyond it (D148 point 4).
        if (cycle.Plugin && facts.Verdict is { Clears: true } cleared)
        {
            return new(EndState.Ended, EndBy.PullRequest, cleared.Form, $"its pull request completed and its merge commit is on `{cleared.Form}`");
        }

        if (!facts.Ancestry.Read) return new(EndState.Unknown, null, null, "git could not read the line, so whether it holds the branch is not said");
        var asked = facts.Verdict is { Clears: false, Code: { } code } ? $"; its pull request is not proof here ({code})" : "";
        return new(EndState.Handed, null, null, $"its landing made it and the line does not hold it yet{asked}");
    }

    private static EndVerdict JudgeSession(EndCycle cycle, EndSubject subject, EndFacts facts)
    {
        if (subject.Landing is { } landing && facts.Inside == true && facts.LandingEnd is { Ended: true } carried)
        {
            return new(EndState.Ended, EndBy.Landed, landing.Branch, $"its work is on `{landing.Branch}`, which the line holds ({carried.By})");
        }

        if (facts.Carrier is { } carrier)
        {
            return new(EndState.Ended, EndBy.Carried, carrier, $"a completed pull request, `{carrier}`, carried it and its merge commit is on the line");
        }

        if (facts.Ancestry.Found)
        {
            return new(EndState.Ended, subject.Worked ? EndBy.Merged : EndBy.Empty, facts.Ancestry.Where,
                subject.Worked ? $"every commit of it is on `{facts.Ancestry.Where}`" : $"it holds nothing `{facts.Ancestry.Where}` lacks");
        }

        if (facts.Content is { Found: true } content)
        {
            if (IsLine(content.Where!, facts.Line))
            {
                return new(EndState.Ended, EndBy.OnLine, content.Where, $"its work reads on `{content.Where}` by content");
            }

            // D88: landed only on another branch of the person's is kept until the line has it.
            return new(EndState.Handed, null, content.Where, $"its work is on `{content.Where}`, a branch of the person's, and the line does not hold it yet");
        }

        if (facts.OnPersonsBranch == true) return new(EndState.Handed, null, null, "a branch of the person's holds it and the line does not yet");
        if (subject.Landing is { } handed)
        {
            return new(EndState.Handed, null, handed.Branch, $"`{handed.Branch}` carries it and the line does not hold that yet ({cycle.Name})");
        }

        if (!facts.Ancestry.Read) return new(EndState.Unknown, null, null, "git could not read the line, so whether it holds the branch is not said");
        return new(EndState.Claim, null, null, subject.Claimed
            ? "its agent said it is done, which is a claim, not an end: the line does not hold it"
            : "nothing was handed on and the line does not hold it");
    }

    private static bool IsLine(string where, string? line) => line is not null && (where == line || where == $"origin/{line}");

    /// <summary>
    /// Read what <see cref="Judge"/> needs, each through the proof's own code: the line's forms and ancestry (<see cref="LandedProofs"/>),
    /// the landing's content proof (<see cref="LandedProofs.ProveAsync"/>) or a session branch's (<see cref="ContentHolds.HeldByContentAsync"/>),
    /// the kept pull request answer (<see cref="PullRequestAsks.VerdictAsync"/>) and D88's other branches (<see cref="TreeFollows.UnlandedAsync"/>).
    /// The refs come from <paramref name="refs"/>; what a snapshot does not hold (merge-base, content) git is asked, as those proofs do today.
    /// </summary>
    /// <param name="notHolders">The branches a landing made and recorded here, never a holder by content (SQUASHTIDY1c).</param>
    /// <param name="carriers">The completed pull requests that carried work here, from <see cref="PullRequestAsks.CarriersAsync"/>.</param>
    internal static async Task<EndFacts> GatherAsync(
        string root, EndSubject subject, string? line, IReadOnlySet<string> notHolders, IReadOnlyList<PullRequestCarrier>? carriers,
        CancellationToken ct, RefSnapshot? refs = null, EndVerdict? landingEnd = null)
    {
        refs ??= RefSnapshot.Take(root);
        var forms = await LandedProofs.LineFormsAsync(root, line, ct, refs).ConfigureAwait(false);
        var facts = new EndFacts(line) { Ancestry = forms.Count == 0 ? Held.Unread : await AncestryAsync(root, subject.Tip, forms, ct).ConfigureAwait(false) };
        if (subject.Role == EndRole.Landing && subject.Landing is { } entry)
        {
            var standing = await LandedProofs.StandingTipAsync(root, entry, ct, refs).ConfigureAwait(false);
            facts = facts with { Standing = standing is not null };
            if (facts.Ancestry.Found || standing is null) return facts;
            // A proof Unknown is no proof: a read git could not make never ends it.
            var proof = await LandedProofs.ProveAsync(root, subject.Tip, line, forms, ct).ConfigureAwait(false);
            if (proof.Kind == LandedKind.OnLine && proof.Where is { } on) facts = facts with { Content = Held.At(on) };
            if (entry.PullRequestState is { } kept)
            {
                facts = facts with { Verdict = await PullRequestAsks.VerdictAsync(root, line, forms, kept, standing, ct).ConfigureAwait(false) };
            }

            return facts;
        }

        if (subject.Landing is { } carrying)
        {
            facts = facts with { LandingEnd = landingEnd, Inside = await InsideAsync(root, subject.Tip, carrying, ct).ConfigureAwait(false) };
        }

        if (facts.Ancestry.Found) return facts;
        if (carriers is { Count: > 0 }
            && await PullRequestAsks.CarrierOfAsync(root, subject.Tip, carriers, ct).ConfigureAwait(false) is { } carrier)
        {
            facts = facts with { Carrier = carrier.Entry.Branch };
        }

        var content = await ContentHolds.HeldByContentAsync(root, subject.Tip, line, notHolders, ct, refs).ConfigureAwait(false);
        if (content is not null) facts = facts with { Content = Held.At(content.Where) };
        // Git's answer collapses "not held" and "could not say" in the content proof, so only the person's branches are read here.
        return facts with { OnPersonsBranch = await TreeFollows.UnlandedAsync(root, subject.Tip, ct).ConfigureAwait(false) is { } unlanded ? unlanded == 0 : null };
    }

    /// <summary>
    /// The whole judgement of one branch: its tip from <paramref name="refs"/>, the facts gathered, and for a session branch a landing
    /// carries, the landing branch's own end first.
    /// </summary>
    internal static async Task<EndVerdict> JudgeAsync(
        string root, EndCycle cycle, EndSubject subject, string? line, IReadOnlySet<string> notHolders, IReadOnlyList<PullRequestCarrier>? carriers,
        CancellationToken ct, RefSnapshot? refs = null)
    {
        refs ??= RefSnapshot.Take(root);
        EndVerdict? landingEnd = null;
        if (subject.Role == EndRole.Session && subject.Landing is { } landing
            && await TreeRefs.CommitOfAsync(root, $"refs/heads/{landing.Branch}", ct, refs).ConfigureAwait(false) is { } landingTip)
        {
            var landingSubject = new EndSubject(landing.Branch, landingTip, EndRole.Landing, landing);
            var landingFacts = await GatherAsync(root, landingSubject, line, notHolders, carriers, ct, refs).ConfigureAwait(false);
            landingEnd = Judge(EndCycle.Recorded(landing), landingSubject, landingFacts);
        }

        var facts = await GatherAsync(root, subject, line, notHolders, carriers, ct, refs, landingEnd).ConfigureAwait(false);
        return Judge(cycle, subject, facts);
    }

    /// <summary>The first form of the line whose history holds <paramref name="tip"/>; unread where git answered neither yes nor no.</summary>
    private static async Task<Held> AncestryAsync(string root, string tip, IReadOnlyList<LandedProofs.LineForm> forms, CancellationToken ct)
    {
        var read = true;
        foreach (var form in forms)
        {
            var (code, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", tip, form.Ref], ct).ConfigureAwait(false);
            if (code == 0) return Held.At(form.Name);
            if (code != 1) read = false;
        }

        return read ? Held.None : Held.Unread;
    }

    /// <summary>A session's tip at or under what the landing made: the squash tip it recorded where it squashed, else the branch's recorded tip.</summary>
    private static async Task<bool?> InsideAsync(string root, string tip, LandedBranch landing, CancellationToken ct)
    {
        var (code, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", tip, landing.Squashed ?? landing.Tip], ct).ConfigureAwait(false);
        return code switch { 0 => true, 1 => false, _ => null };
    }
}
