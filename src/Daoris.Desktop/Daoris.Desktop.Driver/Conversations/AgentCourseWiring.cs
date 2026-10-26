using System.Runtime.CompilerServices;

namespace Daoris.Driver;

internal sealed class CourseAgentOperations : IAgentCourse
{
    [ModuleInitializer]
    internal static void Install() => AgentCourse.Current = new CourseAgentOperations();

    public IAgentInbox OpenInbox(Action<IAgentQueue> changed) => new DrivenInbox(queue => changed(queue));

    public string? HandOverText(string? note) => HandOverNote.Said(note);

    public Func<TransientEpisode, string, CancellationToken, Task<IAgentWord?>> RetryGoOn(ServiceClient service, string sessionId)
    {
        var door = new SaidDoor(service, sessionId);
        return async (episode, text, ct) =>
        {
            var key = DaorisKeys.TransientRetry(sessionId, episode.Began);
            var said = await service.SayAsync(sessionId, text, ct: ct, from: DaorisWords.From, key: key, running: true).ConfigureAwait(false);
            if (said is not { Kept: true, Word.Id: { } id }) return null;
            // The original dispatcher marks attempted before the wire; the conclusion's key fallback remains below.
            return SaidDoor.Dispatched(
                new ChatMessage(text, []) { Id = id, Kept = true, Daoris = new DaorisWord(key) }, door,
                went => { _ = TakeOffAsync(door, id); });
        };
    }

    /// <summary>The go-on off its record by its id once written; one that fails is taken off by its key at the conclusion.</summary>
    private static async Task TakeOffAsync(ISaidDoor door, string id)
    {
        try
        {
            await door.TakeOffAsync([id], CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or OperationCanceledException
                                          or ObjectDisposedException or System.Text.Json.JsonException)
        {
            // The conclusion's fallback takes it off.
        }
    }

    public async Task GoOnOffAsync(ServiceClient service, string sessionId, CancellationToken ct)
    {
        try
        {
            if (await service.RecordAsync(sessionId, ct).ConfigureAwait(false) is not { } record) return;
            List<string> ids = [.. (record.Said ?? []).Where(word => DaorisKeys.IsTransientRetry(word.Key)).Select(word => word.Id).OfType<string>()];
            if (ids.Count > 0) await service.TakenAsync(sessionId, ids, by: null, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or System.Text.Json.JsonException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            // Left waiting, it is a word of Daoris's, which the hold's word rule never reads as a release.
        }
    }
}

/// <summary>Course's typed entry points over the registry's supplied lower inbox, retaining the actual dispatcher.</summary>
public static class CourseSessionInboxes
{
    public static DrivenInbox OpenInbox(this SessionProcesses processes, string sessionId) =>
        Original(processes.OpenWordInbox(sessionId));

    public static DrivenInbox? InboxOf(this SessionProcesses processes, string sessionId) =>
        processes.WordInboxOf(sessionId) is { } inbox ? Original(inbox) : null;

    private static DrivenInbox Original(IAgentInbox inbox) => inbox as DrivenInbox
        ?? throw new InvalidOperationException("this Course operation requires its original driven inbox");
}
