using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// One word as it goes to the say door: its text, the names of its files, the door it was said at, and the answer of its park's
/// question it was pressed as (PARKVIEW1), which the conversation shows it under; null for every other word. FOUNDATION1b: it
/// keeps its producer's key and the level it was said at wherever it is held, in the held file too, so the record keeps one word
/// for a producer's retry and a forward says it as the first say did; a word held before has no key, and the default level.
/// COMMS1e: a press keeps the id of the question it answered, so a forward is checked against the head's question.
/// </summary>
internal sealed record HeldSay(
    string Text, IReadOnlyList<string> Files, string Door, string? Choice = null, string? Key = null,
    MessageLevel Level = MessageLevel.Urgent, string? Question = null);
