namespace Daoris.Driver;

/// <summary>
/// The conversations waiting to be handed over (MIXAGENT1a-i, D163 §2): <c>&lt;home&gt;/sessions/&lt;session&gt;.hand-over.json</c>,
/// beside the session's marks. A box is the ask, kept with when it was given (D143 point 4), until the shell's chat runner takes
/// it: at once where the conversation is not talking, else at its turn's end, else as the shell comes up.
/// </summary>
/// <remarks>
/// <para><b>This machine's, never the record's.</b> A file that does not read is no ask: nothing is handed over, which loses nothing.</para>
///
/// <para>What is kept is this store's; the file and its format are <see cref="HandOverFiles"/>' (STRUCT3h, D166's note).</para>
/// </remarks>
public sealed class HandOverBox(string home)
{
    public const string Suffix = ".hand-over.json";

    /// <summary>Where a session's box is kept.</summary>
    public string PathOf(string session) => DaorisHome.Entry(home, HomeEntry.Sessions, session + Suffix);

    /// <summary>Keep the ask, replacing any before. An id that is not a session's keeps nothing.</summary>
    /// <exception cref="IOException">The ask could not be written: the asker is told, rather than told it was kept.</exception>
    public void Write(string session, HandOverRequest request)
    {
        if (!SessionEventText.IsLocalId(session)) return;
        HandOverFiles.WriteBox(PathOf(session), request);
    }

    /// <summary>The ask kept for this session, or null for none, a file that does not read, or an id that is not one.</summary>
    public HandOverRequest? Read(string session) => SessionEventText.IsLocalId(session) ? HandOverFiles.ReadBox(PathOf(session)) : null;

    /// <summary>The sessions with a box, in the order of their ids: what a shell coming up takes.</summary>
    public IReadOnlyList<string> Pending() =>
        [.. HandOverFiles.Leaves(DaorisHome.Entry(home, HomeEntry.Sessions),"*" + Suffix)
            .Select(leaf => leaf[..^Suffix.Length])
            .Where(SessionEventText.IsLocalId)
            .Order(StringComparer.Ordinal)];

    /// <summary>Take a session's box away: it was taken, or could not be. Nothing to take is no failure.</summary>
    public void Clear(string session)
    {
        // A box left behind is judged again when it is taken, and refused if it no longer holds.
        if (SessionEventText.IsLocalId(session)) HandOverFiles.Delete(PathOf(session));
    }
}

/// <summary>
/// *Hand a conversation over* (MIXAGENT1a-i, D163, the mixed-agents design §2 and §5): a conversation goes on with another agent
/// when its own account's allowance runs low. This is the ask's judge: it refuses what cannot be handed over by a code before
/// anything is kept, and keeps the <see cref="HandOverBox"/> the shell's chat runner takes.
/// </summary>
/// <remarks>
/// <b>Conversations only here.</b> A quest's session is <see cref="QuestHandOver"/>'s (MIXAGENT1a-ii); asked of this judge it is
/// refused <see cref="NotConversation"/>, saying where its door is. Ask Daoris's own
/// conversations and intakes never go on elsewhere (D137 §2.2), and the conversation cannot be handed to the agent and account it
/// runs on, nor while another hand-over waits, nor from a tree that is gone.
/// </remarks>
public static class HandOver
{
    /// <summary>A quest's session where no driven roster is handed in (the shell's page press, a later row), or a conversation about a quest.</summary>
    public const string NotConversation = "not-conversation";

    /// <summary>A quest's session whose quest is not open or taken any more (MIXAGENT1a-ii): no work is left to hand over.</summary>
    public const string QuestClosed = "quest-closed";

    /// <summary>A quest's session whose quest is not this machine's to carry on: taken elsewhere, or the session stood down.</summary>
    public const string QuestElsewhere = "quest-elsewhere";

