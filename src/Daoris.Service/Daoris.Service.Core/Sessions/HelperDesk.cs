namespace Daoris.Knowledge;

/// <summary>Why a door to a helper's record refused (MIXAGENT2a-ii) — or <see cref="None"/> when it did what was asked.</summary>
public enum HelperRefusal
{
    None,

    /// <summary>A shared host: a helper reads a clone this machine made, and a shared host keeps none.</summary>
    Shared,

    /// <summary>No such session of this machine's, to answer or to be answered.</summary>
    NotFound,

    /// <summary>A part that is not one: a blank, or a session that cannot be what it is named as.</summary>
    BadShape,

    /// <summary>The helper's tree is held by another session: one session per working tree (D51).</summary>
    Busy,

    /// <summary>The answer was handed already, or the helper does not serve this session.</summary>
    NotHanded,

    /// <summary>The asking session cannot take words now: running, parked, stood down, a teammate's, an intake or a reviewer.</summary>
    CannotTake,
}

/// <param name="Refusal"><see cref="HelperRefusal.None"/> when the door did what was asked.</param>
/// <param name="Message">The whole answer, phrased once here for every door.</param>
public sealed record HelperOutcome(HelperRefusal Refusal, string Message)
{
    /// <summary>The helper's record a pass opened, or the asker's record the answer was handed to.</summary>
    public Session? Session { get; init; }

    /// <summary>The word the answer waits in on the asker's record, when it was handed.</summary>
    public SaidWord? Word { get; init; }

    /// <summary>
    /// The hand was answered <b>settled</b> (FOUNDATION1 row p, the design §3.4 rule 5): its key's word left a record of the asker's line within the
    /// horizon, and this names that record. Its own terminal kind, never a word: nothing was kept now, and <see cref="Session"/> and
    /// <see cref="Word"/> are null. Only a hand that asked for it (<c>settled</c>) is answered so.
    /// </summary>
    public string? SettledOn { get; init; }

    /// <summary>
    /// The answer was waiting already (COMMS1b): <see cref="Session"/> holds it and <see cref="Word"/> is it, so the caller dispatches it
    /// there before it marks the ask handed. Nothing was kept now.
    /// </summary>
    public bool Waiting { get; init; }
}

/// <summary>What the driver names for a helper's record: it is the driver's to say where it reads, as which agent and account.</summary>
/// <param name="Asker">The session it answers.</param>
/// <param name="Adapter">The agent that reads.</param>
/// <param name="Tree">Its clone of the repository, never the asker's tree.</param>
public sealed record HelperOpen(string Asker, string Adapter, string Tree)
{
    public string? Account { get; init; }

    public string? HarnessVersion { get; init; }

    /// <summary>The asker's commit the clone stands at.</summary>
    public string? BaseCommit { get; init; }
}

/// <summary>
/// The judgement over a helper's record and its answer's return (MIXAGENT2a-ii, D163 point 2; the mixed agents design §3): one
/// implementation for the local host's two doors the driver calls, as <see cref="OpinionDesk"/> is for a second opinion. It
/// opens a read-only helper's record, a chat that serves the asking session and no quest, in a clone of its own; and hands the
/// helper's answer to the asker as a said word marked as the helper's, never the person's. Whether the rule allows a helper, how
/// many at once and which agent are the driver's, which alone reads <c>driver.json</c>.
/// </summary>
/// <param name="local">Whether this is a local host's desk; a shared host's refuses every door.</param>
public sealed class HelperDesk(SessionStore sessions, bool local = true)
{
    private const string Shared = "A helper reads a clone this machine made, and is answered only on this machine.";

