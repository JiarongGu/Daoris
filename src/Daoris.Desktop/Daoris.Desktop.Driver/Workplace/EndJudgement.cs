namespace Daoris.Driver;

/// <summary>What a branch's work is, as far as git and the record can say.</summary>
internal enum EndState
{
    /// <summary>Nothing was handed on and the line does not hold the tip: at most the agent's word that it is done.</summary>
    Claim,

    /// <summary>The work left the tree into the person's flow (a landing's branch, a pull request, a branch of the person's) and the line does not hold it yet.</summary>
    Handed,

    /// <summary>The line holds the committed tip, proven by git.</summary>
    Ended,

    /// <summary>A read the judgement needed failed: absent is never zero, so nothing is said.</summary>
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

    /// <summary>A part of a chain its workflow's <c>tidy</c> ended once the chain's pull request merged: its record, never a proof here.</summary>
    public const string Tidied = "tidied";

    /// <summary>
    /// A landing branch whose own proofs did not end it, inside another landing branch whose own proof did (as <see cref="LandedCleanups.JudgeLandedAsync"/> proves it): that one's history holds its commits.
    /// </summary>
    public const string Inside = LandedKind.Inside;
}

/// <summary>
/// A landing whose own proof ended it (<see cref="EndBy.Merged"/>, <see cref="EndBy.OnLine"/>, <see cref="EndBy.PullRequest"/>), at the tip it
/// is judged at: what a landing inside it rests on, and a session branch whose work it holds with no unit naming it.
/// </summary>
internal sealed record EndHost(LandedBranch Entry, string Tip, EndVerdict End);

/// <summary>
/// How a piece of work ends: resolved as <i>Accept</i> resolves it, or from what the landing recorded. There is no
/// <c>end</c> setting. The cycle is an input to <see cref="EndJudgement"/>, so a workspace's or a repository's own definition of its
/// end plugs in where it is resolved.
/// </summary>
/// <param name="Form"><see cref="LandingForm.Merge"/>, <see cref="LandingForm.Branch"/>, or <see cref="None"/> where nothing was ever handed by Daoris.</param>
/// <param name="Plugin">A plugin pushes the branch and opens its pull request, so its completed answer is a proof.</param>
/// <param name="Squash">The landing branch is one commit, the recorded squash tip vouching for the session's commits.</param>
/// <param name="Workflow">The named workflow whose landing decided it; null under Current.</param>
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

    /// <summary>A landing as it stands, from the run's bound <c>land</c>.</summary>
    public static EndCycle Of(Landing landing, LandingWorkflow? workflow = null)
    {
        var branch = landing.Form == LandingForm.Branch;
        return new(landing.Form, false, branch && landing.Squash, workflow);
    }

    /// <summary>
    /// The cycle for work not yet handed, through the process <i>Accept</i> reads (<c>TreeLands.LandAsync</c>): its bound version's
    /// landing, else the merge a process with no bound version declares.
    /// </summary>
    public static EndCycle Accepts(DriverConfig config, string repository, string? workspace, IWorkplaceProcess? process = null)
    {
        var named = process is { Named: true } ? process : null;
        return Of(named?.Landing ?? Landing.Merge, named?.Record);
    }

    /// <summary>
    /// The cycle for handed work, from what its landing recorded: a recorded landing is the branch form (the merge form keeps no
    /// record), its plugin and its pull request, <see cref="LandedBranch.Squashed"/>, and the workflow's version.
    /// </summary>
    public static EndCycle Recorded(LandedBranch entry) => new(
        LandingForm.Branch, entry.Plugin is not null || entry.Rule?.Plugin is not null || entry.PullRequestState is not null,
        entry.Squashed is not null, entry.Workflow);
}

