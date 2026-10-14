namespace Daoris.Driver;

/// <summary>
/// *Start a conversation on its first words* (NEW1a, D162 point 2): the conversation opens as <c>START_CHAT</c> always opened
/// one, and is handed the person's words as its first message. Files over a message's limit go as the words alone, and there
/// is no preface: nothing was said anywhere before. "Sent" means the door took the words (queued or taken), as
/// <see cref="StartFrom"/>'s does; a conversation that opened without taking them is answered as such and the driver keeps
/// nothing, since the page keeps the person's draft.
/// </summary>
public static class StartWith
{
    /// <summary>Open the conversation, then hand it the words.</summary>
    /// <param name="start">Opens a conversation: the chat runner's own start.</param>
    /// <param name="say">Hands a conversation its first message: false where nothing took it; throws <see cref="DriverException"/> where the files are too many.</param>
    public static async Task<StartedFrom> RunAsync(
        Func<CancellationToken, Task<ChatStart>> start, Func<string, string, IReadOnlyList<ChatUpload>, string?, bool> say,
        string text, IReadOnlyList<ChatUpload> files, CancellationToken ct = default)
    {
        var started = await start(ct).ConfigureAwait(false);
        if (started.SessionId is not { } to) return new StartedFrom(null, false, null, started.Message);

        bool took;
        try
        {
            took = say(to, text, files, null);
        }
        catch (DriverException) when (files.Count > 0)
        {
            // More files than one message carries: the words go alone rather than not at all.
            took = say(to, text, [], null);
        }

        return took
            ? new StartedFrom(to, true, null, $"conversation `{to}` starts with your words.")
            : new StartedFrom(to, false, null, $"conversation `{to}` opened, but your words did not reach it.");
    }
}
