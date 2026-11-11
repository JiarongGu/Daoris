namespace Daoris.Driver;

/// <summary>
/// What a turn's message puts in a conversation's record, and what the agent is told beside it: the
/// record's own lines, which the chat runner's door and the course's moves both write. Course's, the lowest subdomain that reads them.
/// </summary>
internal static class ChatRecorded
{
    /// <summary>
    /// The person's message as the record keeps it: their words and the names of what they
    /// attached — never the kept paths, nor the lines or links a door added to reach them.
    /// </summary>
    /// <summary>
    /// What a turn's message puts in the record as it is sent, in order: the browser, the process and where the
    /// person is as their notes, then the person's message. A conversation handed over from another agent opens with Daoris's
    /// briefing instead: its one note says where it came from, since no person spoke.
    /// </summary>
    /// <param name="shown">
    /// What the message shows of the words a keyed open moved to its part: one event per moved word under its
    /// id, recorded in place of the one joined event its words are otherwise; null for every other message.
    /// </param>
    internal static IEnumerable<SessionEvent> Recorded(ChatMessage message, IReadOnlyList<SessionEvent>? shown = null)
    {
        if (message.TakenOver is { } taken)
        {
            yield return new SessionEvent { Kind = SessionEventKind.Note, Text = taken.Note, Parts = taken.Parts };
            if (Briefed(message) is { } briefed) yield return briefed;

            // A part that goes on from a lost conversation is handed the person's
            // words that waited there after the brief, in the same prompt; they are the person's, as they said them.
            // The words a keyed open moved are each shown under its id, so each word shown waiting pairs with where it is asked.
            // The standing block a hand-over's first prompt carries, noted beside the brief it rides with.
            if (ToldBlock(message.Block) is { } blocked) yield return blocked;
            if (shown is not null)
            {
                foreach (var each in shown) yield return each;
            }
            else if (message.Text.Length > 0)
            {
                yield return Asked(message);
            }

            yield break;
        }

        if (message.Helped is { } helped)
        {
            // Daoris's words around another agent's claim, under the id the record gave them so they pair with the
            // event that showed them waiting; never the person's, so no preface and no browser line ride beside them.
            // Marked as that helper's, so the answers-alone bound counts the turn it prompts.
            // A second opinion's findings by the opinion they carry, as a resumed run's are.
            yield return new SessionEvent
            {
                Kind = SessionEventKind.User, Origin = "target", Helper = helped.Helper, Opinion = helped.By, Id = helped.Id, Text = message.Text,
            };
            yield break;
        }

        if (message.Said)
        {
            // Handed back after a steer found no turn running: said and told already, taken here.
            yield return ChatMessage.Words(message);
            yield break;
        }

        if (message.Daoris is not null)
        {
            // Daoris's own turn to a conversation, the closing note a hand-over asks of its outgoing agent: Daoris's on
            // the record, never the person's, so no brief reads it as their word.
            yield return ChatMessage.Words(message);
            yield break;
        }

        // An Ask Daoris conversation started from an earlier one is handed that line's brief ahead of the person's first words.
        if (Briefed(message) is { } brief) yield return brief;
        if (ToldBrowser(message) is { } toldBrowser) yield return toldBrowser;
        if (ToldPlace(message) is { } toldPlace) yield return toldPlace;
        if (ToldProcess(message) is { } toldProcess) yield return toldProcess;
        if (ToldBlock(message.Block) is { } toldBlock) yield return toldBlock;
        if (Told(message) is { } told) yield return told;
        if (ToldDraft(message) is { } toldDraft) yield return toldDraft;
        if (shown is null)
        {
            yield return Asked(message);
            yield break;
        }

        // A first message of moved words (start-from's, a fresh start's) shows each under its id.
        foreach (var each in shown) yield return each;
    }

