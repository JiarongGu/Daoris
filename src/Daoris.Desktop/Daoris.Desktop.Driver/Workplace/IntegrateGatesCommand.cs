using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris integrate &lt;repository&gt; --accept|--withdraw [--plan]</c>: a line's declaration accepted (or the acceptance withdrawn)
/// from a terminal, pressed on <c>INTEGRATION_GATES</c> through the running Daoris as a terminal press, the change said first in the
/// words the window says it in. <c>--plan</c> says it and writes nothing. The one acceptance serves every purpose the declaration has.
/// </summary>
public static class IntegrateGatesCommand
{
    /// <summary>The acts it takes, as <see cref="IntegrateAsk.Act"/> spells them.</summary>
    public static IReadOnlyList<string> Acts { get; } = ["accept", "withdraw"];

    private static InstanceRoute Look { get; } = new(InstanceRoute.DriverModule, "WORKFLOW_CURRENT", RouteClass.Open);

    private static InstanceRoute Press { get; } = new(InstanceRoute.DriverModule, "INTEGRATION_GATES", RouteClass.Person);

    /// <summary>What a terminal says where no Daoris runs to take the press: the acceptance is the person's, pressed in its window.</summary>
    public static string Alone(IntegrateAsk ask) =>
        $"integrate: `--{ask.Act}` is the owner's act, and no Daoris answers here: open `{ask.Repository}`'s set-up page in Daoris's window to {(ask.Act == "accept" ? "accept" : "withdraw")} its checks.";

    /// <summary>The standing of the line's declaration as the route answers it, said in plain words; <paramref name="answer"/> is <c>WORKFLOW_CURRENT</c>'s.</summary>
    public static (string? Blob, bool Waits, IReadOnlyList<string> Lines) Read(JsonElement answer, string repository)
    {
        var start = answer.TryGetProperty("start", out var found) ? found : default;
        var lines = new List<string>();
        var waits = false;
        string? blob = null;
        foreach (var part in new[] { "integration", "landed" })
        {
            if (start.ValueKind != JsonValueKind.Object || !start.TryGetProperty(part, out var each) || each.ValueKind != JsonValueKind.Object) continue;
            var state = Text(each, "state");
            if (state is not ("changed" or "unaccepted")) continue;
            waits = true;
            if (each.TryGetProperty("tip", out var tip) && tip.ValueKind == JsonValueKind.Object)
            {
                blob = Text(tip, "gates");
                lines.Insert(0, $"integrate: `{repository}`'s checks on {Text(tip, "line")} {(state == "changed" ? "changed since you accepted them" : "have not been accepted")}:");
            }

            if (each.TryGetProperty("change", out var change) && change.ValueKind == JsonValueKind.Object) lines.AddRange(ChangeLines(change));
            break;
        }

        return (blob, waits, lines);
    }

    private static IEnumerable<string> ChangeLines(JsonElement change)
    {
        foreach (var (key, verb) in new[] { ("added", "added"), ("removed", "removed"), ("changed", "changed") })
        {
            if (!change.TryGetProperty(key, out var checks) || checks.ValueKind != JsonValueKind.Array) continue;
            foreach (var check in checks.EnumerateArray())
            {
                var was = Text(check, "was");
                yield return $"  {verb} `{Text(check, "name")}`: {Text(check, "run")}{(was is null ? "" : $" (was: {was})")}";
            }
        }

        if (change.TryGetProperty("other", out var other) && other.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in other.EnumerateArray()) yield return $"  changed its `{part.GetString()}` part";
        }
    }

    private static string? Text(JsonElement of, string name) => of.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Says the change, then presses the acceptance (or its withdrawal) unless <c>--plan</c> asked for the words alone; 0 where done, 1 where refused.</summary>
    public static async Task<int> RunAsync(IntegrateAsk ask, InstanceWorld world, CancellationToken ct = default)
    {
        JsonElement? standing = null;
        var found = await world.PressAsync(Look, new { repository = ask.Repository }, () => Task.FromResult(-1), answer => { standing = answer.Clone(); return 0; }, ct)
            .ConfigureAwait(false);
        if (found == -1)
        {
            await world.Error.WriteLineAsync(Alone(ask)).ConfigureAwait(false);
            return 1;
        }

        if (found != 0 || standing is not { } read) return found == 0 ? 1 : found;
        var (blob, waits, lines) = Read(read, ask.Repository);
        if (ask.Act == "accept")
        {
            if (!waits || blob is null)
            {
                world.Out.WriteLine($"integrate: `{ask.Repository}` has no checks waiting for your acceptance.");
                return 0;
            }

            foreach (var line in lines) world.Out.WriteLine(line);
            if (ask.PlanOnly)
            {
                world.Out.WriteLine("integrate: --plan: nothing was accepted.");
                return 0;
            }
        }
        else
        {
            world.Out.WriteLine($"integrate: withdraws your acceptance of `{ask.Repository}`'s checks: its landed work verifies by none, and integration uses none, until you accept them again.");
            if (ask.PlanOnly)
            {
                world.Out.WriteLine("integrate: --plan: nothing was withdrawn.");
                return 0;
            }
        }

        object payload = ask.Act == "accept" ? new { repository = ask.Repository, gates = blob } : new { repository = ask.Repository, clear = true };
        return await world.PressAsync(Press, payload, () => Task.FromResult(-1), answer =>
        {
            var done = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("done", out var d) && d.ValueKind == JsonValueKind.True;
            var message = answer.ValueKind == JsonValueKind.Object ? Text(answer, "message") : null;
            world.Out.WriteLine(done ? $"integrate: {(ask.Act == "accept" ? "accepted" : "withdrawn")}: `{ask.Repository}`'s checks." : $"integrate: {message ?? "nothing was changed."}");
            return done ? 0 : 1;
        }, ct).ConfigureAwait(false) switch
        {
            -1 => await AloneAsync(ask, world).ConfigureAwait(false),
            var code => code,
        };
    }

    private static async Task<int> AloneAsync(IntegrateAsk ask, InstanceWorld world)
    {
        await world.Error.WriteLineAsync(Alone(ask)).ConfigureAwait(false);
        return 1;
    }
}
