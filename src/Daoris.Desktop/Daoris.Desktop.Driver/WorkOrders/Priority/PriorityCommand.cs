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

    /// <summary>The session whose terminal sets it, whose <c>priority.set</c> the risk policy judges; null for the person's own terminal and window.</summary>
    public string? Session { get; init; }

    public ITerminalRisk Risk { get; init; } = TerminalRisk.Current;

    /// <summary>
    /// This terminal's: the home, an id found among the service's quests and then its asks, the machine's clock, and the session whose
    /// terminal this is (<see cref="ChildMarks.SessionVariable"/>), else the person.
    /// </summary>
    public static PriorityWorld OfThisTerminal(string home, TimeProvider clock, Func<string, string?>? variable = null)
    {
        variable ??= Environment.GetEnvironmentVariable;
        var session = variable(ChildMarks.SessionVariable);
        var own = string.IsNullOrWhiteSpace(session) ? null : session.Trim();
        return new PriorityWorld(home, async (id, ct) =>
        {
            using var service = ServiceClient.FromEnvironment();
            return await FindIn(service)(id, ct).ConfigureAwait(false);
        }, clock.GetUtcNow, own is null ? "the person" : AgentVersionAct.PipeSessionActor(own)) { Session = own };
    }

    public static Func<string, CancellationToken, Task<PriorityTarget?>> FindIn(ServiceClient service) => (id, ct) => FindAsync(service, id, ct);

    private static async Task<PriorityTarget?> FindAsync(ServiceClient service, string id, CancellationToken ct)
    {
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
    public const string Usage = "usage: daoris priority <ask|quest> [<class> --why \"<words>\" [--until <time> | <n>m|h|d]]  (class: urgent, high, normal or low)";

    public static async Task<int> RunAsync(IReadOnlyList<string> args, PriorityWorld world, TextWriter said, CancellationToken ct)
    {
        if (Parse(args, world.Clock()) is not { } asked)
        {
            said.WriteLine(Usage);
            return 2;
        }

        if (asked.Class is null)
        {
            if (await world.Find(asked.Id, ct).ConfigureAwait(false) is not { } target)
            {
                said.WriteLine($"priority: {Nothing(asked.Id)}");
                return 1;
            }

            said.WriteLine(Standing(target, PriorityBook.Read(world.Home), world.Clock()));
            return 0;
        }

        try
        {
            said.WriteLine(await SetAsync(world, asked.Id, asked.Class, asked.Why!, asked.Until, ct).ConfigureAwait(false));
            return 0;
        }
        catch (PriorityRefused refused)
        {
            said.WriteLine($"priority: {refused.Message}");
            return 1;
        }
    }

    /// <summary>Set a class and say it standing: the one way the terminal and the window's press (<c>PRIORITY_SET</c>) set one; a session's is judged as <c>priority.set</c>.</summary>
    public static async Task<string> SetAsync(PriorityWorld world, string id, string @class, string why, DateTimeOffset? until, CancellationToken ct)
    {
        id = id.Trim().TrimStart('#');
        if (await world.Find(id, ct).ConfigureAwait(false) is not { } target) throw new PriorityRefused(Nothing(id));
        var now = world.Clock();
        if (world.Session is { } session && Judged(world, session, target, @class, now) is { } held) throw new PriorityRefused(held);
        var change = new PriorityEvent(now, target.Target, target.Id, target.Workspace, @class, why, world.By)
        {
            Until = until ?? (@class == PriorityClasses.Urgent ? now + world.Rules.UrgentLife : null), Run = target.Run,
        };
        if (PriorityBook.Set(world.Home, change, now, world.Rules) is { } refused) throw new PriorityRefused($"not set: {refused}");
        return Standing(target, PriorityBook.Read(world.Home), now);
    }

    private static string Nothing(string id) => $"`{id}` names no ask or quest here.";

    private static string? Judged(PriorityWorld world, string session, PriorityTarget target, string @class, DateTimeOffset now)
    {
        var actor = AgentVersionAct.PipeSessionActor(session);
        var subject = $"priority {target.Target} #{target.Id} {@class}";
        var workspace = target.Workspace is { Length: > 0 } named ? named : null;
        var verdict = world.Risk.Verdict(world.Home, RiskActs.PrioritySet, workspace, actor, subject);
        var passed = verdict.Outcome == RiskVerdict.Pass;
        world.Risk.Journal(world.Home, RiskActs.PrioritySet, workspace, verdict, new RiskLine(
            $"priority:{actor}/{subject}#{now.UtcTicks}", Release: true, actor, subject,
            passed ? "set: passed by the risk policy" : "nothing set here: it waits on its holder's yes", !passed, now));
        return passed
            ? null
            : $"not set: this terminal is a session's, and the risk policy classes `{RiskActs.PrioritySet}` `{verdict.Class}` at "
              + $"{(workspace is null ? "this machine" : $"the `{workspace}` workspace")}: it waits on {AgentVersionAct.Holder(verdict)}'s yes — "
              + $"{verdict.Why}. Set it in Daoris's window, or from the owner's own terminal.";
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

    private sealed record PriorityWords(string Id, string? Class, string? Why, DateTimeOffset? Until);

    private static PriorityWords? Parse(IReadOnlyList<string> args, DateTimeOffset now)
    {
        if (args.Count == 0 || args[0].StartsWith('-') || args[0].TrimStart('#').Length == 0) return null;
        var id = args[0].TrimStart('#');
        if (args.Count == 1) return new PriorityWords(id, null, null, null);
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
    public static DateTimeOffset? Time(string word, DateTimeOffset now)
    {
        var unit = word.Length > 1 ? word[^1] : ' ';
        if (unit is 'm' or 'h' or 'd' && int.TryParse(word[..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count > 0)
        {
            return now + (unit == 'm' ? TimeSpan.FromMinutes(count) : unit == 'h' ? TimeSpan.FromHours(count) : TimeSpan.FromDays(count));
        }

        return word.Contains('T') && DateTimeOffset.TryParse(word, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at) ? at : null;
    }
}

/// <summary>A class not set, with the words that say why: the id names nothing, the risk policy holds it, or the book refused it.</summary>
public sealed class PriorityRefused(string message) : DriverException(message);
