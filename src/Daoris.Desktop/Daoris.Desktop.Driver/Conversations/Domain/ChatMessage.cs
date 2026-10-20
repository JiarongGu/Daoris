namespace Daoris.Driver;

/// <summary>A file a person attached to a message, with its bytes, as it arrived at a door.</summary>
/// <param name="Name">The name it was given, which is not yet safe to keep — <see cref="ChatFiles.SafeName"/> is.</param>
public sealed record ChatUpload(string Name, byte[] Content);

/// <summary>An attached file as kept for its session: the person's name for it, and where it lies.</summary>
public sealed record KeptFile(string Name, string Path);

/// <summary>One message of a conversation: the person's words and the files they attached (CONV4c).</summary>
public sealed record ChatMessage(string Text, IReadOnlyList<KeptFile> Files)
{
    /// <summary>The most a preface carries: a few lines of where the person is, never a document.</summary>
    public const int PrefaceLimit = 1200;

    /// <summary>
    /// Where the person is, handed to the agent ahead of their words (HELP1b) — what the screen already
    /// shows, said when it changed. Never the person's words: the record keeps it as what the agent was told.
    /// </summary>
    public string? Preface { get; init; }

    /// <summary>
    /// When the sender asked it to reach a working agent (COWORK1a, D188 point 1): urgent unless said, which is every path's reach
    /// from before (the person's default, D136; another session's claim at its next step until COWORK1d). The door gives what it
    /// can and says so; no level stops a turn.
    /// </summary>
    public MessageLevel Level { get; init; } = MessageLevel.Urgent;

    /// <summary>
    /// The level the record keeps beside the person's words while they wait (COWORK1a): spelled for the person's own, null for
    /// another session's claim, whose level its own row carries (COWORK1d), so nothing reads a claim's default as one stated.
    /// </summary>
    internal string? LevelSaid => Channel is { Person: false } || Helped is not null ? null : MessageLevels.Spelled(Level);

    /// <summary>
    /// The words' id in a driven session's record (STEER1, D136), stamped by its inbox: it pairs the words shown at once,
    /// while they wait, with the same words where the session took them. Null for a conversation's message. A word kept on the
    /// record's <c>said</c> (COWORK1c) carries the record's own id for it.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// Its producer's key (COWORK1c, the message-urgency design §6): one per press of the person's, so a press that arrives twice keeps
    /// one word. Daoris's words carry their fact's key on <see cref="Daoris"/>. Null for a word kept with none.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// Kept on the session's record before anything held it (COWORK1c), under <see cref="Id"/>: taken off it by that id once on the
    /// wire. False for words held in this process alone (a channel message, a word a service from before would not keep).
    /// </summary>
    internal bool Kept { get; init; }

    /// <summary>
    /// Marks this kept word attempted on its record (COWORK1c, the message-urgency design §6), set by its dispatcher as it hands the word
    /// to a door: the door awaits it before it writes the word, so a shell that comes up again never sends it twice. Null for a word
    /// kept nowhere, which needs none. Never faults.
    /// </summary>
    internal Func<Task>? Attempting { get; init; }

    /// <summary>
    /// Told once the door wrote this kept word to the wire, or settled it without the wire (a word of Daoris's dropped): its dispatcher
    /// takes it off the record by its id, after every take-off before it (COWORK1c). Null for a word kept nowhere.
    /// </summary>
    internal Action? Went { get; init; }

    /// <summary>
    /// The person's words a conversation goes on with (MSG1c, D137 §2.2), kept on its record while it had ended: its first
    /// turn, each word its own block on the protocol door and said again under its id as the door takes it. The record
    /// already shows them waiting, so the queue a page is told never lists them. Null for every other message.
    /// </summary>
    internal ResumeAsk? GoesOn { get; init; }

    /// <summary>
    /// What Daoris has handed the agent that it should know before the person speaks (BRWHINT1a): the browser it was given,
    /// said once with a new conversation's first message. A field of its own, never the preface — that is recorded as where
    /// the person is and bounded — and never the person's words, which the record keeps as written.
    /// </summary>
    public string? Briefing { get; init; }

    /// <summary>
    /// Daoris's brief of the line a handed-over conversation continues (SESSCTX1b, <see cref="ContextBrief"/>), handed first, ahead of
    /// what <see cref="Briefing"/> tells; the record keeps it as the conversation's first prompt with its account (the design §3.4).
    /// Null for every other message.
    /// </summary>
    internal ComposedBrief? Brief { get; init; }

    /// <summary>
    /// The repository's process a conversation in a tree of its own is told with its first message (PROCESS1d-ii, D164's amendment
    /// point 4), after the browser's line: a field of its own, so the record keeps it as its own note, never under the browser's.
    /// Null for every other message.
    /// </summary>
    public ProcessBriefing? Process { get; init; }

    /// <summary>
    /// The line a conversation handed over from another agent opens with (MIXAGENT1a-i, D163 §3): its first message is Daoris's
    /// briefing, never the person's words, so the record keeps this note in place of the person's message and of the browser's
    /// line. Null for every other message.
    /// </summary>
    internal Noted? TakenOver { get; init; }