    /// <summary>
    /// Open the helper's record: a chat on no quest that serves <paramref name="open"/>'s asker, in the repository and workspace
    /// the asker works in, in the tree the driver cloned. Judged first: the asker is a session of this machine's that is no
    /// helper, no reviewer, no intake and not Ask Daoris, and the clone is nobody's but the helper's.
    /// </summary>
    public async Task<HelperOutcome> OpenAsync(HelperOpen open, DateTimeOffset now, CancellationToken ct = default)
    {
        if (!local) return new(HelperRefusal.Shared, Shared);
        if (string.IsNullOrWhiteSpace(open.Asker) || string.IsNullOrWhiteSpace(open.Adapter) || Trees.Normalize(open.Tree) is not { } tree)
        {
            return Refused(HelperRefusal.BadShape, "A helper's record needs the session it answers, its agent and its own clone");
        }

        return await sessions.ExclusiveAsync(async inside =>
        {
            var asker = await sessions.FindAsync(open.Asker.Trim(), inside).ConfigureAwait(false);
            if (asker is not { Origin: null })
            {
                return Refused(HelperRefusal.NotFound, $"There is no session `{open.Asker.Trim()}` of this machine's to answer");
            }

            string? wrong =
                asker.Opinion is { } reads ? $"Session `{asker.Id}` reads another session's work for second opinion `{reads}`, and a reviewer cannot ask for a helper"
                : asker.Serves is { } serves ? $"Session `{asker.Id}` is a helper for session `{serves}`, and a helper cannot ask for a helper"
                : asker.Ask is { } ask ? $"Session `{asker.Id}` is the intake for ask `#{ask}`, and an intake cannot ask for a helper"
                // FOUNDATION1q (D203 point 6): the store's own guard, held to the driver's start layer by fixtures/helper-askers.json.
                : SessionLedger.IsHelp(asker) ? $"Session `{asker.Id}` is Ask Daoris's conversation, and Ask Daoris cannot ask for a helper"
                : string.Equals(Trees.Normalize(asker.Tree), tree, StringComparison.Ordinal)
                    ? $"`{tree}` is session `{asker.Id}`'s own tree: a helper reads a clone of its own"
                : null;
            if (wrong is not null) return Refused(HelperRefusal.BadShape, wrong);

            if (await sessions.ActiveForAsync(asker.Repository, tree, inside).ConfigureAwait(false) is { } holder)
            {
                return Refused(HelperRefusal.Busy, $"`{asker.Repository}`'s tree `{tree}` already has an active session, `{holder.Id}`: a helper's clone is its own");
            }

            var helper = await sessions.CreateAsync(
                    quest: null, asker.Repository, open.Adapter.Trim(), now, asker.Workspace, SessionKind.Chat,
                    harnessVersion: open.HarnessVersion, profile: open.Account, tree: tree, baseCommit: open.BaseCommit?.ToLowerInvariant(),
                    ct: inside, serves: asker.Id, ownEntry: true, actor: "helper")
                .ConfigureAwait(false);
            return new HelperOutcome(
                HelperRefusal.None,
                $"Session `{helper.Id}` reads `{asker.Repository}` in a clone of its own, to answer session `{asker.Id}`.")
            {
                Session = helper,
            };
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Hand a helper's answer to the session it serves: a word on its record naming the helper as <see cref="SaidWord.Helper"/>,
    /// which reopens it at the driver's next look (D137). Once, through <see cref="SolicitedAnswers"/>, the door every solicited answer
    /// shares: to the head of the asker's line, by that door's table of the head's state.
    /// </summary>
    /// <param name="text">The words as Daoris composed them around the helper's claim.</param>
    /// <param name="running">
    /// The driver that runs a working driven asker hands an urgent ask's answer for its run (COWORK1d, the message-urgency design §5): kept
    /// on the running record's words, which that run's inbox takes at its next step and takes off by its id. Without it a running asker
    /// takes the answer at its end, as before.
    /// </param>
    /// <param name="level">The ask's level, kept beside the word (COWORK1d): <c>urgent</c> where the ask was; none for an ordinary ask.</param>
    public async Task<HelperOutcome> HandAsync(
        string helperId, string text, DateTimeOffset now, CancellationToken ct = default, bool running = false, string? level = null, bool settled = false)
    {
        if (!local) return new(HelperRefusal.Shared, Shared);
        if (string.IsNullOrWhiteSpace(text)) return Refused(HelperRefusal.BadShape, "The words handed are blank");

        return await sessions.ExclusiveAsync(async inside =>
        {
            // B5: a reviewer carries `serves` too, so the opinion mark is read first.
            var helper = await sessions.FindAsync(helperId.Trim(), inside).ConfigureAwait(false);
            if (helper is not { Origin: null, Opinion: null, Serves: { } served })
            {
                return Refused(HelperRefusal.NotFound, $"There is no helper session `{helperId.Trim()}` on this machine");
            }

            var outcome = await SolicitedAnswers.KeepAsync(
                sessions,
                new SolicitedAsk(
                    served, await sessions.FindAsync(served, inside).ConfigureAwait(false), KeyOf(helper.Id), text, $"helper `{helper.Id}`'s answer", "a helper's answer")
                {
                    Helper = helper.Id, Level = level, Running = running, Settled = settled,
                },
                now, inside).ConfigureAwait(false);
            return Handed(outcome, $"helper `{helper.Id}`'s answer", "it waits on its record as another agent's claim, never the person's words");
        }, ct).ConfigureAwait(false);
    }

    /// <summary>The desk's answer to what the solicited-answer door did; shared by the helper and the partner desk.</summary>
    internal static HelperOutcome Handed(SolicitedOutcome outcome, string subject, string waits) => outcome.Fate switch
    {
        SolicitedFate.Kept => new HelperOutcome(
            HelperRefusal.None,
            $"Handed {subject} to session `{outcome.Session!.Id}`: {waits}, and it goes on with it at the driver's next look.")
        {
            Session = outcome.Session,
            Word = outcome.Word,
        },
        SolicitedFate.Waiting => new HelperOutcome(HelperRefusal.None, outcome.Message)
        {
            Session = outcome.Session,
            Word = outcome.Word,
            Waiting = true,
        },
        SolicitedFate.Settled => new HelperOutcome(HelperRefusal.None, outcome.Message) { SettledOn = outcome.SettledOn },
        _ => new HelperOutcome(
            outcome.Refusal switch
            {
                SolicitedRefusal.NotFound => HelperRefusal.NotFound,
                SolicitedRefusal.NotHanded => HelperRefusal.NotHanded,
                _ => HelperRefusal.CannotTake,
            },
            $"{outcome.Message}. Nothing was kept."),
    };

    /// <summary>A helper's answer's stable key, the producer key its word carries (HELPERWAIT1): one per helper, and so one per ask.</summary>
    public static string KeyOf(string helper) => $"helper:{helper}";

    private static HelperOutcome Refused(HelperRefusal refusal, string why) => new(refusal, $"{why}. Nothing was kept.");
}
