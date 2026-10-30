using System.Globalization;
using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// The branches this machine's landings made (WSR5): <c>&lt;home&gt;/landings.json</c>. Written at the
/// landing, read by the clean-up and the hand-off, and kept as a trace once the branch is gone (REVIEW2, D113), each
/// repository's newest <see cref="TracesKept"/> traces (LEFT3).
/// </summary>
/// <remarks>
/// <para><b>Machine-local, under the home (D63), never in the repository.</b> Which branches a landing on
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
    /// How many traces of gone branches each repository keeps (LEFT3): the newest, by when each went. A standing entry
    /// is never dropped. Every reader parses the whole file, and a trace is kept only so a session's review can say where
    /// its work went, which is asked of recent sessions.
    /// </summary>
    public const int TracesKept = 50;

    // One writer at a time in this process: two presses in one shell would otherwise read the same file and
    // the second write would drop the first's branch.
    private static readonly object Gate = new();

    private static readonly RecordList<LandedBranch> List = new(Gate, "branches", Read, ToJson, Bounded);

    public string FilePath => DaorisHome.Entry(home, HomeEntry.Landings);

    /// <summary>The record's lock across processes (FABRIC-END2): the loop and a verb beside it edit this file one at a time.</summary>
    public string LockPath => RecordLock.PathOf(home, FileName);

    /// <summary>Every branch recorded that still stands, in the order they landed: what the clean-up, the hand-off and Ask Daoris act on.</summary>
    public IReadOnlyList<LandedBranch> All() => [.. Everything().Where(entry => entry.GoneAt is null)];

    /// <summary>
    /// The newest landing of one session, standing or a trace (REVIEW2, D113): what its review says of where its work
    /// went, since a tidy took its tree and the clean-up may have taken the branch since. A session whose done moved a
    /// chain's branch on finds that branch's landing (LAND2c).
    /// </summary>
    public LandedBranch? Landing(string session) => Everything().LastOrDefault(entry => entry.Names(session));

    /// <summary>
    /// Every branch <paramref name="plugin"/> answered that it pushed, standing or a trace, in the order they landed:
    /// what a plugin's Activity lists as its pushes (PLUGUI1d, D119 §3.2).
    /// </summary>
    public IReadOnlyList<LandedBranch> PushedBy(string plugin) =>
        [.. Everything().Where(entry => entry.Pushed && string.Equals(entry.Plugin, plugin, StringComparison.OrdinalIgnoreCase))];

    /// <summary>
    /// Every entry, standing and traces, in the order they landed: what a work reads for the landings that name its sessions
    /// (PAUSE1a, <see cref="AskWork"/>), since a session a landing names keeps its tree whatever git says (D132 point 7).
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
    /// where they say which, the newest first (PLUGHOOK1c): what <c>trees state</c> asks about, since a trace's pull request may
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

    /// <summary>
    /// What a plugin answered when it pushed the branch (D100), kept on its entry. An answer naming no pull request keeps the
    /// one the entry holds: a push after an advance grows the pull request already open (LAND2c).
    /// </summary>
    public void Pushed(string repository, string branch, PluginLanding said, string tip) => Edit(all =>
        [.. all.Select(each => Standing(each, repository, branch)
            ? each with { Plugin = said.Plugin, Pushed = said.Pushed, PullRequest = said.PullRequest ?? each.PullRequest, PushedTip = tip }
            : each)]);

    /// <summary>
    /// A later done moved the standing branch on as a fast-forward (LAND2c, D149 point 2), or by one more squashed commit
    /// (PROCESS1b-ii): its tip is the advance's, the session tip squashed moves with it where the advance squashed one, and the
    /// advance is kept with the rest. The branch stays the landing's, so the clean-up and the hand-off judge it at its new tip.
    /// </summary>
    /// <param name="unit">The advancing session's branch work unit (FABRIC-END2), added to <see cref="LandedBranch.Units"/> where it is new.</param>
    public void Advanced(string repository, string branch, LandedAdvance advance, string? unit = null) => Edit(all =>
        [.. all.Select(each => Standing(each, repository, branch)
            ? each with
            {
                Tip = advance.To, Squashed = advance.Squashed ?? each.Squashed, Advances = [.. each.Advances, advance],
                Units = unit is null || each.Units.Contains(unit, StringComparer.Ordinal) ? each.Units : [.. each.Units, unit],
            }
            : each)]);

    /// <summary>
    /// The end judgement's write (FABRIC-END2, <see cref="EndKeeping"/>, its only caller): the standing entry's held fact, or none where it
    /// stopped holding. A trace is never written again.
    /// </summary>
    internal void Hold(LandedBranch entry, HeldFact? held) => Edit(all =>
        [.. all.Select(each => each.GoneAt is null && SameEntry(each, entry) ? each with { Held = held } : each)]);

    /// <summary>
    /// CLEANUP1b: the end judgement's write for a landing whose branch git said is missing (<see cref="EndKeeping.KeepGoneAsync"/>, its only
    /// caller): the fact the line's ancestry proved at the tip its landing recorded, kept as the entry retires with git's word
    /// (<see cref="LandedGone.Missing"/>), on that landing alone (<see cref="SameLanding"/>). A standing entry becomes a trace; a trace that
    /// says no why, or says it moved, gains it, and no longer names where it moved. One with a fact already, one Daoris removed, and one that
    /// says another why are left as they are: a trace with its fact is never judged again (FABRIC-END2).
    /// </summary>
    /// <returns>Whether it was written.</returns>
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
    /// Where bringing it up to date replayed it (WSR6): its new tip, and the line's commit it now grows from.
    /// Daoris moved it, so it stays the landing's; a branch someone else rebased no longer holds its tip and is not.
    /// </summary>
    public void Moved(string repository, string branch, string tip, string from) => Edit(all =>
        [.. all.Select(each => Standing(each, repository, branch) ? each with { Tip = tip, From = from } : each)]);

    /// <summary>
    /// Branches found gone, or no longer the landing's (a name that is the person's now): kept as traces, never judged
    /// again (REVIEW2, D113). Until D113 they were forgotten, and the review of the session that landed one then had
    /// nothing to say of where its work went.
    /// </summary>
    public void Gone(string repository, IReadOnlyCollection<string> branches) => Edit(all =>
        [.. all.Select(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase)
                               && each.GoneAt is null && branches.Contains(each.Branch, StringComparer.Ordinal)
            ? each with { GoneAt = DateTimeOffset.UtcNow }
            : each)]);

    /// <summary>
    /// The clean-up's word on one landing it judged gone, or no longer the landing's (CLEANUP1, <see cref="LandedGone"/>): that standing
    /// entry becomes a trace with why. Applied to that landing alone, by its identity and the tip judged (<see cref="SameLanding"/>), so a
    /// new landing of the same name recorded since keeps its standing.
    /// </summary>
    internal void Gone(LandedBranch entry, string why, string? movedTo = null, string? keptOn = null, string? movedToAt = null) => Edit(all =>
        [.. all.Select(each => each.GoneAt is null && SameLanding(each, entry)
            ? each with { GoneAt = DateTimeOffset.UtcNow, GoneAs = why, MovedTo = movedTo, KeptOn = keptOn, MovedToAt = movedToAt }
            : each)]);

    /// <summary>
    /// The look's word on why a branch Daoris did not remove went (CLEANUP1, <see cref="LandedGone"/>), on that landing alone, by its
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
    /// The withdrawal's write (LANDRETIRE1, <see cref="LandedWithdrawals"/>, its only caller): that standing landing becomes a trace saying it was
    /// withdrawn, why, and the recovery ref its commits stay on. Applied to that landing alone (<see cref="SameLanding"/>), so a new landing of
    /// the same name keeps its standing, and never over a trace, which is never judged again (D113).
    /// </summary>
    /// <returns>Whether it was written.</returns>
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

    /// <summary>The same landing as <paramref name="entry"/>: its identity (<see cref="SameEntry"/>) and the tip it was observed at.</summary>
    private static bool SameLanding(LandedBranch each, LandedBranch entry) =>
        SameEntry(each, entry) && string.Equals(each.Tip, entry.Tip, StringComparison.OrdinalIgnoreCase);

    /// <summary>A branch the clean-up removed once its work read on the line (WSR5): a trace, with what the proof found and where.</summary>
    public void Removed(string repository, string branch, string kind, string? where) => Edit(all =>
        [.. all.Select(each => Standing(each, repository, branch)
            ? each with { GoneAt = DateTimeOffset.UtcNow, RemovedAs = kind, RemovedOn = where }
            : each)]);

    /// <summary>
    /// A plugin's answer about the entry's pull request (PLUGHOOK1a, D148 point 6), standing or a trace: it replaces the kept
    /// answer, and a failure older than it is no longer the latest word.
    /// </summary>
    /// <remarks>
    /// FABRIC-END6c: occasions ask at once, and the slower request can return last, so an answer is kept only where it was asked no
    /// earlier than the kept one, judged under the record's lock; and a kept <c>completed</c>, which is final (D148 point 6), is never
    /// replaced. A failure asked after the answer stays the latest word beside it.
    /// </remarks>
    public void Answered(LandedBranch entry, PullRequestState answer) => Edit(all =>
        [.. all.Select(each => SameEntry(each, entry) && Newer(each, answer)
            ? each with { PullRequestState = answer, PullRequestAskFailed = each.PullRequestAskFailed is { } failed && failed.At > answer.AskedAt ? failed : null }
            : each)]);

    /// <summary>An ask that failed (design §2.4): kept beside the answer, which it never overwrites. Absent is never zero.</summary>
    /// <remarks>FABRIC-END6c: a failure asked before the kept answer, or before the kept failure, is not the latest word, and is not kept.</remarks>
    public void AskFailed(LandedBranch entry, PullRequestAskFailed failed) => Edit(all =>
        [.. all.Select(each => SameEntry(each, entry)
                               && !(each.PullRequestState is { } kept && kept.AskedAt > failed.At)
                               && !(each.PullRequestAskFailed is { } was && was.At > failed.At)
            ? each with { PullRequestAskFailed = failed }
            : each)]);

    /// <summary>Whether an answer is kept over the entry's: none kept, or one not completed and asked no later than this one.</summary>
    private static bool Newer(LandedBranch each, PullRequestState answer) =>
        each.PullRequestState is not { } kept || (kept.State != PullRequestStates.Completed && answer.AskedAt >= kept.AskedAt);

    /// <summary>A session branch removed on the entry's pull request's answer (design §2.5): kept with the rest.</summary>
    public void Carried(LandedBranch entry, CarriedBranch carried) => Edit(all =>
        [.. all.Select(each => SameEntry(each, entry) ? each with { Carried = [.. each.Carried, carried] } : each)]);

    /// <summary>
    /// The traces <paramref name="forget"/> names, dropped (HIST1c, the history-clearing design §2.2): a trace is kept so a
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
            // The home and the folder the record is in (`data/` since HOMELAYOUT3).
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

    // Every entry, traces included: a write that read only the standing ones would drop every trace (D113).
    private void Edit(Func<IReadOnlyList<LandedBranch>, IReadOnlyList<LandedBranch>> change) => List.Edit(FilePath, change, LockPath);

    /// <summary>
    /// The entries with each repository's traces beyond its newest <see cref="TracesKept"/> dropped (LEFT3), the one that
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

    /// <summary>Written by hand, as the driver's other files are, for the AOT reason <see cref="DriverConfig"/> gives.</summary>
    private static string ToJson(IReadOnlyList<LandedBranch> entries) => JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        writer.WriteStartArray("branches");
        foreach (var entry in entries)
        {
            writer.WriteStartObject();
            writer.WriteString("repository", entry.Repository);
            writer.WriteString("workspace", entry.Workspace);
            writer.WriteString("branch", entry.Branch);
            if (entry.Line is not null) writer.WriteString("line", entry.Line);
            writer.WriteString("tip", entry.Tip);
            // Written only where the landing squashed (PROCESS1b-ii): absent is the session's own commits on the branch.
            if (entry.Squashed is not null) writer.WriteString("squashed", entry.Squashed);
            writer.WriteString("session", entry.Session);
            if (entry.Quest is not null) writer.WriteString("quest", entry.Quest);
            if (entry.Title is not null) writer.WriteString("title", entry.Title);
            writer.WriteString("landedAt", Moments.Write(entry.LandedAt));
            if (entry.Plugin is not null) writer.WriteString("plugin", entry.Plugin);
            if (entry.Pushed) writer.WriteBoolean("pushed", true);
            if (entry.PullRequest is not null) writer.WriteString("pullRequest", entry.PullRequest);
            if (entry.PushedTip is not null) writer.WriteString("pushedTip", entry.PushedTip);
            if (entry.From is not null) writer.WriteString("from", entry.From);
            if (entry.GoneAt is { } gone) writer.WriteString("goneAt", Moments.Write(gone));
            if (entry.RemovedAs is not null) writer.WriteString("removedAs", entry.RemovedAs);
            if (entry.RemovedOn is not null) writer.WriteString("removedOn", entry.RemovedOn);
            if (entry.GoneAs is not null) writer.WriteString("goneAs", entry.GoneAs);
            if (entry.MovedTo is not null) writer.WriteString("movedTo", entry.MovedTo);
            if (entry.MovedToAt is not null) writer.WriteString("movedToAt", entry.MovedToAt);
            if (entry.KeptOn is not null) writer.WriteString("keptOn", entry.KeptOn);
            // LANDRETIRE1: only where a withdrawal said why, so a trace from before reads as it did.
            if (entry.GoneWhy is not null) writer.WriteString("goneWhy", entry.GoneWhy);
            if (entry.AcceptedBy is not null) writer.WriteString("acceptedBy", entry.AcceptedBy);
            if (entry.Rule is { } rule)
            {
                writer.WriteStartObject("rule");
                if (rule.Plugin is not null) writer.WriteString("plugin", rule.Plugin);
                writer.WriteBoolean("autoAccept", rule.AutoAccept);
                writer.WriteString("source", rule.Source);
                // AUTONOMY1c2: added beside the rest, only where the access setup turned the switch on; a record from before reads none.
                if (rule.Access is not null) writer.WriteString("access", rule.Access);
                writer.WriteEndObject();
            }

            if (entry.Integrate is { } integrate) WriteIntegrate(writer, integrate);
            if (entry.Review is { } review) WriteReview(writer, review);
            if (entry.Opinion is { } opinion) WriteOpinion(writer, opinion);
            if (entry.Workflow is { } workflow) WriteWorkflow(writer, workflow);

            if (entry.Advances.Count > 0)
            {
                writer.WriteStartArray("advances");
                foreach (var advance in entry.Advances)
                {
                    writer.WriteStartObject();
                    writer.WriteString("from", advance.From);
                    writer.WriteString("to", advance.To);
                    writer.WriteString("at", Moments.Write(advance.At));
                    writer.WriteString("session", advance.Session);
                    if (advance.AcceptedBy is not null) writer.WriteString("acceptedBy", advance.AcceptedBy);
                    if (advance.Integrate is { } stood) WriteIntegrate(writer, stood);
                    if (advance.Review is { } advanced) WriteReview(writer, advanced);
                    if (advance.Opinion is { } read) WriteOpinion(writer, read);
                    if (advance.Workflow is { } went) WriteWorkflow(writer, went);
                    if (advance.Squashed is not null) writer.WriteString("squashed", advance.Squashed);
                    // FABRIC-UNITLINK1, FABRIC-CLOSE1h13: each only where it is known, so an advance from before reads as it did.
                    if (advance.Quest is not null) writer.WriteString("quest", advance.Quest);
                    if (advance.Unit is not null) writer.WriteString("unit", advance.Unit);
                    if (advance.Submission is { } made) WriteSubmission(writer, made);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            if (entry.PullRequestState is { } state) WriteState(writer, state);
            if (entry.PullRequestAskFailed is { } failed)
            {
                writer.WriteStartObject("pullRequestAskFailed");
                writer.WriteString("code", failed.Code);
                writer.WriteString("plugin", failed.Plugin);
                writer.WriteString("at", Moments.Write(failed.At));
                writer.WriteEndObject();
            }

            if (entry.Carried.Count > 0)
            {
                writer.WriteStartArray("carried");
                foreach (var carried in entry.Carried)
                {
                    writer.WriteStartObject();
                    writer.WriteString("branch", carried.Branch);
                    writer.WriteString("tip", carried.Tip);
                    writer.WriteString("at", Moments.Write(carried.At));
                    writer.WriteString("by", carried.By);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            // FABRIC-END2: each only where it is known, so an entry from before reads as it did.
            if (entry.Held is { } held) HeldFact.Write(writer, held);
            if (entry.Units.Count > 0)
            {
                writer.WriteStartArray("units");
                foreach (var unit in entry.Units) writer.WriteStringValue(unit);
                writer.WriteEndArray();
            }

            // FABRIC-CLOSE1h11, FABRIC-CLOSE1h13: each only where it is known, so a landing from before reads as it did.
            if (entry.Served is not null) writer.WriteString("served", entry.Served);
            if (entry.Submission is { } submission) WriteSubmission(writer, submission);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    /// <summary>The submission a landing or an advance was made on (FABRIC-CLOSE1h13): its machine and sequence.</summary>
    private static void WriteSubmission(Utf8JsonWriter writer, QuestSubmissionRef submission)
    {
        writer.WriteStartObject("submission");
        writer.WriteString("machine", submission.Machine);
        writer.WriteNumber("sequence", submission.Sequence);
        writer.WriteEndObject();
    }

    /// <summary>
    /// The submission a landing or an advance kept, or null: absent, which is every record from before FABRIC-CLOSE1h13 and every done's,
    /// or named only in half.
    /// </summary>
    private static QuestSubmissionRef? SubmissionOf(JsonElement element) =>
        element.TryGetProperty("submission", out var submission) && submission.ValueKind == JsonValueKind.Object
        && Text(submission, "machine") is { Length: > 0 } machine
        && submission.TryGetProperty("sequence", out var sequence) && sequence.ValueKind == JsonValueKind.Number
        && sequence.TryGetInt64(out var number) && number > 0
            ? new QuestSubmissionRef(machine, number)
            : null;

    /// <summary>
    /// The acceptance's provenance (INTEGRATE1e, design §17.4): written only where <c>integrate</c> stood, so a landing or an advance without
    /// it, and every one from before, writes nothing and reads as it did.
    /// </summary>
    private static void WriteIntegrate(Utf8JsonWriter writer, LandedIntegrate integrate)
    {
        writer.WriteStartObject("integrate");
        writer.WriteString("request", integrate.Request);
        writer.WriteString("source", integrate.Source);
        writer.WriteEndObject();
    }

    /// <summary>
    /// The provenance a landing or an advance kept, or null: a record that says neither, which is one from before INTEGRATE1e, one an older
    /// build rewrote, one made where no <c>integrate</c> stood, and one whose shape does not read whole. Its values are kept as written.
    /// </summary>
    private static LandedIntegrate? IntegrateOf(JsonElement element) =>
        element.TryGetProperty("integrate", out var integrate) && integrate.ValueKind == JsonValueKind.Object
        && Text(integrate, "request") is { Length: > 0 } request && Text(integrate, "source") is { Length: > 0 } source
            ? new LandedIntegrate(request, source)
            : null;

    /// <summary>
    /// The review that let a landing go (REVIEWENV1c), each field only where it is known. Internal with its reader and the opinion's and
    /// workflow's, since an integration request keeps its gate proof in the record's own shape (INTEGRATE1e part e2).
    /// </summary>
    internal static void WriteReview(Utf8JsonWriter writer, LandingReview review)
    {
        writer.WriteStartObject("review");
        writer.WriteString("said", review.Said);
        if (review.Environment is not null) writer.WriteString("environment", review.Environment);
        if (review.Quest is not null) writer.WriteString("quest", review.Quest);
        if (review.Commit is not null) writer.WriteString("commit", review.Commit);
        if (review.At is { } at) writer.WriteString("at", Moments.Write(at));
        if (review.Words is not null) writer.WriteString("words", review.Words);
        writer.WriteEndObject();
    }

    /// <summary>The review a landing or an advance kept, or null: absent, which is every landing from before REVIEWENV1c, or not whole.</summary>
    internal static LandingReview? ReviewOf(JsonElement element) =>
        element.TryGetProperty("review", out var review) && review.ValueKind == JsonValueKind.Object && Text(review, "said") is { } said
            ? new LandingReview(said, Text(review, "environment"), Text(review, "quest"))
            {
                Commit = Text(review, "commit"),
                At = Moments.Read(Text(review, "at")),
                Words = Text(review, "words"),
            }
            : null;

    /// <summary>The second opinion that let a landing go (XAGENT1f, design §8.6), each field only where it is known.</summary>
    internal static void WriteOpinion(Utf8JsonWriter writer, LandingOpinion opinion)
    {
        writer.WriteStartObject("opinion");
        writer.WriteString("said", opinion.Said);
        if (opinion.Opinion is not null) writer.WriteString("opinion", opinion.Opinion);
        if (opinion.Reviewer is not null) writer.WriteString("reviewer", opinion.Reviewer);
        if (opinion.Label is not null) writer.WriteString("label", opinion.Label);
        if (opinion.Base is not null) writer.WriteString("base", opinion.Base);
        if (opinion.Tip is not null) writer.WriteString("tip", opinion.Tip);
        writer.WriteNumber("passes", opinion.Passes);
        Counts(writer, "weights", opinion.Weights);
        Counts(writer, "answers", opinion.Answers);
        writer.WriteNumber("disputes", opinion.Disputes);
        writer.WriteNumber("unread", opinion.Unread);
        if (opinion.Person is not null) writer.WriteString("person", opinion.Person);
        if (opinion.At is { } at) writer.WriteString("at", Moments.Write(at));
        if (opinion.Words is not null) writer.WriteString("words", opinion.Words);
        if (opinion.Code is not null) writer.WriteString("code", opinion.Code);
        writer.WriteEndObject();

        static void Counts(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, int> counts)
        {
            writer.WriteStartObject(name);
            foreach (var (key, count) in counts.OrderBy(pair => pair.Key, StringComparer.Ordinal)) writer.WriteNumber(key, count);
            writer.WriteEndObject();
        }
    }

    /// <summary>The second opinion a landing or an advance kept, or null: absent, which is every landing from before XAGENT1f.</summary>
    internal static LandingOpinion? OpinionOf(JsonElement element)
    {
        if (!element.TryGetProperty("opinion", out var opinion) || opinion.ValueKind != JsonValueKind.Object || Text(opinion, "said") is not { } said)
        {
            return null;
        }

        static int Number(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n) ? n : 0;
        static IReadOnlyDictionary<string, int> Counts(JsonElement element, string name)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            if (!element.TryGetProperty(name, out var kept) || kept.ValueKind != JsonValueKind.Object) return counts;
            foreach (var each in kept.EnumerateObject())
            {
                if (each.Value.ValueKind == JsonValueKind.Number && each.Value.TryGetInt32(out var count)) counts[each.Name] = count;
            }

            return counts;
        }

        return new LandingOpinion(said)
        {
            Opinion = Text(opinion, "opinion"),
            Reviewer = Text(opinion, "reviewer"),
            Label = Text(opinion, "label"),
            Base = Text(opinion, "base"),
            Tip = Text(opinion, "tip"),
            Passes = Number(opinion, "passes"),
            Weights = Counts(opinion, "weights"),
            Answers = Counts(opinion, "answers"),
            Disputes = Number(opinion, "disputes"),
            Unread = Number(opinion, "unread"),
            Person = Text(opinion, "person"),
            At = Moments.Read(Text(opinion, "at")),
            Words = Text(opinion, "words"),
            Code = Text(opinion, "code"),
        };
    }

    /// <summary>The named workflow a landing went by (WORKFLOW1f, design §5.5), each field only where it is known.</summary>
    internal static void WriteWorkflow(Utf8JsonWriter writer, LandingWorkflow workflow)
    {
        writer.WriteStartObject("workflow");
        writer.WriteString("workflow", workflow.Workflow);
        writer.WriteNumber("version", workflow.Version);
        writer.WriteString("level", workflow.Level);
        if (workflow.Kind is not null) writer.WriteString("kind", workflow.Kind);
        writer.WriteEndObject();
    }

    /// <summary>The named workflow a landing or an advance kept, or null: absent, which is every landing under Current and from before WORKFLOW1f.</summary>
    internal static LandingWorkflow? WorkflowOf(JsonElement element) =>
        element.TryGetProperty("workflow", out var workflow) && workflow.ValueKind == JsonValueKind.Object
        && Text(workflow, "workflow") is { } id && Text(workflow, "level") is { } level
        && workflow.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out var number)
            ? new LandingWorkflow(id, number, level) { Kind = Text(workflow, "kind") }
            : null;

    /// <summary>The kept answer, in the answer's own field names, with who answered and when it was asked (PLUGHOOK1a).</summary>
    private static void WriteState(Utf8JsonWriter writer, PullRequestState state)
    {
        writer.WriteStartObject("pullRequestState");
        writer.WriteString("state", state.State);
        if (state.PullRequest is not null) writer.WriteString("pullRequest", state.PullRequest);
        if (state.MergeCommit is not null) writer.WriteString("mergeCommit", state.MergeCommit);
        if (state.SourceCommit is not null) writer.WriteString("sourceCommit", state.SourceCommit);
        if (state.Target is not null) writer.WriteString("target", state.Target);
        if (state.How is not null) writer.WriteString("how", state.How);
        if (state.At is { } at) writer.WriteString("at", Moments.Write(at));
        if (state.Message is not null) writer.WriteString("message", state.Message);
        if (state.Plugin is not null) writer.WriteString("plugin", state.Plugin);
        writer.WriteString("askedAt", Moments.Write(state.AskedAt));
        writer.WriteEndObject();
    }

    /// <summary>
    /// The kept answer, read by the wire's own rules (<see cref="PullRequestAnswers.Read"/>), or null: one that would not have
    /// been an answer is none, since a removal chooses by it.
    /// </summary>
    private static PullRequestState? StateOf(JsonElement element)
    {
        if (!element.TryGetProperty("pullRequestState", out var kept) || kept.ValueKind != JsonValueKind.Object) return null;
        var plugin = Text(kept, "plugin");
        return PullRequestAnswers.Read(kept, plugin ?? "") is { } state
            ? state with
            {
                Plugin = plugin,
                AskedAt = Moments.Read(Text(kept, "askedAt")) ?? DateTimeOffset.MinValue,
            }
            : null;
    }

    /// <summary>The latest failed ask, whole or none.</summary>
    private static PullRequestAskFailed? AskFailedOf(JsonElement element) =>
        element.TryGetProperty("pullRequestAskFailed", out var failed) && failed.ValueKind == JsonValueKind.Object
        && Text(failed, "code") is { } code && Text(failed, "plugin") is { } plugin
        && Moments.Read(Text(failed, "at")) is { } at
            ? new PullRequestAskFailed(code, plugin, at)
            : null;

    /// <summary>The session branches removed on its answer, each whole or left out. Absent is none.</summary>
    private static IReadOnlyList<CarriedBranch> CarriedOf(JsonElement element)
    {
        if (!element.TryGetProperty("carried", out var list) || list.ValueKind != JsonValueKind.Array) return [];
        var carried = new List<CarriedBranch>();
        foreach (var each in list.EnumerateArray())
        {
            if (each.ValueKind != JsonValueKind.Object || Text(each, "branch") is not { } branch || Text(each, "tip") is not { } tip
                || Text(each, "by") is not { } by
                || Moments.Read(Text(each, "at")) is not { } at)
            {
                continue;
            }

            carried.Add(new CarriedBranch(branch, tip, at, by));
        }

        // The empty case is the property's own default, so an entry from before PLUGHOOK1a still equals itself read twice.
        if (carried.Count == 0) return [];
        return carried;
    }

    /// <summary>One entry, or null where it lacks what makes it one: a repository, a branch, the commit it was made at, a session.</summary>
    private static LandedBranch? Read(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        var repository = Text(element, "repository");
        var branch = Text(element, "branch");
        var tip = Text(element, "tip");
        var session = Text(element, "session");
        if (repository is null || branch is null || tip is null || session is null) return null;
        var at = Moments.Read(Text(element, "landedAt")) ?? DateTimeOffset.MinValue;
        return new LandedBranch(repository, Text(element, "workspace") ?? "default", branch, Text(element, "line"), tip, session,
            Text(element, "quest"), Text(element, "title"), at)
        {
            Plugin = Text(element, "plugin"),
            Pushed = element.TryGetProperty("pushed", out var pushed) && pushed.ValueKind == JsonValueKind.True,
            PullRequest = Text(element, "pullRequest"),
            PushedTip = Text(element, "pushedTip"),
            From = Text(element, "from"),
            GoneAt = Moments.Read(Text(element, "goneAt")),
            RemovedAs = Text(element, "removedAs"),
            RemovedOn = Text(element, "removedOn"),
            GoneAs = Text(element, "goneAs"),
            MovedTo = Text(element, "movedTo"),
            MovedToAt = Text(element, "movedToAt"),
            KeptOn = Text(element, "keptOn"),
            GoneWhy = Text(element, "goneWhy"),
            AcceptedBy = Text(element, "acceptedBy"),
            Rule = element.TryGetProperty("rule", out var rule) && rule.ValueKind == JsonValueKind.Object
                ? new LandedRule(
                    Text(rule, "plugin"),
                    rule.TryGetProperty("autoAccept", out var auto) && auto.ValueKind == JsonValueKind.True,
                    Text(rule, "source") ?? LandingSource.Default)
                { Access = Text(rule, "access") }
                : null,
            Integrate = IntegrateOf(element),
            Review = ReviewOf(element),
            Opinion = OpinionOf(element),
            Workflow = WorkflowOf(element),
            Advances = AdvancesOf(element),
            PullRequestState = StateOf(element),
            PullRequestAskFailed = AskFailedOf(element),
            Carried = CarriedOf(element),
            Squashed = Text(element, "squashed"),
            Held = HeldFact.Read(element),
            Units = UnitsOf(element),
            Served = Text(element, "served"),
            Submission = SubmissionOf(element),
        };
    }

    /// <summary>The units an entry carries (FABRIC-END2), each a non-empty string. Absent is none, which is every landing from before.</summary>
    private static IReadOnlyList<string> UnitsOf(JsonElement element)
    {
        if (!element.TryGetProperty("units", out var list) || list.ValueKind != JsonValueKind.Array) return [];
        var units = list.EnumerateArray()
            .Where(each => each.ValueKind == JsonValueKind.String && each.GetString() is { Length: > 0 })
            .Select(each => each.GetString()!)
            .ToList();
        // The empty case is the property's own default, so an entry from before still equals itself read twice.
        if (units.Count == 0) return [];
        return units;
    }

    /// <summary>
    /// The advances an entry keeps (LAND2c), each whole or left out: one without the commits it moved between, when, or the
    /// session is no fact at all. Absent is none, which is every landing from before LAND2c.
    /// </summary>
    private static IReadOnlyList<LandedAdvance> AdvancesOf(JsonElement element)
    {
        if (!element.TryGetProperty("advances", out var list) || list.ValueKind != JsonValueKind.Array) return [];
        var advances = new List<LandedAdvance>();
        foreach (var each in list.EnumerateArray())
        {
            if (each.ValueKind != JsonValueKind.Object || Text(each, "from") is not { } from || Text(each, "to") is not { } to
                || Text(each, "session") is not { } session
                || Moments.Read(Text(each, "at")) is not { } at)
            {
                continue;
            }

            advances.Add(new LandedAdvance(from, to, at, session)
            {
                AcceptedBy = Text(each, "acceptedBy"), Integrate = IntegrateOf(each), Review = ReviewOf(each), Opinion = OpinionOf(each), Workflow = WorkflowOf(each),
                Squashed = Text(each, "squashed"), Quest = Text(each, "quest"), Unit = Text(each, "unit"), Submission = SubmissionOf(each),
            });
        }

        // The empty case is the property's own default, so an entry from before LAND2c still equals itself read twice.
        if (advances.Count == 0) return [];
        return advances;
    }
}
