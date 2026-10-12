using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>Why a conversation was handed to another agent, by code (MIXAGENT1a-i, D163): the page's <c>note.handed.why.*</c>.</summary>
public static class HandOverWhy
{
    /// <summary>The person pressed it.</summary>
    public const string Press = "press";

    /// <summary>The account it ran on was near its limit.</summary>
    public const string Near = "near";

    /// <summary>The account it ran on reached its limit.</summary>
    public const string Limit = "limit";

    /// <summary>The agent asked for it.</summary>
    public const string Agent = "agent";

    /// <summary>The reason as the briefing's agent reads it, in Daoris's one English: after <c>handed over because</c>.</summary>
    public static string Briefed(string why) => why switch
    {
        Press => "the person asked for it",
        Near => "the account it ran on was near its limit",
        Limit => "the account it ran on reached its limit",
        Agent => "the agent asked for it",
        _ => why,
    };

    /// <summary>The reason as the record's English says it, after <c>handed over because</c>.</summary>
    public static string Noted(string why) => why switch
    {
        Press => "the person asked for it",
        Near => "its account was near its limit",
        Limit => "its account reached its limit",
        Agent => "the agent asked for it",
        _ => why,
    };

    /// <summary>Whether a word is one of the reasons.</summary>
    public static bool Known(string? why) => why is Press or Near or Limit or Agent;
}

/// <summary>Who asked for a hand-over (MIXAGENT1a-i, D163), by code.</summary>
public static class HandOverBy
{
    /// <summary>The person, by a press or the terminal verb.</summary>
    public const string Person = "person";

    /// <summary>Daoris, by its own look at an account.</summary>
    public const string Daoris = "daoris";

    /// <summary>The agent, by its own ask.</summary>
    public const string Agent = "agent";

    /// <summary>Whether a word is one of the askers.</summary>
    public static bool Known(string? by) => by is Person or Daoris or Agent;
}

/// <summary>A conversation's hand-over as asked (MIXAGENT1a-i): the agent and account it goes to, why, by whom, and when.</summary>
/// <param name="Agent">The adapter the new conversation runs on.</param>
/// <param name="Account">The account it runs as, or null for the one the roster picks.</param>
/// <param name="Why">One of <see cref="HandOverWhy"/>.</param>
/// <param name="AskedBy">One of <see cref="HandOverBy"/>.</param>
public sealed record HandOverRequest(string Agent, string? Account, string Why, string AskedBy, DateTimeOffset At);

/// <summary>
/// The conversations waiting to be handed over (MIXAGENT1a-i, D163 §2): <c>&lt;home&gt;/sessions/&lt;session&gt;.hand-over.json</c>,
/// beside the session's marks. A box is the ask, kept with when it was given (D143 point 4), until the shell's chat runner takes
/// it: at once where the conversation is not talking, else at its turn's end, else as the shell comes up.
/// </summary>
/// <remarks><b>This machine's, never the record's.</b> A file that does not read is no ask: nothing is handed over, which loses nothing.</remarks>
public sealed class HandOverBox(string home)
{
    public const string Suffix = ".hand-over.json";

    /// <summary>Where a session's box is kept.</summary>
    public string PathOf(string session) => Path.Combine(home, "sessions", session + Suffix);

    /// <summary>Keep the ask, replacing any before. An id that is not a session's keeps nothing.</summary>
    /// <exception cref="IOException">The ask could not be written: the asker is told, rather than told it was kept.</exception>
    public void Write(string session, HandOverRequest request)
    {
        if (!SessionEvents.IsId(session)) return;

        var path = PathOf(session);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("agent", request.Agent);
            if (request.Account is { } account) writer.WriteString("account", account); else writer.WriteNull("account");
            writer.WriteString("why", request.Why);
            writer.WriteString("askedBy", request.AskedBy);
            writer.WriteString("at", request.At.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteEndObject();
        }

        // LF on every platform, as every file under the home is written.
        AtomicFile.WriteText(path, Encoding.UTF8.GetString(buffer.ToArray()).Replace("\r\n", "\n") + "\n");
    }

