namespace Daoris.Driver;

/// <summary>What a start adds to its session's target beyond the quest: the person's words, the work's language and its access setup's go-aheads.</summary>
internal static class StartTargets
{
    /// <summary>
    /// A quest's target with the person's words on the ask its session's line is on, read for this start: every start on the ask is
    /// handed them, since a carry-on's own record holds only the session before it. A line on no ask is handed none, and words the
    /// service did not answer leave today's instruction, saying so (<see cref="AskWords.ReadAsync"/>).
    /// </summary>
    public static async Task<SessionTarget> WithAskWordsAsync(SessionTarget target, string session, ServiceClient service, CancellationToken ct) =>
        target with { Words = await AskWords.ReadOfSessionAsync(service, session, ct).ConfigureAwait(false) };

    /// <summary>The work's session language for the quest's repository, from the config this tick holds: the repository's, else its workspace's, else none, and never the window's.</summary>
    public static SessionTarget WithLanguage(SessionTarget target, DriverConfig config, string? workspace) =>
        target with { Language = SessionLanguages.Resolve(config, target.Repository, workspace) };

    /// <summary>The go-aheads its repository's access setup asks for, from the setup this start resolved once: under <c>never</c>, the instruction asks only what the person's words demand.</summary>
    public static SessionTarget WithAccess(SessionTarget target, ResolvedAccess access) =>
        target with { NoGoAheads = access.Value(AccessRules.GoAheads) == AccessRules.Never };

    /// <summary>
    /// Why the graph named for a session's take did not bind it, or null: a line's extra word is never worth failing the end of a
    /// run for, so a service that did not answer says nothing more than the line already did.
    /// </summary>
    public static async Task<string?> TookBindOrNullAsync(ServiceClient service, string sessionId, CancellationToken ct)
    {
        try
        {
            return await service.TookBindAsync(sessionId, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException or IOException)
        {
            return null;
        }
    }
}
