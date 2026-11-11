namespace Daoris.Driver;

/// <summary>
/// <c>daoris ask --delete &lt;id&gt;</c>: an ask made by mistake deleted with every quest it became, through the service's own door, its
/// sentence printed. The ask's record <i>Delete</i> is the other door. The person's press where Daoris runs; else the direct post, which
/// the service refuses a keyless call.
/// </summary>
/// <remarks>Exit codes: 0 deleted, 1 refused (a quest it became has been started on, no such ask).</remarks>
public static class AskDeleteCommand
{
    /// <summary>Post the delete and say the service's sentence: 0 deleted, 1 refused.</summary>
    public static async Task<int> RunAsync(string ask, ServiceClient service, TextWriter output, CancellationToken ct = default)
    {
        var (ok, message) = await service.DeleteAskAsync(ask, ct).ConfigureAwait(false);
        output.WriteLine(message);
        // A refusal is an answer, not a tool error.
        return ok ? 0 : 1;
    }

    /// <summary>The person's press through the running Daoris's window where one answers, its lines the route's; else <see cref="RunAsync"/>.</summary>
    public static Task<int> PressAsync(string ask, ServiceClient service, TextWriter output, InstanceWorld? instance, CancellationToken ct = default) =>
        InstanceDoor.ThroughAsync(instance, PersonPresses.AskDelete, InstanceDoor.Said(("ask", ask)), () => RunAsync(ask, service, output, ct), output, ct);
}
