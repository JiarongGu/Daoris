using System.Globalization;
using System.Text.Json;
using static Daoris.Driver.Json;
using static Daoris.Driver.LandedBranchJson;

namespace Daoris.Driver;

/// <summary>
/// The branches this machine's landings made: <c>&lt;home&gt;/landings.json</c>. Written at the
/// landing, read by the clean-up and the hand-off, and kept as a trace once the branch is gone, each
/// repository's newest <see cref="TracesKept"/> traces.
/// </summary>
/// <remarks>
/// <para><b>Machine-local, under the home, never in the repository.</b> Which branches a landing on
/// this machine made is a fact about this machine, like its trees: another machine's landings are its
/// own, and a tracked list would put one person's branches in everyone's history.</para>
///
/// <para><b>The record is what makes a branch Daoris's to judge.</b> A branch a pattern happens to match
/// is not enough — <c>feature/{quest}-{slug}</c> is how people name their own branches — so the clean-up
/// and the hand-off act only on what is written here, and only while the branch still holds the commit
/// the landing made it at.</para>
///
/// <para><b>A file that does not read is no record at all</b>, never a failure: a landing has already
/// made its branch and is owed its answer, and a branch nobody recorded is never judged — the safe side.</para>
/// </remarks>
public sealed class LandedBranches(string home)
{
    public const string FileName = "landings.json";

    /// <summary>
    /// How many traces of gone branches each repository keeps: the newest, by when each went. A standing entry
    /// is never dropped. Every reader parses the whole file, and a trace is kept only so a session's review can say where
    /// its work went, which is asked of recent sessions.
    /// </summary>
    public const int TracesKept = 50;

    // One writer at a time in this process: two presses in one shell would otherwise read the same file and
    // the second write would drop the first's branch.
    private static readonly object Gate = new();

    private static readonly RecordList<LandedBranch> List = new(Gate, "branches", Read, ToJson, Bounded);

    public string FilePath => DaorisHome.Entry(home, HomeEntry.Landings);

    /// <summary>The record's lock across processes: the loop and a verb beside it edit this file one at a time.</summary>
    public string LockPath => RecordLock.PathOf(home, FileName);

    /// <summary>Every branch recorded that still stands, in the order they landed: what the clean-up, the hand-off and Ask Daoris act on.</summary>
    public IReadOnlyList<LandedBranch> All() => [.. Everything().Where(entry => entry.GoneAt is null)];

    /// <summary>
    /// The newest landing of one session, standing or a trace: what its review says of where its work
    /// went, since a tidy took its tree and the clean-up may have taken the branch since. A session whose done moved a
    /// chain's branch on finds that branch's landing.
    /// </summary>
    public LandedBranch? Landing(string session) => Everything().LastOrDefault(entry => entry.Names(session));

    /// <summary>
    /// Every branch <paramref name="plugin"/> answered that it pushed, standing or a trace, in the order they landed:
    /// what a plugin's Activity lists as its pushes.
    /// </summary>
    public IReadOnlyList<LandedBranch> PushedBy(string plugin) =>
        [.. Everything().Where(entry => entry.Pushed && string.Equals(entry.Plugin, plugin, StringComparison.OrdinalIgnoreCase))];

    /// <summary>
    /// Every entry, standing and traces, in the order they landed: what a work reads for the landings that name its sessions
    /// (<see cref="AskWork"/>), since a session a landing names keeps its tree whatever git says.
    /// </summary>
    public IReadOnlyList<LandedBranch> Entries() => Everything();

    /// <summary>Every entry, standing and traces, in the order they landed.</summary>
    private IReadOnlyList<LandedBranch> Everything() => List.Read(FilePath);

    /// <summary>The recorded branch of that name in that repository, or null.</summary>
    public LandedBranch? Of(string repository, string branch) =>
        All().LastOrDefault(entry => Same(entry, repository, branch));

