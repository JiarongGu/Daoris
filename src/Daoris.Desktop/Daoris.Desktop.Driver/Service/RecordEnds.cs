using Daoris.Contracts.Sessions;

namespace Daoris.Driver;

/// <summary>One end of a record's turns as the strikes judge it: the state it ended in, its limit flag, a stop not the person's, and its note's codes.</summary>
internal sealed record RecordEnd(string State, bool Limit, bool Interrupted, IReadOnlyList<string> Codes);

/// <summary>
/// The ends of a driven record and the one judge of each: a record that went on keeps each earlier turn's end on that turn (the go-on forgives
/// the record's own flags), and its own state is its last turn's, so the strikes and the failures in a row read every end and a go-on never
/// erases a failed one.
/// </summary>
internal static class RecordEnds
{
    /// <summary>
    /// The record's ends, oldest first: each of its own turns' kept ends, then its own. A turn of another part, which every record of a line
    /// answers, is that part's.
    /// </summary>
    public static IReadOnlyList<RecordEnd> Of(SessionResponse session) =>
    [
        .. (session.Turns ?? []).Where(turn => turn.End is not null && string.Equals(turn.Part, session.Id, StringComparison.Ordinal))
            .OrderBy(turn => turn.Number)
            .Select(turn => new RecordEnd(turn.End!.State, turn.End.Limit, turn.End.Interrupted, turn.End.Codes ?? [])),
        new RecordEnd(
            session.State ?? "", session.Limit, session.Interrupted,
            session.NoteParts is { } parts && NotePart.ReadParts(parts) is { } read ? [.. read.Select(part => part.Code).OfType<string>()] : []),
    ];

    /// <summary>
    /// An end that is another count's or no end of the work: a limit, a refused account, a choice not offered, a transient end, an update's
    /// close, or the quiet end (the <c>quiet</c> rule's own). It is no failure and breaks no run of them.
    /// </summary>
    public static bool IsNeutral(RecordEnd end) =>
        end.Limit
        || end.Codes.Any(code => RecordNotes.AccountsOwn.Contains(code)
                                 || code is RecordNotes.EndedChoiceNotOffered or RecordNotes.EndedTransient or RecordNotes.UpdateClosed or RecordNotes.EndedQuiet);

    /// <summary>The one definition of a failure, for the quest's count and its run on one choice: a <c>failed</c> end or a cut-off, never a neutral one.</summary>
    public static bool IsFailure(RecordEnd end) =>
        !IsNeutral(end)
        && (Is(end, "failed") || (Is(end, "stopped") && end.Interrupted));

    public static bool Is(RecordEnd end, string state) => string.Equals(end.State, state, StringComparison.OrdinalIgnoreCase);
}
