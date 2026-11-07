namespace Daoris.Driver;

/// <summary>What the run settled of a record the person's words wait on, for the stages after it.</summary>
/// <param name="Start">The start as it goes on: with the limited record's kept word, or the record as it stands now when its words are handed on.</param>
/// <param name="FellBack">Why the record's own conversation could not go on and its words go to a new session; null where none is tried.</param>
/// <param name="AnswerKept">Whether a resume that is tried kept its answer, which the tree's hold must be taken again for.</param>
/// <param name="Written">The record the words wait on, as the new session is handed them.</param>
/// <param name="Switched">Whether the roster's switch moved the words off a cooling own account to a new part.</param>
internal sealed record GoOnFound(Consideration Start, ContinueReason? FellBack, bool AnswerKept, PriorSession? Written, bool Switched);
