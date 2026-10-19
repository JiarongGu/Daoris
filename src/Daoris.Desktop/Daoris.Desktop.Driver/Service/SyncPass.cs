using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The pass <c>daoris-driver sync</c> runs now, through the running Daoris where one answers (RPCOPEN1, RPC1e, the terminal RPC design
/// §7.2): the instance's own set runs each circle's pass, as its tick and the screen's <i>Sync now</i> do (<c>SYNC_NOW</c>, D156 §3), and
/// the verb prints the report it answers in the words it prints for a pass of its own. Alone where none runs.
/// </summary>
public static class SyncPass
{
    /// <summary>
    /// One pass of every circle, or of the one <paramref name="workspace"/> names. With an instance, a press per circle, so each circle's
    /// problems and notes carry its name as the set's own report does, and are joined as it joins them; with none, <paramref name="alone"/>
    /// once for the whole. Null where the instance lives and did not run it, which the door has said (exit 2).
    /// </summary>
    /// <param name="circles">The circles this machine has a remote for, in the set's order.</param>
    /// <param name="alone">The set's own pass, for the workspace named or for every circle (null).</param>
    /// <exception cref="DriverException">A circle was named that this machine has no remote for, said as the set says it.</exception>
    public static async Task<SyncReport?> RunAsync(
        string? workspace, IReadOnlyList<string> circles, Func<string?, Task<SyncReport>> alone, InstanceWorld instance,
        CancellationToken ct = default)
    {
        var look = await InstanceDoor.PeekAsync(instance, ct).ConfigureAwait(false);
        if (look.Found is InstanceFound.Silent or InstanceFound.Refused)
        {
            await InstanceDoor.RefusedAsync(look, instance).ConfigureAwait(false);
            return null;
        }

        if (look.Found != InstanceFound.Answers) return await alone(workspace).ConfigureAwait(false);

        IReadOnlyList<string> which;
        if (workspace is null)
        {
            which = circles;
        }
        else
        {
            var named = RemoteTarget.Workspace(workspace);
            if (!circles.Contains(named, StringComparer.OrdinalIgnoreCase)) throw new DriverException(RemoteSyncSet.NoRemote(workspace));
            which = [named];
        }

        var problems = new List<string>();
        var notes = new List<string>();
        foreach (var circle in which)
        {
            SyncReport? report = null;
            var exit = await InstanceDoor.PressAsync(
                OpenRoutes.SyncNow, new { workspace = circle },
                alone: async () =>
                {
                    // The instance went between the look and this press: what none running does, for this circle.
                    report = await alone(circle).ConfigureAwait(false);
                    return 0;
                },
                words: answer =>
                {
                    report = Read(answer);
                    if (report is not null) return 0;
                    instance.Error.WriteLine($"{instance.Verb}: Daoris answered in a form this terminal does not read.");
                    return 2;
                },
                instance, ct).ConfigureAwait(false);
            if (exit != 0 || report is null) return null;
            if (report.Problem is not null) problems.Add(report.Problem);
            notes.AddRange(report.Notes);
        }

        return new SyncReport(problems.Count == 0 ? null : string.Join(" · ", problems), notes);
    }

    /// <summary>The report <c>SYNC_NOW</c> answers (<c>{ workspace, problem, notes }</c>), or null where the answer is no report.</summary>
    private static SyncReport? Read(JsonElement answer)
    {
        if (answer.ValueKind != JsonValueKind.Object) return null;
        string? problem = null;
        var notes = new List<string>();
        var named = false;
        foreach (var each in answer.EnumerateObject())
        {
            if (string.Equals(each.Name, "workspace", StringComparison.OrdinalIgnoreCase)) named = true;
            if (string.Equals(each.Name, "problem", StringComparison.OrdinalIgnoreCase) && each.Value.ValueKind == JsonValueKind.String)
            {
                problem = each.Value.GetString();
            }

            if (string.Equals(each.Name, "notes", StringComparison.OrdinalIgnoreCase) && each.Value.ValueKind == JsonValueKind.Array)
            {
                notes.AddRange(each.Value.EnumerateArray().Where(note => note.ValueKind == JsonValueKind.String).Select(note => note.GetString()!));
            }
        }

        return named ? new SyncReport(problem, notes) : null;
    }
}
