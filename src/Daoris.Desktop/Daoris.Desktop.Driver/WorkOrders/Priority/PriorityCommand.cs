using System.Globalization;

namespace Daoris.Driver;

/// <summary>An ask or a quest a class is set on, as the service found it: which, its id, its workspace, and a quest's ask.</summary>
public sealed record PriorityTarget(string Target, string Id, string Workspace, string? Ask)
{
    /// <summary>The run a quest's work is (<see cref="QuestRuns"/>), which its check is named by; null for an ask.</summary>
    public string? Run { get; init; }
}

/// <summary>What the command reads and writes: the home, how an id is found, the clock, and who sets the class.</summary>
/// <param name="Find">The ask or quest an id names, or null where it names none.</param>
/// <param name="By">Who sets it, as a wait line names them: <c>the person</c> from their own terminal, <c>session &lt;id&gt;</c> from a session's.</param>
public sealed record PriorityWorld(string Home, Func<string, CancellationToken, Task<PriorityTarget?>> Find, Func<DateTimeOffset> Clock, string By)
{
    /// <summary>The workspace's cap and urgent life; Daoris's defaults until a workspace sets its own.</summary>
    public PriorityRules Rules { get; init; } = PriorityRules.Default;

    /// <summary>
    /// This terminal's: the home, an id found among the service's quests and then its asks, the machine's clock, and the session whose
    /// terminal this is (<see cref="ChildMarks.SessionVariable"/>), else the person.
    /// </summary>
    public static PriorityWorld OfThisTerminal(string home, TimeProvider clock, Func<string, string?>? variable = null)
    {
        variable ??= Environment.GetEnvironmentVariable;
        var session = variable(ChildMarks.SessionVariable);
        return new PriorityWorld(home, FindAsync, clock.GetUtcNow, string.IsNullOrWhiteSpace(session) ? "the person" : $"session {session.Trim()}");
    }

    private static async Task<PriorityTarget?> FindAsync(string id, CancellationToken ct)
    {
        using var service = ServiceClient.FromEnvironment();
        if (await service.FindQuestAsync(id, ct).ConfigureAwait(false) is { } quest)
        {
            var chain = new Dictionary<string, QuestView>(StringComparer.Ordinal) { [quest.Id] = quest };
            for (var (at, step) = (quest, 0); step < QuestRuns.Steps && at.Parent is { Length: > 0 } parent && !chain.ContainsKey(parent); step++)
            {
                if (await service.FindQuestAsync(parent, ct).ConfigureAwait(false) is not { } above) break;
                chain[above.Id] = above;
                at = above;
            }

            return new PriorityTarget(PriorityBook.Quest, quest.Id, quest.Workspace, AskWords.AskOf(quest.From))
            {
                Run = QuestRuns.Of(quest, parent => chain.GetValueOrDefault(parent)),
            };
        }

        return await service.FindAskAsync(id, ct).ConfigureAwait(false) is { } ask ? new PriorityTarget(PriorityBook.Ask, ask.Id, ask.Workspace, null) : null;
    }
}

/// <summary>
/// <c>daoris priority &lt;ask|quest&gt; [&lt;class&gt; --why "…" [--until &lt;time&gt;]]</c>: sets an ask's or a quest's
/// class as a recorded event (<see cref="PriorityBook"/>), or with the id alone says the class standing and where it comes from. Any
/// actor may set one, with a why; urgent takes its end from <c>--until</c> (a time, or <c>&lt;n&gt;m</c>, <c>h</c> or <c>d</c> from now), else the
/// workspace's urgent life, and is refused past the workspace's cap.
/// </summary>
/// <remarks>Exit codes keep the family contract: 0 set or said · 1 refused, or the id names no ask or quest · 2 the usage.</remarks>
[CommandGroup("priority", Takes = "<ask|quest>", Whole = true)]
public static class PriorityCommand
{
    public const string Usage = "usage: daoris priority <ask|quest> [<class> --why \"<words>\" [--until <time> | <n>m|h|d]]  (class: urgent, high, normal or low) | daoris priority <ask|quest> hold --why \"<words>\" | daoris priority <ask|quest> lift";

