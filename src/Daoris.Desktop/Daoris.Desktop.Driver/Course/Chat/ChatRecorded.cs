namespace Daoris.Driver;

/// <summary>
/// What a turn's message puts in a conversation's record, and what the agent was told beside it (CONV4c, HELP1b, BRWHINT1a, PROCESS1d-ii): the
/// record's own lines, which the chat runner's door and the course's moves both write. Course's, the lowest subdomain that reads them.
/// </summary>
internal static class ChatRecorded
{
    /// <summary>
    /// The person's message as the record keeps it (CONV4c): their words and the names of what they
    /// attached — never the kept paths, nor the lines or links a door added to reach them.
    /// </summary>
    /// <summary>
    /// What a turn's message puts in the record as it is sent, in order: the browser, the process (PROCESS1d-ii) and where the
    /// person is as their notes, then the person's message. A conversation handed over from another agent opens with Daoris's
    /// briefing instead (MIXAGENT1a-i): its one note says where it came from, since no person spoke.
    /// </summary>
    internal static IEnumerable<SessionEvent> Recorded(ChatMessage message)
    {
        if (message.TakenOver is { } taken)
        {
            yield return new SessionEvent { Kind = SessionEventKind.Note, Text = taken.Note, Parts = taken.Parts };
            // SESSCTX1b (the session-context design §3.4): the brief is the conversation's first prompt, Daoris's, with its account and
            // the session it continues, as an instruction is kept. Never a message of the person's, nor the browser's line.
            if (message.Brief is { } brief)
            {
                yield return new SessionEvent
                {
                    Kind = SessionEventKind.User, Origin = "target", Text = brief.Text, Account = brief.Account, Continues = brief.Continues,
                };
            }

            // SESSCTX1c part B (the session-context design §2): a part that goes on from a lost conversation is handed the person's
            // words that waited there after the brief, in the same prompt; they are the person's, as they said them.
            if (message.Text.Length > 0) yield return Asked(message);
            yield break;
        }

        if (message.Helped is { } helped)
        {
            // Daoris's words around another agent's claim (MIXAGENT2a-iii), under the id the record gave them so they pair with the
            // event that showed them waiting; never the person's, so no preface and no browser line ride beside them.
            // COWORK1e: marked as that helper's, so the answers-alone bound counts the turn it prompts.
            // CHATFLOW1e: a second opinion's findings by the opinion they carry, as a resumed run's are.
            yield return new SessionEvent
            {
                Kind = SessionEventKind.User, Origin = "target", Helper = helped.Helper, Opinion = helped.By, Id = helped.Id, Text = message.Text,
            };
            yield break;
        }

        if (message.Said)
        {
            // Handed back after a steer found no turn running (STEER2d): said and told already, taken here.
            yield return ChatMessage.Words(message);
            yield break;
        }

        if (message.Daoris is not null)
        {
            // SESSCTX1c part B: Daoris's own turn to a conversation, the closing note a hand-over asks of its outgoing agent: Daoris's on
            // the record, never the person's, so no brief reads it as their word.
            yield return ChatMessage.Words(message);
            yield break;
        }

        if (ToldBrowser(message) is { } toldBrowser) yield return toldBrowser;
        if (ToldProcess(message) is { } toldProcess) yield return toldProcess;
        if (Told(message) is { } told) yield return told;
        yield return Asked(message);
    }

    private static SessionEvent Asked(ChatMessage message) => new()
    {
        Kind = SessionEventKind.User,
        Origin = "person",
        Text = message.Text,
        Files = message.Files.Count > 0 ? [.. message.Files.Select(file => file.Name)] : null,
    };

    /// <summary>
    /// What the agent was told beside the person's words (HELP1b), as the record keeps it: a note, never
    /// the person's, so a reader later sees what the agent knew and who said what.
    /// </summary>
    internal static SessionEvent? Told(ChatMessage message)
    {
        if (message.Preface is not { Length: > 0 } preface) return null;
        // CONVNOTE2c: the lead by its code; the preface is Daoris's one-copy English to the agent (D142 point 6), shown as written.
        var told = Noted.Of(NoteCodes.ChatToldWhere, "told where the person is:").Then(" ", Noted.Said(preface, NoteBy.Program));
        return new SessionEvent { Kind = SessionEventKind.Note, Text = told.Note, Parts = told.Parts };
    }

    /// <summary>
    /// The browser the agent was handed and told of beside the person's first words (BRWHINT1a), as the record keeps it: a note of
    /// its own, so the person sees what Daoris told the agent. The line is Daoris's one-copy English (D142 point 6), shown as written.
    /// </summary>
    internal static SessionEvent? ToldBrowser(ChatMessage message)
    {
        if (message.Briefing is not { Length: > 0 } line) return null;
        var told = Noted.Of(NoteCodes.ChatToldBrowser, "told the agent:").Then(" ", Noted.Said(line, NoteBy.Program));
        return new SessionEvent { Kind = SessionEventKind.Note, Text = told.Note, Parts = told.Parts };
    }

    /// <summary>
    /// The repository's process the agent was told with the person's first words (PROCESS1d-ii), as the record keeps it: a note of
    /// its own under its own lead, the commit rule's mode and where it was set as facts, the words Daoris's one-copy English.
    /// </summary>
    internal static SessionEvent? ToldProcess(ChatMessage message)
    {
        if (message.Process is not { Words.Length: > 0 } process) return null;
        var set = process.Source == CommitRules.DefaultSource ? "by default" : $"set for its {process.Source}";
        var told = Noted.Of(
                NoteCodes.ChatToldProcess, $"told the agent this repository's process, its commit rule `{process.Mode}` {set}:",
                ("mode", process.Mode), ("source", process.Source))
            .Then(" ", Noted.Said(process.Words, NoteBy.Program));
        return new SessionEvent { Kind = SessionEventKind.Note, Text = told.Note, Parts = told.Parts };
    }
}