    /// <summary>A quest's session whose take was lost to the person's own reopen of the work its step grew from (FABRIC-CLOSE1c20).</summary>
    public const string QuestReopened = "quest-reopened";

    /// <summary>A quest's session that is not the quest's last run on this machine, which a later session here carries.</summary>
    public const string NotLastRun = "not-last-run";

    /// <summary>A quest's session parked on the person, who answers it instead.</summary>
    public const string Parked = "parked";

    /// <summary>The named agent's door cannot drive the quest's repository: a pipe-door agent where Daoris is not adopted.</summary>
    public const string NoDoor = "no-door";

    /// <summary>The roster has no such agent.</summary>
    public const string UnknownAgent = "unknown-agent";

    /// <summary>The account is not one of that agent's.</summary>
    public const string NotItsAccount = "not-its-account";

    /// <summary>It already runs on that agent and account.</summary>
    public const string SameAgent = "same-agent";

    /// <summary>It runs in another Daoris process, which this one cannot end (the chat runner's own refusal).</summary>
    public const string Running = "running";

    /// <summary>The new conversation opened, but Daoris's briefing did not reach its agent (an answer of the take, not a refusal).</summary>
    public const string Unbriefed = "unbriefed";

    /// <summary>Another hand-over of it waits.</summary>
    public const string Waiting = "waiting";

    /// <summary>The tree it ran in no longer exists, so the new conversation has nowhere to go on.</summary>
    public const string TreeGone = "tree-gone";

    /// <summary>Judge and keep the box for a conversation of this machine's.</summary>
    /// <param name="accountsOf">The accounts the roster holds for an agent, or null where it has no such agent.</param>
    public static async Task<HandOverAnswer> AskAsync(
        ServiceClient service, string home, Func<string, IReadOnlyList<string>?> accountsOf, string id, string agent, string? account,
        string why, string askedBy, DateTimeOffset now, CancellationToken ct = default)
    {
        var (refused, _) = await JudgeAsync(service, accountsOf, id, agent, account, ct).ConfigureAwait(false);
        if (refused is not null) return refused;

        var box = new HandOverBox(home);
        if (box.Read(id) is { } waiting)
        {
            return Refused(Waiting, $"{id} is already waiting to be handed over to {waiting.Agent}.");
        }

        var named = string.IsNullOrWhiteSpace(account) ? null : account.Trim();
        try
        {
            box.Write(id, new HandOverRequest(agent, named, why, askedBy, now));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new HandOverAnswer(false, null, $"your ask could not be kept: {error.Message}");
        }

        return new HandOverAnswer(true, null, $"it goes on with {agent} as soon as its turn is over, in a conversation of its own.");
    }