    /// <summary>
    /// The recorded branches a person names — by the session that landed it, or by the branch — in one
    /// repository where they say which. The newest first, since a session's latest landing is the one it means.
    /// </summary>
    public IReadOnlyList<LandedBranch> Find(string sessionOrBranch, string? repository = null) =>
        [.. All()
            .Where(entry => repository is null || string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase))
            .Where(entry => entry.Names(sessionOrBranch) || string.Equals(entry.Branch, sessionOrBranch, StringComparison.Ordinal))
            .Reverse()];

    /// <summary>
    /// The landings a person names — by the session that landed it, or by the branch — standing or traces, in one repository
    /// where they say which, the newest first: what <c>trees state</c> asks about, since a trace's pull request may
    /// still carry a session branch that stands.
    /// </summary>
    public IReadOnlyList<LandedBranch> FindAll(string sessionOrBranch, string? repository = null) =>
        [.. Everything()
            .Where(entry => repository is null || string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase))
            .Where(entry => entry.Names(sessionOrBranch) || string.Equals(entry.Branch, sessionOrBranch, StringComparison.Ordinal))
            .Reverse()];

    /// <summary>
    /// Record one, replacing any standing entry for the same branch in the same repository. An earlier session's trace
    /// of that name stays, since its review still says where that session's work went.
    /// </summary>
    public void Record(LandedBranch entry) => Edit(all => [.. all.Where(each => !Standing(each, entry.Repository, entry.Branch)), entry]);

    /// <summary>The pull request a plugin's <c>pull-request</c> step opened for the branch (G8), kept on its entry beside the plugin that opened it.</summary>
    public void PullRequested(string repository, string branch, string plugin, string address) => Edit(all =>
        [.. all.Select(each => Standing(each, repository, branch) ? each with { Plugin = plugin, PullRequest = address } : each)]);

    /// <summary>The commit Daoris's own push act left on origin for the branch (G8), kept on its entry for the <c>push</c> node to read.</summary>
    public void Pushed(string repository, string branch, string tip) => Edit(all =>
        [.. all.Select(each => Standing(each, repository, branch) ? each with { Pushed = true, PushedTip = tip } : each)]);

    /// <summary>What a run's <c>tidy</c> ended of the branch's chain, kept on its entry for the node, the keeper and the press to read.</summary>
    public void Tidied(string repository, string branch, ChainTidied tidied) => Edit(all =>
        [.. all.Select(each => Standing(each, repository, branch) ? each with { Tidied = tidied } : each)]);

    /// <summary>
    /// A later done moved the standing branch on as a fast-forward, or by one more squashed commit:
    /// its tip is the advance's, the session tip squashed moves with it where the advance squashed one, and the
    /// advance is kept with the rest. The branch stays the landing's, so the clean-up and the hand-off judge it at its new tip.
    /// </summary>
    /// <param name="unit">The advancing session's branch work unit, added to <see cref="LandedBranch.Units"/> where it is new.</param>
    public void Advanced(string repository, string branch, LandedAdvance advance, string? unit = null) => Edit(all =>
        [.. all.Select(each => Standing(each, repository, branch)
            ? each with
            {
                Tip = advance.To, Squashed = advance.Squashed ?? each.Squashed, Advances = [.. each.Advances, advance],
                Units = unit is null || each.Units.Contains(unit, StringComparer.Ordinal) ? each.Units : [.. each.Units, unit],
            }
            : each)]);

    /// <summary>
    /// The end judgement's write (<see cref="EndKeeping"/>, its only caller): the standing entry's held fact, or none where it
    /// stopped holding. A trace is never written again.
    /// </summary>
    internal void Hold(LandedBranch entry, HeldFact? held) => Edit(all =>
        [.. all.Select(each => each.GoneAt is null && SameEntry(each, entry) ? each with { Held = held } : each)]);

    /// <summary>
    /// The end judgement's write for a landing whose branch git said is missing (<see cref="EndKeeping.KeepGoneAsync"/>, its only
    /// caller): the fact the line's ancestry proved at the tip its landing recorded, kept as the entry retires with git's word
    /// (<see cref="LandedGone.Missing"/>), on that landing alone (<see cref="SameLanding"/>). A standing entry becomes a trace; a trace that
    /// says no why, or says it moved, gains it, and no longer names where it moved. One with a fact already, one Daoris removed, and one that
    /// says another why are left as they are: a trace with its fact is never judged again.
    /// </summary>
    /// <returns>Whether it is written.</returns>
    internal bool HoldGone(LandedBranch entry, HeldFact held)
    {
        var wrote = false;
        Edit(all =>
        {
            var now = DateTimeOffset.UtcNow;
            wrote = false;
            return [.. all.Select(each =>
            {
                if (!SameLanding(each, entry) || each.Held is not null || each.RemovedAs is not null || each.GoneAs is not (null or LandedGone.Moved)) return each;
                wrote = true;
                return each with { Held = held, GoneAt = each.GoneAt ?? now, GoneAs = LandedGone.Missing, MovedTo = null, KeptOn = null, MovedToAt = null };
            })];
        });
        return wrote;
    }

    /// <summary>
    /// Where bringing it up to date replayed it: its new tip, and the line's commit it now grows from.
    /// Daoris moved it, so it stays the landing's; a branch someone else rebased no longer holds its tip and is not.
    /// </summary>
    public void Moved(string repository, string branch, string tip, string from) => Edit(all =>
        [.. all.Select(each => Standing(each, repository, branch) ? each with { Tip = tip, From = from } : each)]);

    /// <summary>
    /// Branches found gone, or no longer the landing's (a name that is the person's now): kept as traces, never judged
    /// again, so the review of the session that landed one still says
    /// where its work went.
    /// </summary>
    public void Gone(string repository, IReadOnlyCollection<string> branches) => Edit(all =>
        [.. all.Select(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase)
                               && each.GoneAt is null && branches.Contains(each.Branch, StringComparer.Ordinal)
            ? each with { GoneAt = DateTimeOffset.UtcNow }
            : each)]);

    /// <summary>
    /// The clean-up's word on one landing it judged gone, or no longer the landing's (<see cref="LandedGone"/>): that standing
    /// entry becomes a trace with why. Applied to that landing alone, by its identity and the tip judged (<see cref="SameLanding"/>), so a
    /// new landing of the same name recorded since keeps its standing.
    /// </summary>
    internal void Gone(LandedBranch entry, string why, string? movedTo = null, string? keptOn = null, string? movedToAt = null) => Edit(all =>
        [.. all.Select(each => each.GoneAt is null && SameLanding(each, entry)
            ? each with { GoneAt = DateTimeOffset.UtcNow, GoneAs = why, MovedTo = movedTo, KeptOn = keptOn, MovedToAt = movedToAt }
            : each)]);

    /// <summary>
    /// The look's word on why a branch Daoris did not remove went (<see cref="LandedGone"/>), on that landing alone, by its
    /// identity and the tip read (<see cref="SameLanding"/>; after review: an observation by name retired a new landing of the same name).
    /// A trace that says no why, or says it moved, gains it; a standing entry becomes a trace with it where git said its branch is missing
    /// (<see cref="LandedGone.Missing"/> or <see cref="LandedGone.Moved"/>). One Daoris removed, a trace that says another why, and a standing
    /// entry where the name stands, are left as they are.
    /// </summary>
    public void GoneAs(LandedBranch entry, string why, string? movedTo = null, string? keptOn = null, string? movedToAt = null) => Edit(all =>
    {
        var now = DateTimeOffset.UtcNow;
        return [.. all.Select(each => SameLanding(each, entry) && each.RemovedAs is null && (each.GoneAs is null or LandedGone.Moved)
                                      && (each.GoneAt is not null || why is LandedGone.Missing or LandedGone.Moved)
            ? each with { GoneAt = each.GoneAt ?? now, GoneAs = why, MovedTo = movedTo, KeptOn = keptOn, MovedToAt = movedToAt }
            : each)];
    });

    /// <summary>
    /// The withdrawal's write (<see cref="LandedWithdrawals"/>, its only caller): that standing landing becomes a trace saying it is
    /// withdrawn, why, and the recovery ref its commits stay on. Applied to that landing alone (<see cref="SameLanding"/>), so a new landing of
    /// the same name keeps its standing, and never over a trace, which is never judged again.
    /// </summary>
    /// <returns>Whether it is written.</returns>
    internal bool Withdrawn(LandedBranch entry, string why, string keptOn)
    {
        var wrote = false;
        Edit(all =>
        {
            var now = DateTimeOffset.UtcNow;
            wrote = false;
            return [.. all.Select(each =>
            {
                if (each.GoneAt is not null || !SameLanding(each, entry)) return each;
                wrote = true;
                return each with { GoneAt = now, GoneAs = LandedGone.Withdrawn, GoneWhy = why, KeptOn = keptOn, MovedTo = null, MovedToAt = null };
            })];
        });
        return wrote;
    }

    /// <summary>The same landing as <paramref name="entry"/>: its identity (<see cref="SameEntry"/>) and the tip observed.</summary>
    private static bool SameLanding(LandedBranch each, LandedBranch entry) =>
        SameEntry(each, entry) && string.Equals(each.Tip, entry.Tip, StringComparison.OrdinalIgnoreCase);

    /// <summary>A branch the clean-up removed once its work read on the line: a trace, with what the proof found and where.</summary>
    public void Removed(string repository, string branch, string kind, string? where) => Edit(all =>
        [.. all.Select(each => Standing(each, repository, branch)
            ? each with { GoneAt = DateTimeOffset.UtcNow, RemovedAs = kind, RemovedOn = where }
            : each)]);

    /// <summary>
    /// A plugin's answer about the entry's pull request, standing or a trace: it replaces the kept
    /// answer, and a failure older than it is no longer the latest word.
    /// </summary>
    /// <remarks>
    /// Occasions ask at once, and the slower request can return last, so an answer is kept only where it is asked no
    /// earlier than the kept one, judged under the record's lock; and a kept <c>completed</c>, which is final, is never
    /// replaced. A failure asked after the answer stays the latest word beside it.
    /// </remarks>
    public void Answered(LandedBranch entry, PullRequestState answer) => Edit(all =>
        [.. all.Select(each => SameEntry(each, entry) && Newer(each, answer)
            ? each with { PullRequestState = answer, PullRequestAskFailed = each.PullRequestAskFailed is { } failed && failed.At > answer.AskedAt ? failed : null }
            : each)]);

    /// <summary>An ask that failed: kept beside the answer, which it never overwrites. Absent is never zero.</summary>
    /// <remarks>A failure asked before the kept answer, or before the kept failure, is not the latest word, and is not kept.</remarks>
    public void AskFailed(LandedBranch entry, PullRequestAskFailed failed) => Edit(all =>
        [.. all.Select(each => SameEntry(each, entry)
                               && !(each.PullRequestState is { } kept && kept.AskedAt > failed.At)
                               && !(each.PullRequestAskFailed is { } was && was.At > failed.At)
            ? each with { PullRequestAskFailed = failed }
            : each)]);

    /// <summary>Whether an answer is kept over the entry's: none kept, or one not completed and asked no later than this one.</summary>
    private static bool Newer(LandedBranch each, PullRequestState answer) =>
        each.PullRequestState is not { } kept || (kept.State != PullRequestStates.Completed && answer.AskedAt >= kept.AskedAt);

    /// <summary>A session branch removed on the entry's pull request's answer: kept with the rest.</summary>
    public void Carried(LandedBranch entry, CarriedBranch carried) => Edit(all =>
        [.. all.Select(each => SameEntry(each, entry) ? each with { Carried = [.. each.Carried, carried] } : each)]);

    /// <summary>
    /// The traces <paramref name="forget"/> names, dropped: a trace is kept so a
    /// session's review can say where its work went, and a cleared session has no review. Never a standing entry, which a clear
    /// refuses instead. How many went comes back; nothing is written when none did.
    /// </summary>
    public int ForgetTraces(Func<LandedBranch, bool> forget)
    {
        lock (Gate)
        {
            using var held = RecordLock.Take(LockPath);
            var all = Everything();
            bool Forgotten(LandedBranch entry) => entry.GoneAt is not null && forget(entry);
            var count = all.Count(Forgotten);
            if (count == 0) return 0;
            // The home and the folder the record is in.
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
            AtomicFile.WriteText(FilePath, ToJson([.. all.Where(entry => !Forgotten(entry))]));
            return count;
        }
    }

    /// <summary>
    /// The same landing, standing or a trace: its repository, its branch, the session that made it, and when. A later landing of
    /// the same name is another entry, and so is a trace of an earlier one.
    /// </summary>
    private static bool SameEntry(LandedBranch each, LandedBranch entry) =>
        Same(each, entry.Repository, entry.Branch)
        && string.Equals(each.Session, entry.Session, StringComparison.OrdinalIgnoreCase)
        && each.LandedAt == entry.LandedAt;

    // Every entry, traces included: a write that read only the standing ones would drop every trace.
    private void Edit(Func<IReadOnlyList<LandedBranch>, IReadOnlyList<LandedBranch>> change) => List.Edit(FilePath, change, LockPath);

    /// <summary>
    /// The entries with each repository's traces beyond its newest <see cref="TracesKept"/> dropped, the one that
    /// went first first, whatever its place in the file; the rest keep the order they landed in.
    /// </summary>
    private static IReadOnlyList<LandedBranch> Bounded(IReadOnlyList<LandedBranch> entries)
    {
        var dropped = entries
            .Select((entry, at) => (Entry: entry, At: at))
            .Where(each => each.Entry.GoneAt is not null)
            .GroupBy(each => each.Entry.Repository, StringComparer.OrdinalIgnoreCase)
            .SelectMany(traces => traces.OrderByDescending(each => each.Entry.GoneAt).ThenByDescending(each => each.At).Skip(TracesKept))
            .Select(each => each.At)
            .ToHashSet();
        return dropped.Count == 0 ? entries : [.. entries.Where((_, at) => !dropped.Contains(at))];
    }

    private static bool Same(LandedBranch entry, string repository, string branch) =>
        SessionBranches.SameBranch(entry.Repository, entry.Branch, repository, branch);

    private static bool Standing(LandedBranch entry, string repository, string branch) => entry.GoneAt is null && Same(entry, repository, branch);

}
