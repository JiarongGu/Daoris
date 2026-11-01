using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// <c>sessions at-work &lt;session&gt; [--json]</c> (COWORKVIEW1a rule 20): who is at work on a session, from the running Daoris's
/// <c>SESSION_AT_WORK</c> where one answers, and with none from the same function over this process's own reading, which issues no token
/// and so reads every open child as not known whether it still runs.
/// </summary>
public static class AtWorkVerb
{
    /// <summary>How long a pending answer is asked for again before the verb says it was not read.</summary>
    public static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    public static async Task<int> RunAsync(SessionsWorld world, SessionsAsk ask, TextWriter output, CancellationToken ct)
    {
        var session = ask.Ids[0];
        if (world.Instance is { } instance)
        {
            var until = world.Clock.GetUtcNow() + Bound;
            while (true)
            {
                string? waits = null;
                var pending = false;
                var exit = await InstanceDoor.TryReadAsync(OpenRoutes.AtWork, new { session }, data =>
                {
                    if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("pending", out var wait) && wait.ValueKind == JsonValueKind.True)
                    {
                        pending = true;
                        waits = data.TryGetProperty("waits", out var which) && which.ValueKind == JsonValueKind.String ? which.GetString() : null;
                        return 2;
                    }

                    return Say(data.Deserialize<AtWorkHeld>(Wire), session, ask.Json, output, instance.Error);
                }, instance, ct).ConfigureAwait(false);
                if (exit is null) break;
                if (!pending) return exit.Value;
                if (world.Clock.GetUtcNow() >= until) return await NotReadAsync(waits, session, instance.Error).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(1), world.Clock, ct).ConfigureAwait(false);
            }
        }

        // The look the listing reads, so the entry's parts stand in the strip's order as the window's groups slice holds them.
        var look = await SessionGroups.LookAsync(world.Service, world.Config, world.Door, lastLook: null, world.Home, ct: ct, coolingOf: world.CoolingOf)
            .ConfigureAwait(false);
        var tokens = new LineageTokens(Environment.ProcessId, DateTime.UtcNow);
        var held = Compose(look, session, record => ChildLineage.Reading(world.Home, record.Id, record.Kind, tokens));
        return Say(held, session, ask.Json, output, Console.Error);
    }

    /// <summary>
    /// The answer over a look the listing reads: the same function the window's read composes by, its routing half (COWORKVIEW1b-i) taken
    /// from the look's entries, rows and quests where the window takes it from the groups and quests slices.
    /// </summary>
    public static AtWorkHeld Compose(SessionLook look, string session, Func<AtWorkRecord, LineageReading> lineage)
    {
        var records = look.Records.GroupBy(each => each.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var parts = SessionGroups.ReadEntries(look).FirstOrDefault(each => each.Parts.Any(part => part.Session == session))
            ?.Parts.Select(part => new AtWorkEntryPart(part.Session, part.Quest, part.Shown, part.Began) { After = part.After, AskedBy = part.AskedBy }).ToList();
        var rows = SessionGroups.Read(look).GroupBy(row => row.Session, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => new AtWorkRow(group.First().Awaits, group.First().AwaitsOf), StringComparer.Ordinal);
        var quests = look.Quests.GroupBy(quest => quest.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => AtWorkQuestOf(group.First()), StringComparer.Ordinal);
        var published = look.Quests.Where(quest => quest.PublishedBy is { Length: > 0 }).GroupBy(quest => quest.PublishedBy!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var routing = new AtWorkRouting(
            parts ?? [], id => rows.GetValueOrDefault(id), id => quests.GetValueOrDefault(id), id => published.GetValueOrDefault(id), new SliceHealth(true))
        {
            Asking = AtWorkQuests.Reading(look.Quests, look.Records, id => rows.GetValueOrDefault(id)),
        };
        return AtWork.Compose(
            session, parts?.Select(part => part.Session).ToList(),
            id => records.GetValueOrDefault(id) is { } record
                ? new AtWorkRecord(record.Id, record.State)
                {
                    Adapter = record.Adapter, Kind = record.Kind, TookQuest = record.TookQuest, Repository = record.Repository, Tree = record.Tree,
                }
                : null,
            lineage, new SliceHealth(true), new SliceHealth(true), routing);
    }

    private static AtWorkQuest AtWorkQuestOf(QuestView quest) => new(quest.Id, quest.Status)
    {
        To = quest.To,
        From = quest.From,
        Awaits = quest.Awaits,
        PublishedBy = quest.PublishedBy,
        Parent = quest.Parent,
        Lanes = quest.Lanes.Count > 0 ? string.Join('+', quest.Lanes) : null,
        Holder = quest.Holder,
        Here = quest.Here,
        Held = quest.Held,
    };

    private static async Task<int> NotReadAsync(string? waits, string session, TextWriter error)
    {
        await error.WriteLineAsync(waits switch
        {
            AtWork.WaitsSlices => "not read yet: this window has not judged its sessions",
            AtWork.WaitsRecord => $"no session {session} is held here",
            _ => "not read yet: this window did not answer in time",
        }).ConfigureAwait(false);
        return 2;
    }

    private static int Say(AtWorkHeld? held, string session, bool json, TextWriter output, TextWriter error)
    {
        if (held?.Failed is { } failed)
        {
            error.WriteLine($"Who is at work could not be read: {failed.Why}");
            return 2;
        }

        if (held?.Answer is not { } answer)
        {
            error.WriteLine($"no session {session} is held here");
            return 2;
        }

        if (json) output.WriteLine(JsonSerializer.Serialize(held, Wire));
        else foreach (var line in Lines(answer)) output.WriteLine(line);
        return 0;
    }

    /// <summary>The answer in words: each part, then the attended part's subagents as a tree.</summary>
    public static IReadOnlyList<string> Lines(AtWorkAnswer answer)
    {
        var lines = new List<string>();
        foreach (var part in answer.Parts)
        {
            var line = $"{part.Key}{(part.Key == answer.Attended ? " (attended)" : "")} · {part.State ?? "unknown"}";
            if (part.Waits is { } wait) line += $" · {Wait(wait)}";
            if (part.Alongside == true) line += " · alongside, in a tree of its own";
            if (part.Joined is { } joined && Joined(joined) is { } how) line += $" · {how}";
            lines.Add(line);
        }

        if (answer.Earlier is > 0 and var earlier) lines.Add($"{earlier} earlier parts");
        if (answer.Published is > 0 and var published && answer.Parts.All(part => part.Waits is null)) lines.Add($"published {published}, waits on none");
        if (answer.Quests is { Count: > 0 } quests)
        {
            lines.Add("asked through quests:");
            lines.AddRange(quests.Select(quest => $"  {Quest(quest)}"));
            if (answer.MoreQuests is > 0 and var more) lines.Add($"  {more} more quests");
            if (quests.Any(quest => quest.Taker?.Untaken == true)) lines.Add("  why a quest is not taken yet is the driver's last look's, which the window says");
        }

        if (answer.AskedBy is { } asker) lines.Add($"asked by: #{asker.Quest} · {Asker(asker.By)}");
        lines.Add($"subagents: {answer.Subagents.Working} working");
        foreach (var child in answer.Subagents.Children)
        {
            var seen = child.LastSeen is { } at ? $" · last seen {at:HH:mm}" : "";
            lines.Add($"{new string(' ', 2 * (child.Depth ?? 1))}{child.Label?.Text ?? child.Type ?? child.Key} · {Words(child.State)}{seen}");
        }

        return lines;
    }

    /// <summary>One quest a part asked: its name, where it went and by which lanes, the step it follows, who took it and what it waits on.</summary>
    private static string Quest(AtWorkAskedQuest quest)
    {
        List<string> words = [$"#{quest.Id}", quest.Title];
        if (quest.Of is { } of) words.Add($"of {of}");
        if (quest.Lanes is { Count: > 0 } lanes) words.Add($"{string.Join(", ", lanes)} lane");
        if (quest.Step is { } step) words.Add($"the next step of #{step}");
        words.Add(quest.Taker switch
        {
            { Untaken: true } => "not taken yet",
            { Session: { } session } taker => $"taken by {session} ({taker.Agent ?? "agent"}, {taker.State ?? "state not read"})",
            { Conversation: { } conversation } => $"taken by conversation {conversation}",
            { Elsewhere: true } => "taken on another machine",
            { Unrecorded: true } => "taken, by no session recorded here",
            _ => quest.Status.ToLowerInvariant(),
        });
        if (quest.Waits is { } wait) words.Add(Wait(wait));
        return string.Join(" · ", words);
    }

    private static string Asker(AtWorkBy by) => by switch
    {
        { Session: { } session } => $"published by {session}",
        { Step: { } step } => $"the next step of #{step}",
        { Ask: { } ask } => $"ask #{ask}",
        _ => "you",
    };

    /// <summary>What a part waits on, in the rail's words: <i>waits on #q, asked of core</i>, or <i>awaiting helper</i>.</summary>
    private static string Wait(AtWorkWait wait)
    {
        if (wait.Helper == true) return "awaiting helper";
        var lanes = wait.Lanes is { Count: > 0 } named ? $" ({string.Join(", ", named)} lane)" : "";
        return wait.Of is { } of ? $"waits on #{wait.Quest}, asked of {of}{lanes}" : $"waits on #{wait.Quest}{lanes}";
    }

    /// <summary>How a part joined, in the design's words (§3.4); null where it only began.</summary>
    private static string? Joined(AtWorkJoined joined)
    {
        var part = (int? number) => number is { } n ? $"part {n}" : "another session";
        if (joined.Took == true) return $"took #{joined.Quest}";
        if (joined.Ask is { } ask) return joined.Began == PartBegan.Asked ? $"the ask's quest #{joined.Quest}" : $"by ask #{ask}";
        if (joined.Parent is { } parent) return $"the next step of #{parent}";
        if (joined.Lanes is { Count: > 0 } lanes && joined.ByPart is not null) return $"published to the {string.Join(", ", lanes)} lane by {part(joined.ByPart)}";
        if (joined.ByPart is not null && joined.Of is { } repository) return $"asked of {repository} by {part(joined.ByPart)}";
        return joined.Began switch
        {
            PartBegan.Asked => joined.AskedBy is { } by ? $"asked by {by}" : "asked by another session",
            PartBegan.Retried => $"retried after {part(joined.After)}",
            PartBegan.Resumed => $"resumed after {part(joined.After)}",
            PartBegan.CarriedOn => joined.After is null ? "carried on" : $"carried on from {part(joined.After)}",
            PartBegan.HandedOver => $"handed over from {part(joined.After)}",
            PartBegan.Moved => $"moved on from {part(joined.After)}",
            PartBegan.Opened => null,
            var other => $"`{other}`, shown as recorded",
        };
    }

    private static string Words(string state) => state switch
    {
        "uncertain" => "not known whether it still runs",
        "agentEnded" => "its agent ended",
        "ended" => "ended with its agent",
        "lost" => "lost when its agent ended",
        "unrecorded" => "ended, its end not recorded",
        _ => state,
    };
}
