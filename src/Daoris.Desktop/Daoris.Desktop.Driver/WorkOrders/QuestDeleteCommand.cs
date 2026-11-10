namespace Daoris.Driver;

/// <summary>
/// <c>daoris quest delete &lt;id&gt;</c>: a quest made by mistake deleted through the service's own door, its sentence printed. The quest
/// drawer's <i>Delete</i> is the other door. The person's press where Daoris runs; else the direct post, which the service refuses a
/// keyless call.
/// </summary>
/// <remarks>Exit codes: 0 deleted, 1 refused (something stands on the quest, no such quest).</remarks>
public static class QuestDeleteCommand
{
    /// <summary>Post the delete and say the service's sentence: 0 deleted, 1 refused.</summary>
    public static async Task<int> RunAsync(string quest, ServiceClient service, TextWriter output, CancellationToken ct = default)
    {
        var (ok, message) = await service.DeleteQuestAsync(quest, ct).ConfigureAwait(false);
        output.WriteLine($"daoris: {message}");
        // A refusal is an answer, not a tool error.
        return ok ? 0 : 1;
    }

    /// <summary>The person's press through the running Daoris's window where one answers, its lines the route's; else <see cref="RunAsync"/>.</summary>
    public static Task<int> PressAsync(string quest, ServiceClient service, TextWriter output, InstanceWorld? instance, CancellationToken ct = default) =>
        InstanceDoor.ThroughAsync(instance, PersonPresses.QuestDelete, InstanceDoor.Said(("quest", quest)), () => RunAsync(quest, service, output, ct), output, ct);
}
