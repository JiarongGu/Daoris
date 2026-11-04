namespace Daoris.Knowledge;

/// <summary>
/// One thing said to a session, waiting for it to go on with it (MSG1a, D137 §2.4): the person's, or, naming
/// <see cref="By"/>, another agent's claims Daoris handed it (XAGENT1c, D155 point 7).
/// </summary>
/// <param name="Id">The word's own handle, so the record can say again where the session took it (D136 §3).</param>
/// <param name="Text">Their words, trimmed at the ends only.</param>
/// <param name="At">When they said it.</param>
/// <param name="Files">The names of the files they gave with it — names alone, never where the files are.</param>
/// <param name="Reopens">
/// Said after the record ended, so it goes on by reopening it; such a word of the person's is kept on its ask once taken,
/// as <c>reopened</c> (D133 §1). False for an answer to a park, which its door keeps on the ask at once.
/// </param>
/// <param name="By">
/// The second opinion whose findings these are, or null for the person's words: so absent is the person's, and every word
/// kept before the field reads as it did. Another agent's claims are never kept on the ask, where every later session would
/// read them as the person's requirements (DRIFT1a).
/// </param>
/// <param name="Helper">
/// The helper session whose answer this is (MIXAGENT2a-i, D163 point 4), or null: a helper's answer is another agent's claim,
/// and never the person's. Its own field, because every reader takes <paramref name="By"/> as an opinion's id; a record from
/// before reads as it did.
/// </param>
/// <param name="Channel">
/// The channel message these words came from (SESSONE2e, D167), or null: the channel keeps that message on the ask as its
/// word kind <c>channel</c>, so the ask is not told of these words a second time. A record from before reads as null.
/// Never whose words they are: the person's own channel message is kept with it, and is theirs.
/// </param>
/// <param name="From">
/// Its author where that is not the person (COWORK1b, D188; the message-urgency design §9): <see cref="SaidFrom.Daoris"/> for Daoris's
/// own words, a reopen or a step's go-on told to a session. Null is the person's, or another agent's named by <paramref name="By"/>
/// or <paramref name="Helper"/>, so a word kept before the field reads as it did.
/// </param>
/// <param name="Key">
/// Its producer's key (COWORK1b, the design §6): the say door keeps one word per key on a record, so a producer's retry is answered
/// with the word kept. Null for a word kept with none, each its own, as every word before the field was.
/// </param>
/// <param name="Level">
/// When its sender asked it to reach a working session (COWORK1c, D188 point 1; the message-urgency design §9), one of
/// <see cref="SaidLevel"/>. Null for a word kept with none, which reads as its old reach: the person's as urgent, any other as ordinary.
/// </param>
/// <param name="Attempt">
/// When a dispatcher first wrote it to a harness (COWORK1c, the design §6), marked before the wire by
/// <see cref="SessionStore.MarkAttemptAsync"/>: a word with an attempt and not taken off when a shell comes up may have reached the agent,
/// and is never sent again. Null for a word never attempted, and every word kept before the field.
/// </param>
/// <param name="Decision">
/// The supervisor's decision a <see cref="SaidFrom.Supervisor"/> word claims (SUPERVISOR1a-i, the supervisor design §6.1), or null.
/// A claim on the record, never an authority: only the driver's memory says which word its release wrote.
/// </param>
/// <param name="Question">The recorded question that decision answers (SUPERVISOR1a-i), or null.</param>
/// <param name="Choice">The offered label the decision chose (SUPERVISOR1a-i), from which the service composed the text, or null.</param>
public sealed record SaidWord(
    string Id, string Text, DateTimeOffset At, IReadOnlyList<string> Files, bool Reopens = false, string? By = null, string? Helper = null,
    string? Channel = null, string? From = null, string? Key = null, string? Level = null, DateTimeOffset? Attempt = null,
    string? Decision = null, string? Question = null, string? Choice = null)
{
    /// <summary>Whether the person said it: every word but another agent's, a reviewer's, a helper's, a partner's, Daoris's or the supervisor's.</summary>
    public bool Persons => Senders.Of(this) == Senders.Person;

    /// <summary>Whether it claims the supervisor (SUPERVISOR1a-i): a claim only, which the driver attributes or quarantines.</summary>
    public bool Supervisors => From == SaidFrom.Supervisor;
}
