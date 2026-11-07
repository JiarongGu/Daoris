namespace Daoris.Driver;

/// <summary>
/// How a protocol door's session begins, written once for a driven run and a conversation alike: a session that goes on
/// goes on in its own conversation, resumed by the id kept for it, never a new session, which would be a new conversation under the same
/// record. A resume the agent refuses is <see cref="AcpResumeRefused"/>, which each caller reads as its own shape says.
/// </summary>
internal static class ProtocolOpen
{
    /// <summary>Open a new session where no conversation is named, resume the named one otherwise.</summary>
    public static Task BeginAsync(string? conversation, Func<Task> open, Func<string, Task> resume) =>
        conversation is null ? open() : resume(conversation);

    /// <summary>The same for a session the run holds.</summary>
    public static Task BeginAsync(AcpSession session, string cwd, string? conversation, CancellationToken ct, ConnectorPlan? plan) =>
        BeginAsync(conversation, () => session.OpenAsync(cwd, ct, plan), named => session.ResumeAsync(cwd, named, ct, plan));
}