    /// <summary>
    /// What can be known of a hand-over before anything is kept or taken: the refusal by its code, or the record as the ledger holds
    /// it. Judged again when the box is taken, since the conversation, its tree or the roster may have changed while it waited.
    /// </summary>
    internal static async Task<(HandOverAnswer? Refused, PriorSession? Prior)> JudgeAsync(
        ServiceClient service, Func<string, IReadOnlyList<string>?> accountsOf, string id, string agent, string? account,
        CancellationToken ct = default)
    {
        var json = await service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        var record = SessionRecords.Parse(json).FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal));
        if (record is null) return (Refused(WordsNever.NotFound, $"no session here is {id}."), null);

        if (Never(record, id, WordsNever.LastHere(json, record.Quest), WordsNever.ContinuedIn(json, id)) is { } never) return (never, null);

        if (record.Kind != "chat" || record.Quest is not null)
        {
            return (Refused(NotConversation, record.Kind == "chat"
                ? $"{id} is a conversation about a quest, not its session: hand over the quest's own session instead."
                : $"{id} is a quest's session: it is handed over from a terminal, `daoris-driver sessions hand-over {id} --agent <adapter>`; its page's press comes later."), null);
        }

        var accounts = accountsOf(agent);
        if (accounts is null) return (Refused(UnknownAgent, $"no agent here is called {agent}."), null);

        var named = string.IsNullOrWhiteSpace(account) ? null : account.Trim();
        if (named is not null && !accounts.Contains(named, StringComparer.OrdinalIgnoreCase))
        {
            return (Refused(NotItsAccount, $"{named} is not an account of {agent}."), null);
        }

        var prior = ServiceClient.ReadRecord(json, id);
        if (prior is not null && string.Equals(prior.Adapter, agent, StringComparison.OrdinalIgnoreCase)
            && string.Equals(prior.Profile ?? "", named ?? "", StringComparison.OrdinalIgnoreCase))
        {
            return (Refused(SameAgent, named is null ? $"{id} already runs on {agent}." : $"{id} already runs on {agent} as {named}."), null);
        }

        if (prior?.Tree is not { Length: > 0 } tree || !ConversationTrees.Stands(tree))
        {
            return (Refused(TreeGone, $"the tree {id} ran in is gone, so there is nothing to go on in."), null);
        }

        return (null, prior);
    }

    /// <summary>
    /// What never goes on elsewhere, whichever kind of session asks: a teammate's record, Ask Daoris's own conversation, an intake,
    /// and a conversation that went on in another already (HANDWORDS1), whose tree that one holds: a second take would open another
    /// conversation in it, and a box left from before the shell closed is refused as it is taken.
    /// </summary>
    /// <param name="next">The conversation it went on in (<see cref="WordsNever.ContinuedIn(string, string)"/>), or null.</param>
    internal static HandOverAnswer? Never(SessionRecord record, string id, string? last, string? next = null)
    {
        switch (WordsNever.Judge(record, last, next) ?? (WordsNever.IsHelp(record) ? WordsNever.Help : null))
        {
            case WordsNever.Continued:
                return Refused(WordsNever.Continued, $"{id} went on in conversation {next}; hand that one over instead.");
            case WordsNever.Teammate:
                return Refused(WordsNever.Teammate, $"{id} ran on {id[..id.IndexOf('/')]}, where its conversation is; nothing said here reaches it.");
            case WordsNever.Help:
                return Refused(WordsNever.Help,
                    $"{id} is Ask Daoris's own conversation, which stays on its own agent: Ask Daoris's history starts a new conversation from it.");
            case WordsNever.Intake:
                return Refused(WordsNever.Intake, $"{id} is an intake; answer its ask #{record.Ask} instead: publish it or close it.");
            default:
                return null;
        }
    }

    internal static HandOverAnswer Refused(string why, string message) => new(false, why, message);
}

