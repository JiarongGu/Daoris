using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>What <c>daoris queue</c> reads alone: the home, and the planner's verdicts over a fresh snapshot.</summary>
/// <param name="Plan">The plan whose waiting starts it lists: the planner over a fresh snapshot, as <c>sessions</c> plans alone.</param>
public sealed record QueueWorld(string Home, Func<CancellationToken, Task<IReadOnlyList<Consideration>>> Plan)
{
    /// <summary>The running Daoris to read through, where one answers; null reads alone.</summary>
    public InstanceWorld? Instance { get; init; }

    /// <summary>The check slots as they stand; the home's by default.</summary>
    public Func<VerifyLook>? Slots { get; init; }
}

/// <summary>
/// <c>daoris queue [--json]</c>: every item waiting in the machine's queues, each with its place, its class and where it comes from, its
/// stage, why it waits and the items ahead, read by the one reader the window's queues view reads (<see cref="QueueRows"/>, <c>QUEUES</c>):
/// the running Daoris's answer where it answers, so its last look's plan is listed, else this process's plan over a fresh snapshot.
/// </summary>
/// <remarks>Exit codes keep the family contract: 0 said · 2 the usage.</remarks>
[CommandGroup("queue", Takes = "(read)", Whole = true, Bare = true)]
public static class QueueCommand
{
    public const string Usage = "usage: daoris queue [--json]";

    public static async Task<int> RunAsync(IReadOnlyList<string> args, QueueWorld world, TextWriter output, CancellationToken ct = default)
    {
        if (args is not ([] or ["--json"]))
        {
            output.WriteLine(Usage);
            return 2;
        }

        var json = args.Count == 1;
        return await InstanceDoor.ThroughAsync(
            world.Instance, OpenRoutes.Queues, InstanceDoor.Said(("json", json ? true : null)),
            async () =>
            {
                var rows = QueueRows.Read(await world.Plan(ct).ConfigureAwait(false), (world.Slots ?? (() => VerifySlot.Look(world.Home)))());
                foreach (var line in Lines(rows, json)) output.WriteLine(line);
                return 0;
            },
            output, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The verb's lines from the rows: what it prints alone, and what <c>QUEUES</c> answers as <c>said</c>, so both print the same words.
    /// <c>--json</c> is one element, the rows as the route answers them.
    /// </summary>
    public static IReadOnlyList<string> Lines(IReadOnlyList<QueueRow> rows, bool json) =>
        json
            ? [new JsonObject { ["rows"] = new JsonArray([.. rows.Select(QueueRows.Wire)]) }.ToJsonString()]
            : QueueRows.Said(rows).TrimEnd('\n').Split('\n');
}