/// <summary>Which branch of the unit is judged: the session's own, or the landing branch that carries it.</summary>
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

    /// <summary>Whether its tree holds uncommitted work. It does not stop the committed tip's end; a reader says both.</summary>
    public bool? Uncommitted { get; init; }

    /// <summary>
    /// A landing whose branch git said is missing (<see cref="LandedGone.Missing"/>), judged at the tip its landing recorded:
    /// only the line's ancestry of that commit ends it, or its content proof on a form of the line.
    /// </summary>
    public bool Gone { get; init; }
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
/// <param name="Line">The repository's line the cycle named, or null where none is set.</param>
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

    /// <summary>Whether a branch of the person's (not a form of the line) holds every commit; null where git could not say.</summary>
    public bool? OnPersonsBranch { get; init; }

    /// <summary>The landing branch of a completed pull request that carried the tip, line and merge commit confirmed here.</summary>
    public string? Carrier { get; init; }

    /// <summary>
    /// The form of the line (<c>main</c>, <c>origin/main</c>) that carrier's merge commit was found on, as the carried proof rests on it
    /// (<see cref="PullRequestCarrier.Rests"/>); null where it names none.
    /// </summary>
    public string? CarrierOn { get; init; }

    /// <summary>Whether the session's tip is at or under what the landing made: its branch, or the squash tip it recorded.</summary>
    public bool? Inside { get; init; }

    /// <summary>The carrying landing branch's own judgement, for a session branch.</summary>
    public EndVerdict? LandingEnd { get; init; }

    /// <summary>
    /// Whether a landing's content proof read the line and compared its files (<see cref="LandedProofs.Proof.Judgeable"/>), so it may still be
    /// inside another landing.
    /// </summary>
    public bool Judgeable { get; init; }

    /// <summary>The landing whose own proof ended it and whose branch holds this landing's tip; null where none does.</summary>
    public EndHost? Within { get; init; }
}