/// <summary>
/// Taking a hand-over's box (MIXAGENT1a-i, D163 §3): judged again, then the old conversation ends, the new starts in its tree on
/// the agent and account asked for and is told Daoris's briefing, and both records say where it went and where it came from.
/// Pure of the runner's own parts, which are handed in, so the sequence is held in-process.
/// </summary>
/// <remarks>
/// <b>The old ends before the new starts</b>: a tree is held by one session at a time (D51). <b>The box is gone however it came
/// out</b>, so a refused start is not tried again at every shell's come-up; the asker is told, and asks again.
/// </remarks>
public static class HandOverTake
{
    /// <summary>Judge, end, start, brief and say where it went.</summary>
    /// <param name="end">Ends the old conversation by the runner's own end, and returns once its record has ended.</param>
    /// <param name="start">
    /// Opens the new conversation in the old one's tree on the box's agent and account, its record naming the one it continues,
    /// why and who asked (MIXAGENT1a-iii).
    /// </param>
    /// <param name="say">Hands the new conversation its first message; false where nothing took it.</param>
    /// <param name="moved">
    /// What the usage curve's decision said, where Daoris moves the conversation to another account by it (SWITCH1c-ii): the
    /// two lines are then the moved pair (<see cref="HandOverNotes.Moved"/>, <see cref="HandOverNotes.MovedFrom"/>), naming the
    /// accounts. Null for every other take, and for a curve's box taken where the decision is not held (a shell come up since),
    /// whose lines are the hand-over's, its reason <c>curve</c>.
    /// </param>
    public static async Task<HandedOver> RunAsync(
        ServiceClient service, string home, SessionEvents events, SessionOutput? output, string id, HandOverRequest ask,
        Func<string, IReadOnlyList<string>?> accountsOf, Func<string, Task> end,
        Func<PriorSession, HandOverRequest, ContinuedFrom, CancellationToken, Task<ChatStart>> start, Func<string, ChatMessage, Task<bool>> say,
        CancellationToken ct = default, SwitchChoice.MoveTo? moved = null)
    {
        var box = new HandOverBox(home);
        try
        {
            var (refused, prior) = await HandOver.JudgeAsync(service, accountsOf, id, ask.Agent, ask.Account, ct).ConfigureAwait(false);
            if (refused is not null) return new HandedOver(null, false, refused.Why, refused.Message);

            var from = prior!.Adapter ?? "its agent";
            var uncommitted = await ConversationTrees.UncommittedAsync(prior.Tree!, HandOverBriefing.ListLimit, ct).ConfigureAwait(false);
            var (plan, words) = SessionLastWords.CarriedFrom(events, home, id);
            // MIXAGENT1c: where its agent asked to hand over, its own note, told after Daoris's record as that agent's claim.
            var note = ask.AskedBy == HandOverBy.Agent ? new HandOverAskFiles(home).Ask(id)?.Note : null;
            var briefing = HandOverBriefing.Compose(from, id, ask.Why, events.FirstAsked(id), plan, words, uncommitted, note);

            await end(id).ConfigureAwait(false);

            var started = await start(prior, ask, new ContinuedFrom(id, ask.Why, ask.AskedBy), ct).ConfigureAwait(false);
            if (started.SessionId is not { } to)
            {
                return new HandedOver(
                    null, false, null,
                    $"{id} ended, but the conversation with {ask.Agent} did not start: {started.Message} Its tree is as it was.");
            }

            // A move by the usage curve names the account it left and the one it went to (SWITCH1c-ii): the record that says so stays
            // on this machine. Only where the decision is the box's own move, on the account the record ran on.
            var left = moved is not null && ask.Why == HandOverWhy.Curve
                       && string.Equals(moved.Account, ask.Account, StringComparison.OrdinalIgnoreCase) && prior.Profile is { Length: > 0 } account
                ? account
                : null;
            var opening = left is null ? HandOverNotes.TakenOver(from, id, ask.Why) : HandOverNotes.MovedFrom(left, moved!);
            var taken = await say(to, new ChatMessage("", []) { Briefing = briefing, TakenOver = opening }).ConfigureAwait(false);

            // Where it went, where the person wrote it: the old conversation's last line, after its own end.
            var went = left is null ? HandOverNotes.HandedOver(ask.Agent, to) : HandOverNotes.Moved(left, moved!);
            output?.Append(id, went.Note);
            events.Keep(id, new SessionEvent { Kind = SessionEventKind.Note, Text = went.Note, Parts = went.Parts }, say: null);

            // A briefing nobody took is no success: the new conversation says so, by a code, and the answer carries it.
            if (!taken)
            {
                var unsaid = HandOverNotes.Unsaid();
                output?.Append(to, unsaid.Note);
                events.Keep(to, new SessionEvent { Kind = SessionEventKind.Note, Text = unsaid.Note, Parts = unsaid.Parts }, say: null);
            }

            return new HandedOver(
                to, true, taken ? null : HandOver.Unbriefed,
                taken
                    ? $"conversation `{to}` goes on from `{id}` with {ask.Agent}."
                    : $"conversation `{to}` opened with {ask.Agent}, but what Daoris kept of `{id}` did not reach it: tell it where you were.");
        }
        finally
        {
            box.Clear(id);
        }
    }
}
