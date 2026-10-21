using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// `daoris-driver space free [--kind &lt;id&gt;]... [--early &lt;group&gt;]... [--yes]` (KEEP1e, the cleanup design §3.3, §4.4, D50): the
/// terminal's twin of This Daoris's <i>Free up space…</i>. Without <c>--yes</c> it lists, per kind, what its rule takes now and what could go
/// early, and changes nothing; the list runs anywhere. With <c>--yes</c> it removes what the rules take (the kinds <c>--kind</c> names, or
/// every kind), and each group <c>--early</c> names, then reads the run to its end, a line per kind as it ends.
/// </summary>
/// <remarks>
/// <para><b>With a Daoris running</b> it is its client (<see cref="InstanceDoor.PressAsync"/>, D194 point 2): the removal is the person's,
/// confirmed in the window (RPC1c), and its answer is the run's id, not its end, so the verb then reads <c>SYSTEM_SPACE</c>, an open read,
/// each second until the run has ended. Where Daoris stops first, how it ended is not known: exit 2. <b>Alone</b> it holds no lease (D194
/// point 3), takes the home's run lock, which a desktop starting meanwhile finds held, and is refused from a session's environment (D194
/// point 5): <c>--yes</c> is the person's word at their own terminal and grants a session nothing.</para>
/// <para>Exit codes keep the family contract: 0 listed, or every unit removed · 1 something named was kept, the person refused it in the
/// window, or a session's terminal · 2 could not: a word it does not answer, a read that failed, the run lock held, the run stopped, or
/// Daoris stopped while it ran.</para>
/// </remarks>
public static class SpaceFreeCommand
{
    public const string Usage = "usage: daoris-driver space free [--kind <id>]... [--early <group>]... [--yes]";

    /// <summary>The kinds a rule takes now, which <c>--kind</c> names.</summary>
    public static IReadOnlyList<string> NowKinds { get; } = ["set-aside", "downloads", "logs"];

    private const string Prefix = "daoris-driver space free";

    /// <summary>How often the run is read again over the pipe while it runs.</summary>
    internal static TimeSpan Every { get; set; } = TimeSpan.FromSeconds(1);

