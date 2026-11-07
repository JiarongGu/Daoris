using Daoris.Contracts.Sessions;

namespace Daoris.Contracts.Participants;

/// <summary>
/// A helper or partner door's answer: the desk's sentence, the record a pass opened or the session the answer went to, and the word.
/// <c>Waiting</c>: the answer is waiting already, <c>Session</c> holds it and <c>Said</c> is it, and nothing is kept now.
/// </summary>
public sealed record HelperActionResponse(
    string Message, SessionResponse? Session = null, SaidWordResponse? Said = null, string? Settled = null, bool? Waiting = null);
