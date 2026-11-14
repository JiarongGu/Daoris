namespace Daoris.Driver;

/// <summary>
/// A start's quest bound to the graph run it is worked in, before its agent can say done: the done is then a submission that leaves it
/// taken, and only the run's end writes its Done (<see cref="RunSessionEnds"/>). A quest bound already, or closed, is left as it stands.
/// </summary>
internal static class RunQuestBinding
{
    /// <summary>
    /// Why the start holds, or null where it goes on. A host that answers no bind (an older build) holds nothing: its quest closes by its done,
    /// as before the bind, and the run's end then ends its sessions alone.
    /// </summary>
    public static async Task<string?> BindAsync(ServiceClient service, QuestView quest, WorkflowBind bound, CancellationToken ct)
    {
        if (bound is not { Run: { Length: > 0 } run, Binding.IsCurrent: false } || quest.Closure is { Length: > 0 } || quest.Status is not ("Open" or "Taken"))
        {
            return null;
        }

        try
        {
            await service.BindToRunAsync(quest.Id, run, ct).ConfigureAwait(false);
            return null;
        }
        catch (DriverException error) when (!ct.IsCancellationRequested && error.Message.Contains("404", StringComparison.Ordinal))
        {
            return null;
        }
        catch (Exception error) when (!ct.IsCancellationRequested && error is DriverException or HttpRequestException)
        {
            return $"its quest #{quest.Id} could not be bound to its run `{run}`, so its done would close it before its workflow ends: {error.Message}";
        }
    }
}