    /// <param name="world">What the press is handed alone: <see cref="SpaceFreeWorld.OfThisTerminal"/>, or a test's.</param>
    /// <param name="instance">The door to the running Daoris, and where the verb writes.</param>
    /// <param name="runs">The runs this process holds; this process's own where none is named.</param>
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args, SpaceFreeWorld world, InstanceWorld instance, CancellationToken ct, SpaceRuns? runs = null)
    {
        var kinds = new List<string>();
        var groups = new List<string>();
        var yes = false;
        for (var at = 0; at < args.Count; at++)
        {
            switch (args[at])
            {
                case "--yes": yes = true; break;
                case "--kind" when at + 1 < args.Count && NowKinds.Contains(args[at + 1]): kinds.Add(args[++at]); break;
                case "--early" when at + 1 < args.Count && SpacePlan.Groups.Contains(args[at + 1]): groups.Add(args[++at]); break;
                case "--kind":
                    return await Refuse(instance, $"`--kind` takes one of {string.Join(", ", NowKinds)}.").ConfigureAwait(false);
                case "--early":
                    return await Refuse(instance, $"`--early` takes one of {string.Join(", ", SpacePlan.Groups)}.").ConfigureAwait(false);
                default:
                    return await Refuse(instance, $"no option `{args[at]}`.").ConfigureAwait(false);
            }
        }

        SpacePlan plan;
        try
        {
            plan = await Task.Run(() => SpaceFree.Plan(world, ct), ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            await instance.Error.WriteLineAsync($"{Prefix}: the list could not be read ({error.Message.TrimEnd('.')}); nothing was done.").ConfigureAwait(false);
            return 2;
        }

        if (!yes)
        {
            await instance.Out.WriteAsync(Listed(plan)).ConfigureAwait(false);
            return 0;
        }

        var asked = plan.Units
            .Where(unit => unit.Early ? groups.Contains(unit.Kind) : kinds.Count == 0 || kinds.Contains(unit.Kind))
            .Select(unit => new SpaceAsked(unit.Kind, unit.Id, unit.Early, unit.Why, unit.Own == true))
            .ToList();
        if (asked.Count == 0)
        {
            await instance.Out.WriteLineAsync("nothing to remove: no rule takes anything now, and no group named holds anything.").ConfigureAwait(false);
            return 0;
        }

        string? sent = null;
        var exit = await InstanceDoor.PressAsync(PersonPresses.SpaceFree, Payload(asked), () => AloneAsync(world, instance, asked, runs ?? SpaceRuns.Shared, ct),
            answer =>
            {
                if (Started(answer, instance) is { } id) sent = id;
                return sent is null ? 2 : 0;
            }, instance, ct).ConfigureAwait(false);
        return sent is null ? exit : await FollowAsync(sent, instance, ct).ConfigureAwait(false);
    }

    /// <summary>The press's payload, as the window's sends it: the units the list showed, now and early.</summary>
    public static JsonObject Payload(IReadOnlyList<SpaceAsked> asked)
    {
        JsonArray Of(bool early) => new([.. asked.Where(unit => unit.Early == early).Select(unit => (JsonNode?)new JsonObject
        {
            ["kind"] = unit.Kind, ["id"] = unit.Id, ["why"] = unit.Why, ["own"] = unit.Own,
        })]);
        return new JsonObject { ["units"] = Of(false), ["early"] = Of(true) };
    }

    /// <summary>The run's id the instance answered, or null with its refusal said (another run holds the home's keeping).</summary>
    private static string? Started(JsonElement answer, InstanceWorld instance)
    {
        if (answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("run", out var run) && run.ValueKind == JsonValueKind.String)
        {
            if (answer.TryGetProperty("joined", out var joined) && joined.ValueKind == JsonValueKind.True)
            {
                instance.Out.WriteLine("a run was already under way in Daoris; following it.");
            }

            return run.GetString();
        }

        var message = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("message", out var said) ? said.GetString() : null;
        instance.Error.WriteLine($"{Prefix}: {message ?? "Daoris did not start the run"}; nothing was done.");
        return null;
    }

    /// <summary>The run read over the pipe each second until it ends, each kind said as it turns done.</summary>
    private static async Task<int> FollowAsync(string id, InstanceWorld instance, CancellationToken ct)
    {
        await instance.Out.WriteLineAsync($"freeing space (run {id}):").ConfigureAwait(false);
        var said = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            JsonElement? run = null;
            var read = await InstanceDoor.TryReadAsync(OpenRoutes.SystemSpace, null, space =>
            {
                run = space.ValueKind == JsonValueKind.Object && space.TryGetProperty("run", out var current) && current.ValueKind == JsonValueKind.Object
                      && current.TryGetProperty("id", out var named) && named.GetString() == id
                    ? current.Clone()
                    : null;
                return 0;
            }, instance, ct).ConfigureAwait(false);
            if (read is null || run is not { } current)
            {
                await instance.Error.WriteLineAsync(
                    $"{Prefix}: Daoris stopped while the run was under way, so how it ended is not known; `daoris-driver space` says what remains.")
                    .ConfigureAwait(false);
                return 2;
            }

            var parsed = JsonNode.Parse(current.GetRawText())!.AsObject();
            foreach (var kind in parsed["kinds"]!.AsArray().Select(node => node!.AsObject()))
            {
                if ((string?)kind["state"] != "done" || !said.Add((string)kind["id"]!)) continue;
                await instance.Out.WriteLineAsync(KindLine(kind)).ConfigureAwait(false);
            }

            if ((string?)parsed["outcome"] is { } outcome) return Ended(outcome, instance);
            try
            {
                await Task.Delay(Every, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await instance.Error.WriteLineAsync(
                    $"{Prefix}: stopped following run {id}; it goes on in Daoris, and `daoris-driver space` says how it ended.").ConfigureAwait(false);
                return 2;
            }
        }
    }

    /// <summary>The press acting alone: refused from a session's terminal; else its run, under the home's run lock, read to its end.</summary>
    private static async Task<int> AloneAsync(
        SpaceFreeWorld world, InstanceWorld instance, IReadOnlyList<SpaceAsked> asked, SpaceRuns runs, CancellationToken ct)
    {
        if (instance.Environment(DriverChoicesCommand.SessionVariable) is { Length: > 0 })
        {
            await instance.Error.WriteLineAsync($"{Prefix}: {DriverChoicesCommand.SessionsTerminal}").ConfigureAwait(false);
            return 1;
        }

        var start = runs.Start(world, KeepingRun.Terminal, asked, ct: ct);
        if (start.Run is not { } run)
        {
            await instance.Error.WriteLineAsync(
                $"{Prefix}: {start.Held?.Said ?? "another run"} holds Daoris's keeping; nothing was done, and this can be run again once it ends.")
                .ConfigureAwait(false);
            return 2;
        }

        await instance.Out.WriteLineAsync($"freeing space (run {run.Id}):").ConfigureAwait(false);
        var said = new HashSet<string>(StringComparer.Ordinal);
        void Say()
        {
            lock (said)
            {
                foreach (var kind in run.Json()["kinds"]!.AsArray().Select(node => node!.AsObject()))
                {
                    if ((string?)kind["state"] == "done" && said.Add((string)kind["id"]!)) instance.Out.WriteLine(KindLine(kind));
                }
            }
        }

        run.KindEnded += _ => Say();
        await run.Finished.ConfigureAwait(false);
        Say();
        return Ended(run.Outcome ?? "stopped", instance);
    }

    private static int Ended(string outcome, InstanceWorld instance)
    {
        switch (outcome)
        {
            case "done":
                instance.Out.WriteLine("done: every unit named was removed.");
                return 0;
            case "kept":
                instance.Out.WriteLine("done: something named was kept, each said above with why.");
                return 1;
            default:
                instance.Error.WriteLine($"{Prefix}: the run stopped before its end; `daoris-driver space` says what remains.");
                return 2;
        }
    }

    /// <summary>A kind's end in a line: what it removed, and what it kept by why.</summary>
    private static string KindLine(JsonObject kind)
    {
        var removed = kind["removed"]!.AsObject();
        var line = $"  {(string?)kind["id"]}: removed {(int)removed["count"]!} ({SpaceCommand.Size((long)removed["bytes"]!)})";
        var kept = kind["kept"]!.AsObject();
        if (kept.Count > 0) line += "; kept " + string.Join(", ", kept.Select(pair => $"{(int)pair.Value!} ({pair.Key})"));
        return line;
    }

    /// <summary>The list as a person reads it: what each rule takes now, what could go early, and the kinds with presses of their own.</summary>
    public static string Listed(SpacePlan plan)
    {
        var text = new StringBuilder();
        var now = plan.Units.Where(unit => !unit.Early).ToList();
        text.Append("what goes now, by the rules: ");
        text.Append(now.Count == 0 ? "nothing\n" : $"{now.Count} {(now.Count == 1 ? "item" : "items")}{Sized(now)}\n");
        foreach (var unit in now) text.Append("  ").Append(unit.Kind).Append("  ").Append(Named(unit)).Append(Sized([unit])).Append('\n');
        text.Append("what could go early, by your word:");
        var early = plan.Units.Where(unit => unit.Early).ToList();
        text.Append(early.Count == 0 ? " nothing\n" : "\n");
        foreach (var group in SpacePlan.Groups)
        {
            var units = early.Where(unit => unit.Kind == group).ToList();
            if (units.Count == 0) continue;
            text.Append("  ").Append(group).Append(": ").Append(units.Count).Append(units.Count == 1 ? " item" : " items").Append(Sized(units));
            var own = units.Count(unit => unit.Own == true);
            if (own > 0) text.Append(", ").Append(own).Append(own == 1 ? " holds files of its own" : " hold files of their own");
            if (group == "browser-cache") text.Append(", kept while the browser runs, and with no Daoris running");
            if (group == "left-over" && units.Any(unit => unit.Id == "browser/profile")) text.Append(" (browser/profile's sign-ins go with it)");
            text.Append("   --early ").Append(group).Append('\n');
        }

        foreach (var (kind, why) in plan.Unread) text.Append("  ").Append(kind).Append(": not read (").Append(why).Append(")\n");
        text.Append("with their own press: `daoris-driver history clear` (Clear history… on a workspace's page), ")
            .Append("`daoris-driver trees clean` (Clean up… on its Branches)\n");
        text.Append("nothing was removed: --yes removes what the rules take, and --early <group> that group too.\n");
        return text.ToString();
    }

    private static string Named(SpaceUnit unit) => unit.Kind switch
    {
        "set-aside" => $"{unit.Id}{(unit.Why == "slim" ? ", its package folders" : unit.At is { } at ? $", past its days since {Day(at)}" : "")}",
        "downloads" => unit.Why == "unfinished" ? $"{unit.Id}, left by an earlier clearing" : unit.Id,
        _ => unit.Id,
    };

    private static string Sized(IReadOnlyList<SpaceUnit> units) =>
        units.All(unit => unit.Bytes is not null) ? $", {SpaceCommand.Size(units.Sum(unit => unit.Bytes!.Value))}" : "";

    private static string Day(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static async Task<int> Refuse(InstanceWorld instance, string why)
    {
        await instance.Error.WriteLineAsync($"{Prefix}: {why}\n{Usage}").ConfigureAwait(false);
        return 2;
    }
}
