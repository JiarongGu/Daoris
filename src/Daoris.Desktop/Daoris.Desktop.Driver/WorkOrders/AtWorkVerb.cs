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

        var records = await world.Service.SessionRecordsAsync(ct).ConfigureAwait(false);
        var held = AtWork.Compose(
            session, null,
            id => records.FirstOrDefault(each => each.Id == id) is { } record ? new AtWorkRecord(record.Id, record.State) { Kind = record.Kind } : null,
            record => ChildLineage.Reading(world.Home, record.Id, record.Kind, new LineageTokens(Environment.ProcessId, DateTime.UtcNow)),
            new SliceHealth(true), new SliceHealth(true));
        return Say(held, session, ask.Json, output, Console.Error);
    }

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
        foreach (var part in answer.Parts) lines.Add($"{part.Key}{(part.Key == answer.Attended ? " (attended)" : "")} · {part.State ?? "unknown"}");
        lines.Add($"subagents: {answer.Subagents.Working} working");
        foreach (var child in answer.Subagents.Children)
        {
            var seen = child.LastSeen is { } at ? $" · last seen {at:HH:mm}" : "";
            lines.Add($"{new string(' ', 2 * (child.Depth ?? 1))}{child.Label?.Text ?? child.Type ?? child.Key} · {Words(child.State)}{seen}");
        }

        return lines;
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
