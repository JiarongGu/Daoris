using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>The service's doors a source's reopen is told through (FABRIC-CLOSE1c8): a seam, so the look's rule is held by tests without a host.</summary>
public interface IReopenDoors
{
    /// <summary>This machine's record with this id, as a run that goes on reads it; null where none is kept here.</summary>
    Task<PriorSession?> RecordAsync(string session, CancellationToken ct);

    /// <summary>The say door (MSG1a): words kept on a parked or ended record of this machine's, for it to go on with.</summary>
    Task<SayAnswer> SayAsync(string session, string text, CancellationToken ct);
}

/// <summary>The reopen's doors through the loop's service: the records' read and the say door.</summary>
public sealed class ServiceReopenDoors(ServiceClient service) : IReopenDoors
{
    public Task<PriorSession?> RecordAsync(string session, CancellationToken ct) => service.RecordAsync(session, ct);

    public Task<SayAnswer> SayAsync(string session, string text, CancellationToken ct) => service.SayAsync(session, text, null, ct);
}

/// <summary>
/// A source's reopen told to the session that said its <c>ready</c> (FABRIC-CLOSE1c8; the fabric events design §E5, <i>its session
/// is told at its next step</i>): once, through the say door, so the planner's go-on resumes that conversation with the words.
/// </summary>
/// <remarks>
/// <para><b>Who.</b> The session the reopen names (<see cref="QuestReopenView.Session"/>, the one that said the reopened ready), on
/// this machine alone: a teammate's (<c>origin/id</c>) is never told here, and a reopen naming none (a ready the respond door made,
/// or a host before the field) tells nobody, the run reading it as waiting for its agent. Never the quest's last record here, which
/// need not be the one that said it: where a later session ran, the planner goes on with the words in the named one all the same
/// (FABRIC-CLOSE1c9, <see cref="Snapshot.NamedByReopen"/>), unless the person stopped it, when it is told nothing and the look says
/// so once, since Daoris's words would release the person's stop.</para>
///
/// <para><b>When.</b> While the quest is taken and its reopen stands; a later submission answers it, so nothing is said. A parked
/// or ended record is told at once: words on a park are its answer, on an ended record they reopen it, and the planner's go-on
/// resumes it at the next look. A running one is told at the first look that finds it parked or ended, since the say door refuses a
/// running record. A record this machine keeps no more, or whose tree is gone, is told nothing, since its go-on would make the work
/// again: the look says why, once.</para>
///
/// <para><b>Once.</b> A mark beside the session's transcript (<see cref="ReopenMarks"/>), written once the say door kept the words,
/// keyed by the quest and the reopen's machine and sequence, holds it across a restart; this telling's own memory holds it where the
/// mark could not be written, and holds a refusal other than <c>running</c>, said once and not asked again. A crash between the
/// say and the mark may tell it twice (D173's note).</para>
///
/// <para><b>The cost.</b> Nothing is read where no quest this look read is a reopened source; otherwise one read of the records per
/// reopen not yet told, unless the look's last records or <see cref="Snapshot.NamedByReopen"/> hold its session.</para>
///
/// <para><b>The step's go-on</b> (FABRIC-CLOSE1c13): a step the person went on with after its last session here had ended normally,
/// parked after its <c>ready</c> or completed (<see cref="Planner.GoneOnSince"/>), is told so in that session with Daoris's words, and
/// the start that goes on with them brings its branch up to date with the source's new submission first (<see cref="BringsToSource"/>).
/// Once with no mark, by the fact itself; nothing more is read.</para>
/// </remarks>
public sealed class ReopenTelling(ReopenMarks marks)
{
    /// <summary>The reopens settled in this run, by <see cref="Key"/>: told, or said once why they are not.</summary>
    private readonly ConcurrentDictionary<string, bool> _settled = new(StringComparer.Ordinal);