    /// <summary>
    /// The brief a message carries, kept as the conversation's first prompt, Daoris's, with its
    /// account and the session it continues, as an instruction is kept. Never a message of the person's, nor the browser's line.
    /// </summary>
    private static SessionEvent? Briefed(ChatMessage message) => message.Brief is { } brief
        ? new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = brief.Text, Account = brief.Account, Continues = brief.Continues }
        : null;

    private static SessionEvent Asked(ChatMessage message) => new()
    {
        Kind = SessionEventKind.User,
        Origin = "person",
        Text = message.Text,
        Files = message.Files.Count > 0 ? [.. message.Files.Select(file => file.Name)] : null,
        // Words the person's *Send now* stopped the turn before for: the page says they began this turn.
        Level = message.StoppedFor ? MessageLevels.Now : null,
    };

    /// <summary>
    /// What the agent is told beside the person's words, as the record keeps it: a note, never
    /// the person's, so a reader later sees what the agent knew and who said what.
    /// </summary>
    internal static SessionEvent? Told(ChatMessage message)
    {
        if (message.Preface is not { Length: > 0 } preface) return null;
        // The lead by its code; the preface is Daoris's one-copy English to the agent, shown as written.
        var told = Noted.Of(ChatNoteCodes.ChatToldWhere, "told where the person is:").Then(" ", Noted.Said(preface, NoteBy.Program));
        return new SessionEvent { Kind = SessionEventKind.Note, Text = told.Note, Parts = told.Parts };
    }

    /// <summary>
    /// The workflow draft the agent is told beside the person's words, as the record keeps it: a note of its own, its lead by
    /// code, then what the agent is handed as the program's words, the draft's JSON last and whole, so the draft door finds it by its
    /// fingerprint (<see cref="WorkflowDraftTold.Told"/>).
    /// </summary>
    internal static SessionEvent? ToldDraft(ChatMessage message) => message.Draft?.ToldNote();

    /// <summary>
    /// The browser the agent is handed and told of beside the person's first words, as the record keeps it: a note of
    /// its own, so the person sees what Daoris told the agent. The line is Daoris's one-copy English, shown as written.
    /// </summary>
    internal static SessionEvent? ToldBrowser(ChatMessage message)
    {
        if (message.Briefing is not { Length: > 0 } line) return null;
        var told = Noted.Of(ChatNoteCodes.ChatToldBrowser, "told the agent:").Then(" ", Noted.Said(line, NoteBy.Program));
        return new SessionEvent { Kind = SessionEventKind.Note, Text = told.Note, Parts = told.Parts };
    }

    /// <summary>What a conversation working in place is told of its place, as the record keeps it: a note of its own under its own lead.</summary>
    internal static SessionEvent? ToldPlace(ChatMessage message)
    {
        if (message.Place is not { Length: > 0 } place) return null;
        var told = Noted.Of(ChatNoteCodes.ChatToldPlace, "told the agent its place:").Then(" ", Noted.Said(place, NoteBy.Program));
        return new SessionEvent { Kind = SessionEventKind.Note, Text = told.Note, Parts = told.Parts };
    }

    /// <summary>
    /// The standing block the agent is told with its first message, as the record keeps it: a note of its own under its own
    /// lead, the block Daoris's one-copy English, shown as written. Null where none is told.
    /// </summary>
    internal static SessionEvent? ToldBlock(StandingBlockTold? block)
    {
        if (block?.Note() is not { } told) return null;
        return new SessionEvent { Kind = SessionEventKind.Note, Text = told.Note, Parts = told.Parts };
    }

    /// <summary>
    /// The repository's process the agent is told with the person's first words, as the record keeps it: a note of
    /// its own under its own lead, the commit rule's mode and where it is set as facts, the words Daoris's one-copy English.
    /// </summary>
    internal static SessionEvent? ToldProcess(ChatMessage message)
    {
        if (message.Process is not { Words.Length: > 0 } process) return null;
        var set = process.Source == CommitRules.DefaultSource ? "by default" : $"set for its {process.Source}";
        var told = Noted.Of(
                ChatNoteCodes.ChatToldProcess, $"told the agent this repository's process, its commit rule `{process.Mode}` {set}:",
                ("mode", process.Mode), ("source", process.Source))
            .Then(" ", Noted.Said(process.Words, NoteBy.Program));
        return new SessionEvent { Kind = SessionEventKind.Note, Text = told.Note, Parts = told.Parts };
    }
}
