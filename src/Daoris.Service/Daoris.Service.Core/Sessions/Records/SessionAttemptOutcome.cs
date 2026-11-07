namespace Daoris.Knowledge;

/// <summary>What marking words attempted did: the record as it now stands, and the words named that no longer wait.</summary>
public sealed record SessionAttemptOutcome(SessionSayRefusal Refusal, string Message, Session? Session, IReadOnlyList<string> Unmarked);
