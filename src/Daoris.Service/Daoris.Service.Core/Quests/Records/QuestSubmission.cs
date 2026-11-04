namespace Daoris.Knowledge;

/// <summary>
/// A taker's submission in a closure graph (FABRIC-CLOSE1a, the fabric events design §E5): the operation that made it, by the
/// machine and sequence that name it everywhere, which the graph's close names as the one it judged; when; and its note. Its
/// answers are the quest's <see cref="Quest.Answers"/>.
/// </summary>
/// <param name="Session">
/// The session that said its <c>ready</c> (FABRIC-CLOSE1c4), as its connector's intake named it: the one to tell when the person
/// reopens it. Null for a <c>ready</c> no session said, through a door that names none, and for every submission from before.
/// </param>
public sealed record QuestSubmission(string Machine, long Sequence, DateTimeOffset At, string? Note, string? Session = null);
