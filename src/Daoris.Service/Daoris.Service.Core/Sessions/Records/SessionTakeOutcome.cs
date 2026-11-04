namespace Daoris.Knowledge;

/// <param name="Refusal"><see cref="SessionSayRefusal.None"/> when the words were taken off the record.</param>
/// <param name="Message">The whole answer, phrased once here.</param>
/// <param name="Session">The session as it now stands, when they were.</param>
/// <param name="Taken">The words taken, in the order they were said: those named that the record held.</param>
public sealed record SessionTakeOutcome(SessionSayRefusal Refusal, string Message, Session? Session, IReadOnlyList<SaidWord> Taken);
