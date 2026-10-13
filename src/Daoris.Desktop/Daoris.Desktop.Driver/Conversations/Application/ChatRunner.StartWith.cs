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

public sealed partial class ChatRunner
{
    private StartRequests? _starts;

    /// <summary>
    /// Open a conversation on its first words (NEW1a): <see cref="StartAsync"/>, then <see cref="Say"/> with the words and
    /// files. A request id makes a retry answer the first start (<see cref="StartRequests"/>).
    /// </summary>
    /// <param name="ownTree">Absent is the driver's choice; true a tree of its own; false the checkout.</param>
    /// <exception cref="DriverException">The request id is malformed.</exception>
    public Task<StartedFrom> StartWithAsync(
        string repository, string adapter, DriverConfig config, string text, IReadOnlyList<ChatUpload> files, string? requestId,
        bool? ownTree = null, string? profile = null, Func<string, string, Task>? onEnded = null, CancellationToken ct = default)
    {
        Task<StartedFrom> Run() => _closing
            ? Task.FromResult(new StartedFrom(null, false, null, "the application is closing, so no conversation starts now."))
            : StartWith.RunAsync(
                token => StartAsync(repository, adapter, config, onEnded, profile, ownTree, token),
                (id, words, attached, preface) => Say(id, words, attached, preface),
                text, files, ct);

        if (requestId is null) return Run();
        _starts ??= new StartRequests(_home);
        return _starts.RunAsync(requestId, Run);
    }

    /// <summary>Whether a conversation opens in a tree of its own: said, or else the repository's standing choice (D51).</summary>
    public static bool Isolates(bool? ownTree, DriverConfig config, string repository) =>
        ownTree ?? config.OpensOwnTree(repository);
}
