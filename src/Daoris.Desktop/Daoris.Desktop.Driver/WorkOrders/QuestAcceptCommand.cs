namespace Daoris.Driver;

/// <summary>
/// <c>daoris quest accept &lt;id&gt;</c>: a done's departure from what the person required, accepted through the service's own door, its
/// sentence printed. The quest page's yes is the other door. The person's press where Daoris runs; else the direct post, which the
/// service refuses a keyless call.
/// </summary>
/// <remarks>Exit codes: 0 accepted, 1 refused (nothing waits for a yes, no such quest).</remarks>
public static class QuestAcceptCommand
{
    /// <summary>Post the accept and say the service's sentence: 0 accepted, 1 refused.</summary>
    public static async Task<int> RunAsync(string quest, ServiceClient service, TextWriter output, CancellationToken ct = default)
    {
        var (ok, message) = await service.AcceptDepartureAsync(quest, ct).ConfigureAwait(false);
        output.WriteLine($"daoris: {message}");
        // A refusal is an answer, not a tool error.
        return ok ? 0 : 1;
    }

    /// <summary>The person's press through the running Daoris's window where one answers, its lines the route's; else <see cref="RunAsync"/>.</summary>
    public static Task<int> PressAsync(string quest, ServiceClient service, TextWriter output, InstanceWorld? instance, CancellationToken ct = default) =>
        InstanceDoor.ThroughAsync(instance, PersonPresses.QuestAccept, InstanceDoor.Said(("quest", quest)), () => RunAsync(quest, service, output, ct), output, ct);
}
