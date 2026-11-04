namespace Daoris.Knowledge;

/// <param name="Refusal"><see cref="SessionOpenRefusal.None"/> when a session was queued.</param>
/// <param name="Message">The full answer, phrased once here so no two doors can drift apart on it.</param>
/// <param name="Session">The queued session, when there is one.</param>
public sealed record SessionOpenOutcome(SessionOpenRefusal Refusal, string Message, Session? Session)
{
    public string? Opens { get; init; }
    public IReadOnlyList<OpenTransfer> Transfer { get; init; } = [];
    public string? Head { get; init; }
}