    /// <summary>One look's telling over the quests it read: the lines it says.</summary>
    /// <exception cref="OperationCanceledException">The loop is closing.</exception>
    public async Task<IReadOnlyList<string>> LookAsync(IReopenDoors doors, Snapshot snapshot, CancellationToken ct)
    {
        var lines = new List<string>();
        foreach (var quest in snapshot.Quests)
        {
            if (quest is not { Status: "Taken", Reopened: { Session: { Length: > 0 } session } reopen } || session.Contains('/')) continue;
            var key = Key(quest.Id, reopen);
            if (_settled.ContainsKey(key) || marks.Told(session, quest.Id, reopen)) continue;

            try
            {
                await TellAsync(doors, snapshot, quest.Id, reopen, session, key, lines, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException
                                              or InvalidOperationException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                lines.Add($"reopen  quest #{quest.Id}'s reopen was not told to session {session} at this look, and the next tries again: {error.Message}");
            }
        }

        // FABRIC-CLOSE1c13 (§E5, the step rebinds once unheld, its branch brought up to date): a step the person went on with after its
        // session here had ended normally is told so in that session, which the planner then goes on with, from the look's own reads.
        foreach (var step in snapshot.Quests)
        {
            if (!snapshot.LastRun.TryGetValue(step.Id, out var last) || !Planner.GoneOnSince(step, last)) continue;
            var key = $"{step.Id}\n{last.Session}\n{step.GrewFrom!.Machine}\n{step.GrewFrom.Sequence}\ngone-on";
            if (_settled.ContainsKey(key)) continue;

            try
            {
                await TellGoneOnAsync(doors, step, last, key, lines, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException
                                              or InvalidOperationException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                lines.Add($"reopen  quest #{step.Id}'s go-on was not told to session {last.Session} at this look, and the next tries again: {error.Message}");
            }
        }

        return lines;
    }

    /// <summary>
    /// Tell <paramref name="last"/> that the person went on with <paramref name="step"/> (FABRIC-CLOSE1c13): words on a park are its
    /// answer, on an ended record they reopen it, so the planner goes on in that same session. A tree gone is told nothing, as a
    /// reopen's is, since its go-on would make the work again; said once.
    /// </summary>
    private async Task TellGoneOnAsync(IReopenDoors doors, QuestView step, PriorSession last, string key, List<string> lines, CancellationToken ct)
    {
        if (last.Tree is not { Length: > 0 } tree || !Directory.Exists(tree))
        {
            Settle(key, lines,
                $"reopen  quest #{step.Id} was gone on with on the new work of #{step.Parent} after session {last.Session} ended, and its tree "
                + "is gone: it is told nothing, and no work is made again for it");
            return;
        }

        var said = await doors.SayAsync(last.Session, GoneOnWords(step.Id, step.Parent!, step.GrewFrom!), ct).ConfigureAwait(false);
        if (said.Kept)
        {
            _settled[key] = true;
            lines.Add(
                $"reopen  session {last.Session} was told the person went on with quest #{step.Id} on the new work of #{step.Parent}, and goes on "
                + "with Daoris's words");
            return;
        }

        // A record that began running since it was read is told at a later look; any other refusal is said once.
        if (said.Refusal != "running") Settle(key, lines, $"reopen  session {last.Session} was not told of quest #{step.Id}'s go-on: {said.Message}");
    }

    private async Task TellAsync(
        IReopenDoors doors, Snapshot snapshot, string quest, QuestReopenView reopen, string session, string key, List<string> lines,
        CancellationToken ct)
    {
        // The look's last records name the session where it is the quest's newest here, and the look's snapshot where a later one
        // is (FABRIC-CLOSE1c9); otherwise its record is read by its id.
        var record = snapshot.LastRun.Values.FirstOrDefault(run => string.Equals(run.Session, session, StringComparison.Ordinal))
                     ?? (snapshot.NamedByReopen.TryGetValue(quest, out var held) && string.Equals(held.Session, session, StringComparison.Ordinal)
                         ? held
                         : null)
                     ?? await doors.RecordAsync(session, ct).ConfigureAwait(false);
        var named = $"quest #{quest}'s reopened submission names session {session}";
        if (record is null)
        {
            Settle(key, lines, $"reopen  {named}, which this machine keeps no record of: nobody here is told");
            return;
        }

        if (record.Running) return;

        if (record.Tree is not { Length: > 0 } tree || !Directory.Exists(tree))
        {
            Settle(key, lines, $"reopen  {named}, whose tree is gone: it is told nothing, and no work is made again for it");
            return;
        }

        // 🔴 The person stopped it, and a later session ran (FABRIC-CLOSE1c9): words on it would go on in it, releasing the person's
        // stop as their own words do, and these are Daoris's. So it is told nothing, and the quest's run reads as it does.
        if (record.PersonStopped
            && snapshot.LastRun.GetValueOrDefault(quest) is { } later && !string.Equals(later.Session, session, StringComparison.Ordinal))
        {
            Settle(key, lines,
                $"reopen  {named}, which the person stopped, and a later session ran: it is told nothing, and nothing goes on with it "
                + "until they say so");
            return;
        }

        var said = await doors.SayAsync(session, Words(quest, reopen), ct).ConfigureAwait(false);
        if (said.Kept)
        {
            _settled[key] = true;
            marks.Mark(session, quest, reopen, DateTimeOffset.UtcNow);
            lines.Add($"reopen  session {session} was told the person reopened its submission on quest #{quest}, and goes on with their words");
            return;
        }

        // A record that began running since it was read is told at a later look; any other refusal is said once.
        if (said.Refusal != "running") Settle(key, lines, $"reopen  session {session} was not told of quest #{quest}'s reopen: {said.Message}");
    }

    private void Settle(string key, List<string> lines, string line)
    {
        if (_settled.TryAdd(key, false)) lines.Add(line);
    }

    private static string Key(string quest, QuestReopenView reopen) => $"{quest}\n{reopen.Machine}\n{reopen.Sequence}";

    /// <summary>
    /// What a step's session is told once the person went on with it on its source's new submission (FABRIC-CLOSE1c13): Daoris's own
    /// sentence, naming that submission, which is also how the start knows to bring its branch up to date (<see cref="BringsToSource"/>).
    /// What the start did with the branch is said after it (LANDSYNC2's place).
    /// </summary>
    public static string GoneOnWords(string quest, string source, QuestSubmissionRef grew) =>
        $"Daoris: the person reopened the work of `#{source}` that quest `#{quest}` grew from, and went on with `#{quest}` on its new "
        + $"submission `{grew.Machine}` {grew.Sequence}, so any result submitted on `#{quest}` was cleared. Bring your work up to date with "
        + $"that submission (what Daoris did with your branch follows, where it did anything), then say `ready` on `#{quest}` again.";

    /// <summary>
    /// Whether a step going on from <paramref name="record"/> is first brought up to date with its source's new submission, then the line
    /// (FABRIC-CLOSE1c5's <c>GoOnInSyncAsync</c>): the session the reopen stopped, carried on, and (FABRIC-CLOSE1c13) one that had ended
    /// normally, going on with Daoris's words of the go-on to the submission the step grows from now, while it has no result again.
    /// Every other going-on syncs with the line alone, as before.
    /// </summary>
    public static bool BringsToSource(QuestView step, PriorSession record) =>
        step.GrewFrom is { } grew
        && (Planner.StoppedByReopen(record)
            || (step is { Submission: null, Parent: { Length: > 0 } source }
                && record.Waiting.Any(word => string.Equals(word.Text, GoneOnWords(step.Id, source, grew), StringComparison.Ordinal))));

    /// <summary>What the session is told: Daoris's own sentence naming the reopened submission, then the person's words quoted.</summary>
    public static string Words(string quest, QuestReopenView reopen)
    {
        var sentence =
            $"Daoris: the person reopened submission `{reopen.Of.Machine}` {reopen.Of.Sequence} of quest `#{quest}`, which your `ready` "
            + $"made, so the quest is not ready any more. Address what they reopened it for, then say `ready` on `#{quest}` again.";
        if (reopen.Note?.Trim() is not { Length: > 0 } note) return sentence;

        var quoted = string.Join("\n", note.ReplaceLineEndings("\n").Split('\n').Select(line => line.Length == 0 ? ">" : "> " + line));
        return $"{sentence} Their words:\n\n{quoted}";
    }
}