    /// <summary>
    /// A helper's answer an open conversation says as its next turn (MIXAGENT2a-iii, D163): Daoris's words around another agent's
    /// claim, kept on the record as that helper's while it waits. Never the person's: recorded as Daoris's (<c>target</c>) under
    /// the word's id, never handed to a turn on the wire, never listed as queued, and taken off the record once on the wire
    /// (<see cref="Delivered"/>). Null for every other message.
    /// </summary>
    internal SaidWordView? Helped { get; init; }

    /// <summary>Told once this message's turn is on the wire: a helper's answer comes off the record then (MIXAGENT2a-iii).</summary>
    internal Action? Delivered { get; init; }

    /// <summary>
    /// The message on the ask's channel these words carry to a running part (SESSONE2b, D167 point 7): the person's, as their
    /// words; another session's, in Daoris's words around that agent's claim, which the record keeps as Daoris's, never the
    /// person's (<see cref="Words"/>). Null for every other message.
    /// </summary>
    public ChannelItemView? Channel { get; init; }

    /// <summary>
    /// Another agent's claim held only to ride a prompt the run sends anyway (COWORK1d, D188 point 4; the message-urgency design §4.2):
    /// an ordinary claim, or any claim on a door that takes words only at its turn's end. Alone at a turn's end it makes no turn and is
    /// let go for the entry's next part; beside a word that makes one, it goes with it, in the order said. Never listed as the person's.
    /// </summary>
    internal bool Rides { get; init; }

    /// <summary>
    /// Tells the entry the channel message these words carry (COWORK1d, the design §6) reached a state: <c>sent</c> once written,
    /// <c>taken</c> on the door's word, <c>released</c> when a part let its claim go unsent. Null for every other message. Never faults.
    /// </summary>
    internal Action<string>? Acknowledge { get; init; }

    /// <summary>
    /// Daoris's own words to a running session (COWORK1b, D188): a reopen told, a step's go-on, at urgent. The record keeps them as
    /// Daoris's, never the person's (<see cref="Words"/>); the page's queue never lists them, and the run judges them again by their
    /// key where they leave a hold for the wire (<see cref="DaorisWords.GoesAsync"/>). Null for every other message.
    /// </summary>
    public DaorisWord? Daoris { get; init; }

    /// <summary>
    /// Whether the record already shows this message waiting, so the queue a page is told never lists it and a stop never hands
    /// it back to the person's composer: the words a conversation goes on with, and a helper's answer.
    /// </summary>
    internal bool OnRecord => GoesOn is not null || Helped is not null || Said;

    /// <summary>
    /// Words the record already shows as said under <see cref="Id"/> and what was told beside them, handed back to be the next
    /// turn because the agent answered their steer <c>promptRequired</c> (STEER2d): that turn records them only as taken under
    /// the same id, once, and never says them or what was told again.
    /// </summary>
    internal bool Said { get; init; }

    /// <summary>What the agent is handed: the brief, the briefing, the process, the preface, then the person's words, each after a blank line.</summary>
    public string Prompt => string.Join(
        "\n\n", new[] { Brief?.Text, Briefing, Process?.Words, Preface }.Where(part => !string.IsNullOrEmpty(part)).Append(Text));

    /// <summary>A preface as a message carries it: trimmed, bounded, and null when there is none.</summary>
    public static string? Bound(string? preface) =>
        preface?.Trim() is { Length: > 0 } said ? said.Length > PrefaceLimit ? said[..PrefaceLimit] : said : null;

    /// <summary>
    /// What the person told a driven session, as its record keeps it (SESS3, STEER1): theirs, under the id that pairs the
    /// words shown while they wait with the same words where the session took them, and the names of what they attached.
    /// The message's own, so a conversation records it as the loop does without naming the loop (LOOPOUT1). Another session's
    /// message on the ask's channel is Daoris's words around that agent's claim (SESSONE2b), never the person's, and Daoris's own
    /// words are Daoris's (COWORK1b).
    /// </summary>
    internal static SessionEvent Words(ChatMessage message) => new()
    {
        Kind = SessionEventKind.User,
        // COWORK1d: a helper's answer taken at a step of a turn is Daoris's words around that helper's claim too.
        Origin = message.Channel is { Person: false } || message.Daoris is not null || message.Helped is not null ? "target" : "person",
        From = message.Daoris is not null ? DaorisWords.From : null,
        // SESSCTX1b: the channel's message these words carry, marked by its id, so a reader never tells it by Daoris's English.
        Channel = message.Channel?.Id is { Length: > 0 } channel ? channel : null,
        Id = message.Id,
        Text = message.Text,
        Files = message.Files.Count > 0 ? [.. message.Files.Select(kept => kept.Name)] : null,
    };
}

/// <summary>
/// The process a conversation is told at its start (PROCESS1d-ii): the words, <see cref="ProcessWords.Conversation"/>'s one-copy
/// English (D142 point 6), and the facts its note carries, the commit rule's mode and where it was set.
/// </summary>
public sealed record ProcessBriefing(string Words, string Mode, string Source);