/// <summary>
/// What the judgement says and the evidence for it: the proof's code, where it held, the commit and the line judged (each is named, so a stored end can stop holding when one moves).
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
/// The one judgement of an end: the cycles' proofs, which four places judged
/// alone (the clean-up, the sweep, the pull request's asks and the look's tidy), behind one answer: claim, handed, ended or unknown.
/// <see cref="Judge"/> is pure over <see cref="EndFacts"/>; <see cref="GatherAsync"/> reads them through the proofs' own seams.
/// </summary>
/// <remarks>
/// <para>Nothing is stored and nothing removed here. <see cref="EndKeeping"/> keeps what it finds held; the look's
/// tidy takes its candidates from that and judges each again, and the sweep keeps its own judgement.</para>
/// <para>🔴 <b>Never ended by a guess</b>: the agent's done, a remote branch reading <c>[gone]</c>, patch equivalence, a plugin's
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
        if (subject.Gone) return JudgeGone(facts);
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

        // A plugin's word is a proof only where git holds the merge commit on the line here and the branch is not beyond it.
        if (cycle.Plugin && facts.Verdict is { Clears: true } cleared)
        {
            return new(EndState.Ended, EndBy.PullRequest, cleared.Form, $"its pull request completed and its merge commit is on `{cleared.Form}`");
        }

        // Its files were compared and differ, and a landing whose own proof
        // ended it holds its commits, so that one's history carries them onto the line. Never inside one ended only by being inside another.
        if (facts.Judgeable && facts.Within is { } within && OwnEnd(within.End))
        {
            return new(EndState.Ended, EndBy.Inside, within.Entry.Branch,
                $"it is inside `{within.Entry.Branch}`, whose work is on the line ({within.End.By})");
        }

        if (!facts.Ancestry.Read) return new(EndState.Unknown, null, null, "git could not read the line, so whether it holds the branch is not said");
        var asked = facts.Verdict is { Clears: false, Code: { } code } ? $"; its pull request is not proof here ({code})" : "";
        return new(EndState.Handed, null, null, $"its landing made it and the line does not hold it yet{asked}");
    }

    /// <summary>
    /// A landing whose branch git said is missing, judged at the tip its landing recorded. The line's ancestry of that commit is
    /// its end: every commit the landing recorded is on a form of the line. So is that commit's content proof on a form of
    /// the line, every file it changed reading there as the landing left it, which is what a platform's squash merge leaves once the person
    /// pulled and deleted the branch. Content only a branch of the person's holds is no end, and a pull request's word is read of a
    /// branch that stands, so neither ends anything here. A branch of its name standing again holding that commit is judged as one that
    /// stands, never here.
    /// </summary>
    private static EndVerdict JudgeGone(EndFacts facts)
    {
        if (facts.Standing != false)
        {
            return new(EndState.Unknown, null, null, "its branch stands again holding what its landing made, so it is not judged as gone");
        }

        if (facts.Ancestry.Found)
        {
            return new(EndState.Ended, EndBy.Merged, facts.Ancestry.Where, $"every commit its landing recorded is on `{facts.Ancestry.Where}`, and its branch went");
        }

        if (facts.Content is { Found: true } content && IsLine(content.Where!, facts.Line))
        {
            return new(EndState.Ended, EndBy.OnLine, content.Where,
                $"every file its landing's recorded commit changed reads on `{content.Where}` as it left it, and its branch went");
        }

        if (!facts.Ancestry.Read) return new(EndState.Unknown, null, null, "git could not read the line, so whether it holds the branch is not said");
        return new(EndState.Handed, null, null, "its branch went, and the line holds the commit its landing recorded neither by ancestry nor by content");
    }

    private static EndVerdict JudgeSession(EndCycle cycle, EndSubject subject, EndFacts facts)
    {
        if (subject.Landing is { } landing && facts.Inside == true && facts.LandingEnd is { Ended: true } carried)
        {
            return new(EndState.Ended, EndBy.Landed, landing.Branch, $"its work is on `{landing.Branch}`, which the line holds ({carried.By})");
        }

        if (facts.Carrier is { } carrier)
        {
            return new(EndState.Ended, EndBy.Carried, facts.CarrierOn ?? carrier,
                $"a completed pull request, `{carrier}`, carried it and its merge commit is on {(facts.CarrierOn is { } on ? $"`{on}`" : "the line")}");
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

            // Landed only on another branch of the person's is kept until the line has it.
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
    /// A landing's end by its own proof: merged, on the line by content, or its pull request's confirmed merge, as the clean-up's
    /// <c>proven</c> are. Only such a landing holds another inside it, or a session branch no unit names.
    /// </summary>
    internal static bool OwnEnd(EndVerdict? verdict) => verdict is { Ended: true, By: EndBy.Merged or EndBy.OnLine or EndBy.PullRequest };

    /// <summary>
    /// Read what <see cref="Judge"/> needs, each through the proof's own code: the line's forms and ancestry (<see cref="LandedProofs"/>),
    /// the landing's content proof (<see cref="LandedProofs.ProveAsync"/>) or a session branch's (<see cref="ContentHolds.HeldByContentAsync"/>),
    /// the kept pull request answer (<see cref="PullRequestAsks.VerdictAsync"/>) and the other branches (<see cref="TreeFollows.UnlandedAsync"/>).
    /// The refs come from <paramref name="refs"/>; what a snapshot does not hold (merge-base, content) git is asked, as those proofs do today.
    /// </summary>
    /// <param name="notHolders">The branches a landing made and recorded here, never a holder by content.</param>
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
            if (facts.Ancestry.Found) return facts;
            // A landing whose branch went is judged only while it is gone (one standing again is judged as one that stands); a
            // standing one only while it is the branch its landing made.
            if (subject.Gone ? standing is not null : standing is null) return facts;
            // A proof Unknown is no proof: a read git could not make never ends it. A gone landing's is read at the
            // commit its landing recorded, which a platform's squash merge leaves on no ancestry of the line.
            var proof = await LandedProofs.ProveAsync(root, subject.Tip, line, forms, ct).ConfigureAwait(false);
            if (proof.Kind == LandedKind.OnLine && proof.Where is { } on) facts = facts with { Content = Held.At(on) };
            // Its files compared, so it may still be inside another landing (the keeper asks, WithinAsync).
            facts = facts with { Judgeable = proof.Judgeable };
            // A pull request's word is read of a branch that stands, so none is asked for a gone one, whose branch reads null here.
            if (standing is not null && entry.PullRequestState is { } kept)
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
            // After the merge, the carried proof rests on the form of the line its merge commit is found on (and, through a
            // cleared tip, on that branch at it), which the carried tidy verifies. The form stands once the platform and the person deleted the
            // carrier's branch, so the kept fact names it, never the gone branch, whose commit no later look can read.
            var on = forms.FirstOrDefault(form => carrier.Rests.Any(rest => string.Equals(rest.Ref, form.Ref, StringComparison.Ordinal)))?.Name;
            facts = facts with { Carrier = carrier.Entry.Branch, CarrierOn = on };
        }

        var content = await ContentHolds.HeldByContentAsync(root, subject.Tip, line, notHolders, ct, refs).ConfigureAwait(false);
        // A proof git could not read (`Unread`) is never a hold, so it never ends the work by content; a later row
        // may carry it as the judgement's unknown.
        if (content.Hold is { } hold) facts = facts with { Content = Held.At(hold.Where) };
        return facts with { OnPersonsBranch = await UnheldTips.HeldAsync(root, subject.Tip, refs, ct).ConfigureAwait(false) };
    }

    /// <summary>
    /// The whole judgement of one branch: its tip from <paramref name="refs"/>, the facts gathered, and for a session branch a landing
    /// carries, the landing branch's own end first.
    /// </summary>
    /// <param name="landingEnd">For a session branch, its landing's end where the caller judged it already this pass (the keeper
    /// judges every landing first, an <see cref="EndBy.Inside"/> end included), so it is not judged again; null judges it here.</param>
    internal static async Task<EndVerdict> JudgeAsync(
        string root, EndCycle cycle, EndSubject subject, string? line, IReadOnlySet<string> notHolders, IReadOnlyList<PullRequestCarrier>? carriers,
        CancellationToken ct, RefSnapshot? refs = null, EndVerdict? landingEnd = null)
    {
        refs ??= RefSnapshot.Take(root);
        if (landingEnd is null && subject.Role == EndRole.Session && subject.Landing is { } landing
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
            var code = await AncestorOfAsync(root, tip, form, ct).ConfigureAwait(false);
            if (code == 0) return Held.At(form.Name);
            if (code != 1) read = false;
        }

        return read ? Held.None : Held.Unread;
    }

    /// <summary>
    /// <c>merge-base --is-ancestor</c>'s code for <paramref name="tip"/> against a form of the line: 0 held, 1 not, else unread. Where both are
    /// full commit ids it asks <c>merge-base</c> alone, which is the tip exactly when the line holds it, and which the content proof asks of the
    /// same pair next (a judgement keeps it once), so a branch the line does not hold costs one start of the pair, not two.
    /// </summary>
    private static async Task<int> AncestorOfAsync(string root, string tip, LandedProofs.LineForm form, CancellationToken ct)
    {
        if (form.At is not { } at || !IsFullId(tip) || !IsFullId(at))
        {
            return (await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", tip, form.Rev], ct).ConfigureAwait(false)).Code;
        }

        var (code, shared, _) = await WorkingTree.GitAsync(root, ["merge-base", tip, at], ct).ConfigureAwait(false);
        return code switch
        {
            0 => string.Equals(shared.Trim(), tip, StringComparison.OrdinalIgnoreCase) ? 0 : 1,
            1 => 1,
            _ => 2,
        };
    }

    private static bool IsFullId(string id) => id.Length is 40 or 64 && id.All(Uri.IsHexDigit);

    /// <summary>
    /// The first of <paramref name="hosts"/>
    /// whose branch, at the tip it was judged at, holds <paramref name="tip"/> by ancestry; null where none does, or git could not say. One
    /// <c>merge-base --is-ancestor</c> a host, until one holds it.
    /// </summary>
    internal static async Task<EndHost?> WithinAsync(string root, string tip, IEnumerable<EndHost> hosts, CancellationToken ct)
    {
        foreach (var host in hosts)
        {
            var (code, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", tip, host.Tip], ct).ConfigureAwait(false);
            if (code == 0) return host;
        }

        return null;
    }

    /// <summary>
    /// The landing, newest first, whose record holds a session branch's <paramref name="tip"/> where no unit names one: the session
    /// tip its squash recorded (as <see cref="TreeSquashes.SquashedHoldAsync"/> reads a squashing landing's record), else the tip it made, which
    /// is what <see cref="InsideAsync"/> asks of a landing a unit names. Only <paramref name="hosts"/> are asked, the landings whose own proof
    /// ended them, so the line holds what each made. One <c>rev-list</c> for them all first, as the squash's record is read: where something of
    /// the tip is outside every one, none holds it.
    /// </summary>
    internal static async Task<EndHost?> HolderAsync(string root, string tip, IReadOnlyList<EndHost> hosts, CancellationToken ct)
    {
        if (hosts.Count == 0) return null;
        var (code, outside, _) = await WorkingTree.GitAsync(
            root, ["rev-list", "--ignore-missing", "-n", "1", tip, "--not", .. hosts.Select(host => host.Entry.Squashed ?? host.Entry.Tip)], ct).ConfigureAwait(false);
        if (code != 0 || outside.Trim().Length > 0) return null;
        foreach (var host in hosts.Reverse())
        {
            var (under, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", tip, host.Entry.Squashed ?? host.Entry.Tip], ct).ConfigureAwait(false);
            if (under == 0) return host;
        }

        return null;
    }

    /// <summary>A session's tip at or under what the landing made: the squash tip it recorded where it squashed, else the branch's recorded tip.</summary>
    private static async Task<bool?> InsideAsync(string root, string tip, LandedBranch landing, CancellationToken ct)
    {
        var (code, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", tip, landing.Squashed ?? landing.Tip], ct).ConfigureAwait(false);
        return code switch { 0 => true, 1 => false, _ => null };
    }
}
