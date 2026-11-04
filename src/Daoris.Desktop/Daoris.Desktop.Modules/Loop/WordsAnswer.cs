using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// What a person's words to a session do, and what a word said now would do (MSG1d, D137 §2, §5.3): the one answer
/// <c>SESSION_INPUT</c> and <c>SESSION_QUEUE</c> give, for every state.
/// </summary>
/// <param name="Sent">The words are held or taken: they reach the session, now or when it can.</param>
/// <param name="Reaches">
/// When they reach it: <c>next-step</c>, <c>turn-end</c> or <c>resume</c>. Null where they are taken at once (a chat between
/// turns), where the door is not known yet (a driven session still opening), and where nothing takes them.
/// </param>
/// <param name="Why">
/// Where nothing takes them, why, by a code the page words itself: <c>teammate</c>, <c>intake</c>, <c>stood-down</c>,
/// <c>help</c>, <c>superseded</c>, <c>continued</c>, <c>not-found</c> or <c>no-words</c>. Null where they are taken or held,
/// except where the person's *Send now* was not carried out (STEER2a): then its <see cref="SendNowWhy"/> code, beside
/// <c>sent: true</c>.
/// </param>
public sealed record WordsAnswer(bool Sent, string? Reaches, string? Why)
{
    /// <summary>
    /// The person's *Send now* was carried out (STEER2a, D136's 2026-10-16 note): steered into the running turn (reach
    /// <c>now</c>), held with the turn stopped so they go next (<c>turn-end</c>), or taken at once because no turn ran. False
    /// where it was not asked, and where the door could do neither, with <see cref="Why"/> saying why.
    /// </summary>
    public bool Now { get; init; }

    /// <summary>
    /// Where <see cref="Why"/> is <c>continued</c> (HANDWORDS1), the conversation it went on in, the latest that continues it: where
    /// the words go instead. Null otherwise.
    /// </summary>
    public string? To { get; init; }

    /// <summary>Whether the words reached a running session's own door, which keeps them on its ask only as it takes them.</summary>
    public bool Running { get; init; }

    /// <summary>
    /// The id the record gave words it kept, which its events say again where the session took them: what a terminal's say
    /// follows to tell whether the same session took them (MSG1e2). Null where nothing kept them yet.
    /// </summary>
    public string? Word { get; init; }

    /// <summary>The service's sentence beside its refusal, which a terminal prints for a code it does not word itself.</summary>
    public string? Message { get; init; }

    /// <summary>
    /// The line's head that took the words, where the record they were said to went on in it (FOUNDATION1d-ii, the foundation design §3.4 rule 2):
    /// the shell said them there. Null where the record named took them. The shell's own: the page's answer names <see cref="To"/> alone.
    /// </summary>
    public string? At { get; init; }

    /// <summary>
    /// What a running session's door can do with a word while a turn runs (COWORK1a): whether its agent steers, and whether it has
    /// the keeping stop. Null where nothing here runs it, its door is not known yet, or it carries only text.
    /// </summary>
    public TurnDoor? Door { get; init; }

    /// <summary>
    /// The person's words waiting on its record (SAIDBACK1b, D137 §2.4), where the record was read: what the queue lists when
    /// nothing here holds the session, so the composer's *Edit* and *Remove* reach them. Null where the record was not read (a
    /// session running here, or nothing known); empty on a record that runs again, whose run is taking them.
    /// </summary>
    public IReadOnlyList<WaitingWord>? Waiting { get; init; }

    /// <summary>Nothing takes them, for this reason.</summary>
    public static WordsAnswer Refused(string why, string? message = null) => new(false, null, why) { Message = message };
}

/// <summary>
/// One of the person's words waiting on a record (SAIDBACK1b, D137 §2.4), as the queue lists it: the id the record gave it, which
/// a take-back names it by, its words and its files' names.
/// </summary>
public sealed record WaitingWord(string Id, string Text, IReadOnlyList<string> Files);
