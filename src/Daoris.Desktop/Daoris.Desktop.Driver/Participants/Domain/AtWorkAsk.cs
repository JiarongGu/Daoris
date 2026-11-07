using System.Text.Json.Serialization;

namespace Daoris.Driver;

/// <summary>One line of work of an ask as the groups slice holds it: its repository, the part its row opens and its parts in the strip's order.</summary>
/// <param name="Shown">The word its leading part shows.</param>
public sealed record AtWorkAskEntryIn(string Entry, string Repository, string Opens, string Shown, IReadOnlyList<string> Parts);

public sealed record AtWorkAskOf(string Ask);

/// <summary>One of an ask's lines of work folded to a count, the entry's own view being where its parts are.</summary>
public sealed record AtWorkAskEntry(string Entry, string Repository, string Opens, string Shown, int Parts)
{
    /// <summary>The parts whose record says they are working.</summary>
    public int Working { get; init; }

    /// <summary>The subagents of its parts read here, and how many work.</summary>
    public int Subagents { get; init; }

    public int SubagentsWorking { get; init; }

    /// <summary>A teammate's part is among them: its subagents are not visible on this machine, so the counts are of what is.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Elsewhere { get; init; }
}

/// <summary>
/// What is at work on an ask: its lines of work, each folded to a count, and its quests. No attended part
/// and no subagents of its own: the entries' views are where those are.
/// </summary>
public sealed record AtWorkAskAnswer(AtWorkAskOf Of, IReadOnlyList<AtWorkAskEntry> Entries)
{
    /// <summary>The entries past the bound.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MoreEntries { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AtWorkAskedQuest>? Quests { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MoreQuests { get; init; }
}

/// <summary>
/// Who is at work on an ask: the one function the window's read and the terminal's verb share, over plain values as <see cref="AtWork"/> is.
/// The asks slice is an existence dependency: an ask it does not hold is not an empty ask, and one it held and no longer does is gone.
/// </summary>
public static class AtWorkAsk
{
    /// <summary>Pending: the slices have judged and the asks hold no such ask (unknown, or deleted after it is opened).</summary>
    public const string WaitsAsk = "ask";

    /// <param name="ask">The ask.</param>
    /// <param name="known">Whether the asks slice holds it.</param>
    /// <param name="entries">Its lines of work, as the groups slice holds them.</param>
    /// <param name="record">A record's read fields; null where the sessions slice holds none.</param>
    /// <param name="lineage">A part's lineage reading; never asked for a teammate's part.</param>
    /// <param name="asked">The quests half over the stores the layer above holds.</param>
    public static AtWorkHeld Compose(
        string ask, bool known, IReadOnlyList<AtWorkAskEntryIn> entries, Func<string, AtWorkRecord?> record, Func<AtWorkRecord, LineageReading> lineage,
        SliceHealth groups, SliceHealth sessions, SliceHealth quests, SliceHealth asks, Func<AtWorkAsked> asked)
    {
        SliceHealth[] inputs = [groups, sessions, quests, asks];
        foreach (var health in inputs)
        {
            if (health.Judged) continue;
            return health.Failing is { } why ? new AtWorkHeld { Failed = new AtWorkWhy(why) } : new AtWorkHeld { Pending = true, Waits = AtWork.WaitsSlices };
        }

        if (!known) return new AtWorkHeld { Pending = true, Waits = WaitsAsk };

        var rows = entries.Take(AtWork.Bound).Select(entry => Fold(entry, record, lineage)).ToList();
        var half = asked();
        var stale = groups.Failing ?? sessions.Failing ?? quests.Failing ?? asks.Failing;
        return new AtWorkHeld
        {
            AskAnswer = new AtWorkAskAnswer(new AtWorkAskOf(ask), rows)
            {
                MoreEntries = entries.Count > rows.Count ? entries.Count - rows.Count : null,
                Quests = half.Quests.Count > 0 ? half.Quests : null,
                MoreQuests = half.More > 0 ? half.More : null,
            },
            Stale = stale is null ? null : new AtWorkWhy(stale),
        };
    }

    private static AtWorkAskEntry Fold(AtWorkAskEntryIn entry, Func<string, AtWorkRecord?> record, Func<AtWorkRecord, LineageReading> lineage)
    {
        int working = 0, subagents = 0, subagentsWorking = 0;
        var elsewhere = false;
        foreach (var id in entry.Parts.Distinct(StringComparer.Ordinal))
        {
            var held = record(id);
            if (held?.State == "working") working++;
            if (AtWork.Teammate(id))
            {
                elsewhere = true;
                continue;
            }

            if (held is null) continue;
            var bindings = lineage(held).Bindings;
            subagents += bindings.Children.Count;
            subagentsWorking += AtWork.Working(bindings);
        }

        return new AtWorkAskEntry(entry.Entry, entry.Repository, entry.Opens, entry.Shown, entry.Parts.Distinct(StringComparer.Ordinal).Count())
        {
            Working = working,
            Subagents = subagents,
            SubagentsWorking = subagentsWorking,
            Elsewhere = elsewhere ? true : null,
        };
    }
}