    /// <summary>The ask kept for this session, or null for none, a file that does not read, or an id that is not one.</summary>
    public HandOverRequest? Read(string session)
    {
        if (!SessionEvents.IsId(session)) return null;

        try
        {
            var path = PathOf(session);
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || Text(root, "agent") is not { Length: > 0 } agent
                || Text(root, "why") is not { } why || !HandOverWhy.Known(why)
                || Text(root, "askedBy") is not { } by || !HandOverBy.Known(by)
                || !DateTimeOffset.TryParse(Text(root, "at"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at))
            {
                return null;
            }

            return new HandOverRequest(agent, Text(root, "account") is { Length: > 0 } account ? account : null, why, by, at);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The sessions with a box, in the order of their ids: what a shell coming up takes.</summary>
    public IReadOnlyList<string> Pending()
    {
        var folder = Path.Combine(home, "sessions");
        if (!Directory.Exists(folder)) return [];
        try
        {
            return [.. Directory.EnumerateFiles(folder, "*" + Suffix)
                .Select(file => Path.GetFileName(file)[..^Suffix.Length])
                .Where(SessionEvents.IsId)
                .Order(StringComparer.Ordinal)];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Take a session's box away: it was taken, or could not be. Nothing to take is no failure.</summary>
    public void Clear(string session)
    {
        if (!SessionEvents.IsId(session)) return;
        try
        {
            File.Delete(PathOf(session));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A box left behind is judged again when it is taken, and refused if it no longer holds.
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

/// <summary>What a hand-over's ask came to (MIXAGENT1a-i): kept, or refused by a code, and its sentence.</summary>
/// <param name="Kept">The box is kept: the shell's chat runner takes it.</param>
/// <param name="Why">Why not, by a code of <see cref="WordsNever"/> or <see cref="HandOver"/>; null where it was kept.</param>
/// <param name="Message">The sentence, in the terminal's words; the screen words <see cref="Why"/> itself.</param>
public sealed record HandOverAnswer(bool Kept, string? Why, string Message);

/// <summary>
/// *Hand a conversation over* (MIXAGENT1a-i, D163, the mixed-agents design §2 and §5): a conversation goes on with another agent
/// when its own account's allowance runs low. This is the ask's judge: it refuses what cannot be handed over by a code before
/// anything is kept, and keeps the <see cref="HandOverBox"/> the shell's chat runner takes.
/// </summary>
/// <remarks>
/// <b>Conversations only.</b> A quest's session goes with MIXAGENT1a-ii, and says so in plain words. Ask Daoris's own
/// conversations and intakes never go on elsewhere (D137 §2.2), and the conversation cannot be handed to the agent and account it
/// runs on, nor while another hand-over waits, nor from a tree that is gone.
/// </remarks>
public static class HandOver
{
    /// <summary>A quest's session, which its own door hands over (MIXAGENT1a-ii).</summary>
    public const string NotConversation = "not-conversation";

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

        switch (WordsNever.Judge(record, WordsNever.LastHere(json, record.Quest)) ?? (WordsNever.IsHelp(record) ? WordsNever.Help : null))
        {
            case WordsNever.Teammate:
                return (Refused(WordsNever.Teammate, $"{id} ran on {id[..id.IndexOf('/')]}, where its conversation is; nothing said here reaches it."), null);
            case WordsNever.Help:
                return (Refused(WordsNever.Help,
                    $"{id} is Ask Daoris's own conversation, which stays on its own agent: Ask Daoris's history starts a new conversation from it."), null);
            case WordsNever.Intake:
                return (Refused(WordsNever.Intake, $"{id} is an intake; answer its ask #{record.Ask} instead: publish it or close it."), null);
        }

        if (record.Kind != "chat" || record.Quest is not null)
        {
            return (Refused(NotConversation, $"{id} is a quest's session: handing a quest's session over comes with MIXAGENT1a-ii, not yet."), null);
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

        if (prior?.Tree is not { Length: > 0 } tree || !Directory.Exists(tree))
        {
            return (Refused(TreeGone, $"the tree {id} ran in is gone, so there is nothing to go on in."), null);
        }

        return (null, prior);
    }

    private static HandOverAnswer Refused(string why, string message) => new(false, why, message);
}

/// <summary>
/// What the new conversation's agent is told first when a conversation is handed to it (MIXAGENT1a-i, D163 §3): Daoris's own
/// record of the old one, never its harness conversation, which another agent cannot read. A pure function of what was kept.
/// </summary>
public static class HandOverBriefing
{
    /// <summary>How much of the request or of the last words is kept, as a parked session's words are (<see cref="Driver.LastWordsLimit"/>).</summary>
    internal const int WordsLimit = 4000;

    /// <summary>The most plan steps or uncommitted paths named: a to-do list, not a log.</summary>
    internal const int ListLimit = 50;

    /// <summary>The briefing, in Daoris's one English; each part is left out where there is none.</summary>
    /// <param name="agent">The agent the conversation ran on.</param>
    /// <param name="request">The request as the person first wrote it, or null.</param>
    /// <param name="lastWords">The agent's last words, or null.</param>
    /// <param name="uncommitted">What the tree holds uncommitted, by relative path.</param>
    public static string Compose(
        string agent, string session, string why, string? request, IReadOnlyList<PlanEntry> plan, string? lastWords,
        IReadOnlyList<string> uncommitted)
    {
        var parts = new List<string>
        {
            $"You continue the {agent} conversation `{session}`, handed over because {HandOverWhy.Briefed(why)}. "
            + "None of its own context carries over to you: this is what Daoris kept of it.",
        };

        if (request?.Trim() is { Length: > 0 } asked)
        {
            parts.Add("The person first wrote:\n\n> " + Quoted(asked.Length <= WordsLimit ? asked : asked[..WordsLimit] + "…"));
        }

        if (plan.Count > 0)
        {
            var steps = plan.Take(ListLimit)
                .Select(step => $"    - {(step.Status is { Length: > 0 } status ? $"[{status}] " : "")}{step.Content.ReplaceLineEndings(" ")}");
            parts.Add("Its plan, as its record last kept it:\n\n" + string.Join("\n", steps) + More(plan.Count));
        }

        if (lastWords?.Trim() is { Length: > 0 } said)
        {
            parts.Add("Its last words were:\n\n> " + Quoted(said.Length <= WordsLimit ? said : "…" + said[^WordsLimit..].TrimStart()));
        }

        if (uncommitted.Count > 0)
        {
            parts.Add("Uncommitted in this tree:\n\n" + string.Join("\n", uncommitted.Take(ListLimit).Select(path => $"    - {path}")) + More(uncommitted.Count));
        }

        return string.Join("\n\n", parts);
    }

    private static string Quoted(string words) => words.ReplaceLineEndings("\n").Replace("\n", "\n> ");

    private static string More(int count) => count > ListLimit ? $"\n    … and {count - ListLimit} more" : "";
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
    /// <param name="start">Opens the new conversation in the old one's tree on the box's agent and account.</param>
    /// <param name="say">Hands the new conversation its first message; false where nothing took it.</param>
    public static async Task<HandedOver> RunAsync(
        ServiceClient service, string home, SessionEvents events, SessionOutput? output, string id, HandOverRequest ask,
        Func<string, IReadOnlyList<string>?> accountsOf, Func<string, Task> end,
        Func<PriorSession, HandOverRequest, CancellationToken, Task<ChatStart>> start, Func<string, ChatMessage, Task<bool>> say,
        CancellationToken ct = default)
    {
        var box = new HandOverBox(home);
        try
        {
            var (refused, prior) = await HandOver.JudgeAsync(service, accountsOf, id, ask.Agent, ask.Account, ct).ConfigureAwait(false);
            if (refused is not null) return new HandedOver(null, false, refused.Why, refused.Message);

            var from = prior!.Adapter ?? "its agent";
            var uncommitted = await UncommittedAsync(prior.Tree!, ct).ConfigureAwait(false);
            var (plan, words) = Driver.CarriedFrom(events, home, id);
            var briefing = HandOverBriefing.Compose(from, id, ask.Why, events.FirstAsked(id), plan, words, uncommitted);

            await end(id).ConfigureAwait(false);

            var started = await start(prior, ask, ct).ConfigureAwait(false);
            if (started.SessionId is not { } to)
            {
                return new HandedOver(
                    null, false, null,
                    $"{id} ended, but the conversation with {ask.Agent} did not start: {started.Message} Its tree is as it was.");
            }

            var taken = await say(to, new ChatMessage("", []) { Briefing = briefing, TakenOver = HandOverNotes.TakenOver(from, id, ask.Why) }).ConfigureAwait(false);

            // Where it went, where the person wrote it: the old conversation's last line, after its own end.
            var went = HandOverNotes.HandedOver(ask.Agent, to);
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

    /// <summary>What the tree holds uncommitted, as git says it; none where git cannot say.</summary>
    private static async Task<IReadOnlyList<string>> UncommittedAsync(string tree, CancellationToken ct)
    {
        try
        {
            return await WorkingTree.UncommittedAsync(tree, HandOverBriefing.ListLimit, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return [];
        }
    }
}

/// <summary>The two lines a hand-over writes into conversations' records (MIXAGENT1a-i), each by its code with facts only.</summary>
public static class HandOverNotes
{
    /// <summary>The old conversation's end: where it went. Never an account's name or a path (D125 §3.6).</summary>
    public static Noted HandedOver(string agent, string session) =>
        Noted.Of(
            NoteCodes.ChatHandedOver, $"The conversation went on with {agent}, in conversation {session}.",
            ("agent", agent), ("session", session));

    /// <summary>The new conversation's line where Daoris's briefing did not reach its agent.</summary>
    public static Noted Unsaid() =>
        Noted.Of(NoteCodes.ChatBriefingUnsaid, "What Daoris kept of the earlier conversation did not reach the agent: tell it where you were.");

    /// <summary>The new conversation's first line: where it came from and why.</summary>
    public static Noted TakenOver(string agent, string session, string why) =>
        Noted.Of(
            NoteCodes.ChatTakenOver, $"The conversation went on from {agent} conversation {session}, handed over because {HandOverWhy.Noted(why)}.",
            ("agent", agent), ("session", session), ("why", why));
}

/// <summary>What a conversation's hand-over came to (MIXAGENT1a-i): the new conversation, or why none, as a start-from's answer.</summary>
/// <param name="SessionId">The new conversation, where it was taken at once; null where the box waits or nothing was done.</param>
/// <param name="Kept">The ask stands: taken now (<paramref name="SessionId"/>) or kept for the conversation's turn to end.</param>
/// <param name="Why">Why nothing was done, by a code of <see cref="WordsNever"/> or <see cref="HandOver"/>; null otherwise.</param>
public sealed record HandedOver(string? SessionId, bool Kept, string? Why, string Message);
