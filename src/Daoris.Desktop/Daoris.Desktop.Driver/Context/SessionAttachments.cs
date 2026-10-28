namespace Daoris.Driver;

/// <summary>
/// Where a session's attached files lie, the brief's own reading of them (<see cref="ContextBriefs"/>): the folder under the home a
/// person's attachments are kept in, and the one its agent may read. <see cref="ChatFiles"/> keeps the files themselves and answers
/// <see cref="ChatFiles.Folder"/> through this, so a brief and the composer name one place.
/// </summary>
internal static class SessionAttachments
{
    /// <summary>The folder a session's attached files are kept in, and the one its agent may read.</summary>
    /// <exception cref="DriverException">The id names no session of this machine.</exception>
    internal static string Folder(string home, string sessionId) =>
        SessionEventText.IsLocalId(sessionId)
            ? DaorisHome.Entry(home, HomeEntry.SessionFiles, sessionId)
            : throw new DriverException($"`{sessionId}` is not a session id, so it names no folder.");
}
