using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What <c>daoris ask at-work</c> was asked: the ask, and whether to print the answer whole.</summary>
public sealed record AskAtWorkAsk(string Ask, bool Json);

/// <summary>
/// <c>ask at-work &lt;ask&gt; [--json]</c> (COWORKVIEW1d, the cowork view design §3.5): who is at work on an ask, the terminal's twin of the ask
/// page's <i>At work</i> section. From the running Daoris's <c>SESSION_AT_WORK</c> keyed <c>{ ask }</c> where one answers, and with none from
/// the same function over this process's own reading (<see cref="AtWorkAsk"/>), which issues no token and so reads every open child as not
/// known whether it still runs. It prints the answer's lines of work and quests, and one line naming what it leaves out.
/// </summary>
public static class AskAtWorkVerb
{
    /// <summary>The verb's usage, as the host prints it beside a problem.</summary>
    public const string Usage = "ask at-work <id> [--json]";

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    /// <summary>Whether the words after <c>ask</c> are this verb's, before the ask's other words, which would take <c>at-work</c> as a new ask's.</summary>
    public static bool Asks(IReadOnlyList<string> args) => args is ["at-work", ..];

    /// <summary>The words after <c>ask at-work</c> read, or null with why not.</summary>
    public static AskAtWorkAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        switch (args)
        {
            case [var id] when Id(id):
                return new AskAtWorkAsk(id.TrimStart('#'), false);
            case [var id, "--json"] when Id(id):
                return new AskAtWorkAsk(id.TrimStart('#'), true);
            default:
                problem = "`at-work` takes one ask's id, and `--json` for the answer whole.";
                return null;
        }
    }

    private static bool Id(string text) => text.TrimStart('#').Length > 0 && !text.StartsWith('-');

    public static async Task<int> RunAsync(SessionsWorld world, AskAtWorkAsk ask, TextWriter output, CancellationToken ct)
    {
        if (world.Instance is { } instance)
        {
            var until = world.Clock.GetUtcNow() + AtWorkVerb.Bound;
            while (true)
            {
                var pending = false;
                string? waits = null;
                var exit = await InstanceDoor.TryReadAsync(OpenRoutes.AtWork, new { ask = ask.Ask }, data =>
                {
                    if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("pending", out var wait) && wait.ValueKind == JsonValueKind.True)
                    {
                        waits = data.TryGetProperty("waits", out var which) && which.ValueKind == JsonValueKind.String ? which.GetString() : null;
                        // An ask the window does not hold is no wait: say so now.
                        pending = waits != AtWorkAsk.WaitsAsk;
                        return waits == AtWorkAsk.WaitsAsk ? Say(null, ask, output, instance.Error) : 2;
                    }

                    return Say(data.Deserialize<AtWorkHeld>(Wire), ask, output, instance.Error);
                }, instance, ct).ConfigureAwait(false);
                if (exit is null) break;
                if (!pending) return exit.Value;
                if (world.Clock.GetUtcNow() >= until)
                {
                    await instance.Error.WriteLineAsync("not read yet: this window did not answer in time").ConfigureAwait(false);
                    return 2;
                }

                await Task.Delay(TimeSpan.FromSeconds(1), world.Clock, ct).ConfigureAwait(false);
            }
        }

        // The look the listing reads, and the asks the service holds: the existence the window's asks slice gives.
        var look = await SessionGroups.LookAsync(world.Service, world.Config, world.Unadopted, lastLook: null, world.Home, ct: ct, coolingOf: world.CoolingOf)
            .ConfigureAwait(false);
        var asks = await world.Service.EveryAskAsync(ct).ConfigureAwait(false);
        var tokens = new LineageTokens(Environment.ProcessId, DateTime.UtcNow);
        var held = Compose(look, asks, ask.Ask, record => ChildLineage.Reading(world.Home, record.Id, record.Kind, tokens));
        return Say(held, ask, output, Console.Error);
    }

    /// <summary>The answer over a look the listing reads and the asks the service holds, by the function the window's read composes by.</summary>
    public static AtWorkHeld Compose(SessionLook look, IReadOnlyList<AskView> asks, string ask, Func<AtWorkRecord, LineageReading> lineage)
    {
        var records = look.Records.GroupBy(each => each.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var entries = SessionGroups.ReadEntries(look)
            .Where(entry => entry.Origin == $"ask:{ask}" && entry.Repository != "intake")
            .Select(entry => new AtWorkAskEntryIn(
                entry.Key, entry.Repository, entry.Opens, entry.Parts.FirstOrDefault(part => part.Session == entry.Leading)?.Shown ?? entry.Parts[^1].Shown,
                [.. entry.Parts.Select(part => part.Session)]))
            .OrderBy(entry => entry.Entry, StringComparer.Ordinal).ToList();
        var rows = SessionGroups.Read(look).GroupBy(row => row.Session, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => new AtWorkRow(group.First().Awaits, group.First().AwaitsOf), StringComparer.Ordinal);
        return AtWorkAsk.Compose(
            ask, asks.Any(each => string.Equals(each.Id, ask, StringComparison.Ordinal)), entries,
            id => records.GetValueOrDefault(id) is { } record
                ? new AtWorkRecord(record.Id, record.State)
                {
                    Adapter = record.Adapter, Kind = record.Kind, TookQuest = record.TookQuest, Repository = record.Repository, Tree = record.Tree,
                }
                : null,
            lineage, new SliceHealth(true), new SliceHealth(true), new SliceHealth(true), new SliceHealth(true),
            () => AtWorkQuests.ForAsk(ask, look.Quests, look.Records, id => rows.GetValueOrDefault(id)));
    }

    private static int Say(AtWorkHeld? held, AskAtWorkAsk ask, TextWriter output, TextWriter error)
    {
        if (held?.Failed is { } failed)
        {
            error.WriteLine($"Who is at work could not be read: {failed.Why}");
            return 2;
        }

        if (held?.AskAnswer is not { } answer)
        {
            // The slices not judged are a wait; an ask not held is refused by name.
            if (held?.Pending == true && held.Waits != AtWorkAsk.WaitsAsk)
            {
                error.WriteLine("not read yet: this window has not judged its sessions");
                return 2;
            }

            error.WriteLine($"no ask #{ask.Ask} is held here");
            return 1;
        }

        if (ask.Json) output.WriteLine(JsonSerializer.Serialize(held, Wire));
        else foreach (var line in Lines(answer)) output.WriteLine(line);
        return 0;
    }

    /// <summary>The answer in words: each line of work as a count, then the ask's quests, then what it leaves out.</summary>
    public static IReadOnlyList<string> Lines(AtWorkAskAnswer answer)
    {
        var total = answer.Entries.Count + (answer.MoreEntries ?? 0);
        var lines = new List<string> { $"ask #{answer.Of.Ask}: {total} {(total == 1 ? "line" : "lines")} of work" };
        foreach (var entry in answer.Entries)
        {
            var line = $"  {entry.Repository} · {entry.Shown} · {Count(entry.Parts, "part")}";
            if (entry.Working > 0) line += $", {entry.Working} working";
            if (entry.Subagents > 0) line += $" · {Count(entry.Subagents, "subagent")}, {entry.SubagentsWorking} working";
            if (entry.Elsewhere == true) line += " · a teammate's part's subagents are not visible on this machine";
            lines.Add($"{line} · opens {entry.Opens}");
        }

        if (answer.MoreEntries is > 0 and var moreEntries) lines.Add($"  {moreEntries} more {(moreEntries == 1 ? "line" : "lines")} of work not drawn");
        if (answer.Quests is { Count: > 0 } quests)
        {
            lines.Add("quests asked by the ask:");
            lines.AddRange(quests.Select(quest => $"  {AtWorkVerb.Quest(quest)}"));
            if (answer.MoreQuests is > 0 and var more) lines.Add($"  {Count(more, "more quest")} not drawn");
        }

        lines.Add("left out: why a quest is not taken yet is the driver's last look's, which the window says; the channel is `daoris ask channel "
            + answer.Of.Ask + "`");
        return lines;
    }

    private static string Count(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