    public static async Task<int> RunAsync(IReadOnlyList<string> args, PriorityWorld world, TextWriter said, CancellationToken ct)
    {
        if (Parse(args, world.Clock()) is not { } asked)
        {
            said.WriteLine(Usage);
            return 2;
        }

        if (await world.Find(asked.Id, ct).ConfigureAwait(false) is not { } target)
        {
            said.WriteLine($"priority: `{asked.Id}` names no ask or quest here.");
            return 1;
        }

        var now = world.Clock();
        if (asked.Hold is { } hold)
        {
            var word = new HoldEvent(now, target.Target, target.Id, target.Workspace, hold, asked.Why ?? "lifted", world.By) { Run = target.Run };
            if (HoldBook.Set(world.Home, word) is { } notHeld)
            {
                said.WriteLine($"priority: not set: {notHeld}");
                return 1;
            }

            said.WriteLine(HoldStanding(target, HoldBook.Read(world.Home)));
            return 0;
        }

        if (asked.Class is not { } @class)
        {
            var (events, holds) = PriorityBook.Load(world.Home);
            said.WriteLine(Standing(target, events, now));
            if (HoldStanding(target, holds) is { Length: > 0 } held && held != NotHeld(target)) said.WriteLine(held);
            return 0;
        }

        var until = asked.Until ?? (@class == PriorityClasses.Urgent ? now + world.Rules.UrgentLife : null);
        var change = new PriorityEvent(now, target.Target, target.Id, target.Workspace, @class, asked.Why!, world.By) { Until = until, Run = target.Run };
        if (PriorityBook.Set(world.Home, change, now, world.Rules) is { } refused)
        {
            said.WriteLine($"priority: not set: {refused}");
            return 1;
        }

        said.WriteLine(Standing(target, PriorityBook.Read(world.Home), now));
        return 0;
    }

    /// <summary>The class standing on an ask or a quest, and where it comes from: <c>quest #q is high, from its ask #a, set by the person: why.</c></summary>
    public static string Standing(PriorityTarget target, IReadOnlyList<PriorityEvent> events, DateTimeOffset now)
    {
        var reading = target.Target == PriorityBook.Quest
            ? PriorityBook.ClassOf(events, target.Id, target.Ask, now)
            : PriorityBook.ClassOf(events, null, target.Id, now);
        var from = reading.Event is not { } change
            ? ": none is set"
            : (target.Target == PriorityBook.Quest && reading.Source == PriorityBook.Ask ? $", from its ask #{change.Id}" : "")
              + $", {PriorityBook.Said(change)}" + (change.Until is { } end ? $", until {PriorityBook.Stamp(end)}" : "");
        return $"{target.Target} #{target.Id} is {reading.Class}{from}.";
    }

    /// <summary>Whether an ask's or a quest's waiting items stand aside, and whose word says so: <c>quest #q is held by the person: why.</c></summary>
    public static string HoldStanding(PriorityTarget target, IReadOnlyList<HoldEvent> holds)
    {
        var word = HoldBook.Standing(holds, target.Target, target.Id) ?? (target.Ask is { Length: > 0 } ask ? HoldBook.Standing(holds, PriorityBook.Ask, ask) : null);
        return word is { Held: true } held
            ? $"{target.Target} #{target.Id} is {HoldBook.Said(held)}."
            : word is null ? NotHeld(target) : $"{NotHeld(target)[..^1]}, lifted by {word.By}.";
    }

    private static string NotHeld(PriorityTarget target) => $"{target.Target} #{target.Id} is not held.";

    private sealed record PriorityWords(string Id, string? Class, string? Why, DateTimeOffset? Until, bool? Hold = null);

    private static PriorityWords? Parse(IReadOnlyList<string> args, DateTimeOffset now)
    {
        if (args.Count == 0 || args[0].StartsWith('-') || args[0].TrimStart('#').Length == 0) return null;
        var id = args[0].TrimStart('#');
        if (args.Count == 1) return new PriorityWords(id, null, null, null);
        if (args[1] is "hold" or "lift")
        {
            var holding = args[1] == "hold";
            if (args.Count == 2) return holding ? null : new PriorityWords(id, null, null, null, false);
            return args is [_, _, "--why", { Length: > 0 } reason] && !string.IsNullOrWhiteSpace(reason) ? new PriorityWords(id, null, reason, null, holding) : null;
        }

        if (PriorityClasses.Parse(args[1]) is not { } @class) return null;

        string? why = null;
        DateTimeOffset? until = null;
        for (var at = 2; at < args.Count; at += 2)
        {
            if (at + 1 >= args.Count) return null;
            switch (args[at])
            {
                case "--why" when !string.IsNullOrWhiteSpace(args[at + 1]):
                    why = args[at + 1];
                    break;
                case "--until" when Time(args[at + 1], now) is { } end:
                    until = end;
                    break;
                default:
                    return null;
            }
        }

        return why is null ? null : new PriorityWords(id, @class, why, until);
    }

    /// <summary>A time as given: a moment (ISO 8601, UTC where it names no offset), or minutes, hours or days from now.</summary>
    private static DateTimeOffset? Time(string word, DateTimeOffset now)
    {
        var unit = word.Length > 1 ? word[^1] : ' ';
        if (unit is 'm' or 'h' or 'd' && int.TryParse(word[..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count > 0)
        {
            return now + (unit == 'm' ? TimeSpan.FromMinutes(count) : unit == 'h' ? TimeSpan.FromHours(count) : TimeSpan.FromDays(count));
        }

        return word.Contains('T') && DateTimeOffset.TryParse(word, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at) ? at : null;
    }
}
