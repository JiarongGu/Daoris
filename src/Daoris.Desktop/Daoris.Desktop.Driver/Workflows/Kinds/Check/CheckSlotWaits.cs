using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// What a run's sent check waits for while the heavy executor has not started it: the machine's check slots
/// (<see cref="VerifySlot"/>, as many as <see cref="CheckAdmission"/> admits) and why it waits for one, by its waiter's file, with its
/// place in their queue. Read from the slots' files at each look and never stored, so a check whose gates run, one settled, or one not
/// sent waits on no slot.
/// </summary>
public static class CheckSlotWaits
{
    /// <summary>The wait's code where every slot runs a check: <c>running</c> of <c>slots</c>, and its place in the queue.</summary>
    public const string Full = "slot";

    /// <summary>The wait's code where another check would leave too little memory: <c>free</c> and <c>need</c> in GB, and its place.</summary>
    public const string Memory = "slot-memory";

    /// <summary>The wait's code where another check would leave less free than the reserve kept for the person's own work: <c>free</c>, <c>need</c> and <c>reserve</c> in GB, and its place.</summary>
    public const string Reserve = "slot-reserve";

    /// <summary>The wait's code where a heavy run of its own repository runs: <c>repository</c>, and its place.</summary>
    public const string Repository = "slot-repository";

    public const string Admitting = "slot-admitting";

    /// <summary>The wait's code where nothing is said yet (the next look takes a slot): its place in the queue.</summary>
    public const string Queued = "slot-queued";

    /// <summary>Every code a check's slots wait under, as the check kind declares them.</summary>
    public static IReadOnlyList<string> Codes { get; } = [Full, Memory, Reserve, Repository, Admitting, Queued];

    /// <summary>The slots' waits of a run's graph, read from the home's slots only where a check of its record is under way.</summary>
    public static IReadOnlyDictionary<string, WorkflowRunWait> Read(string home, NodeGraph graph, NodeRecord? record) =>
        record is not null && graph.Nodes.Any(node => node.Kind == NodeKinds.Check && record.Statuses.GetValueOrDefault(node.Id)?.State == NodeStates.Active)
            ? Of(graph, record, VerifySlot.Look(home))
            : new Dictionary<string, WorkflowRunWait>(StringComparer.Ordinal);

    /// <summary>The wait of each active check of <paramref name="record"/> whose attempt out now is among the slots' waiters, by node.</summary>
    public static IReadOnlyDictionary<string, WorkflowRunWait> Of(NodeGraph graph, NodeRecord? record, VerifyLook look)
    {
        var waits = new Dictionary<string, WorkflowRunWait>(StringComparer.Ordinal);
        if (record is null || look.Waiters.Count == 0) return waits;
        foreach (var node in graph.Nodes.Where(each => each.Kind == NodeKinds.Check))
        {
            var status = WorkPerformer.StatusOf(record, node.Id);
            if (status.State != NodeStates.Active || record.Newest(node.Id) is not { State: AttemptStates.Recorded or AttemptStates.Sent } attempt
                || attempt.Attempt != status.Attempt)
            {
                continue;
            }

            var at = look.Waiters.Select((waiter, index) => (waiter, index)).FirstOrDefault(each => each.waiter.Invocation == attempt.Call.Invocation);
            if (at.waiter is null) continue;
            var values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["position"] = (at.index + 1).ToString(CultureInfo.InvariantCulture), ["of"] = look.Waiters.Count.ToString(CultureInfo.InvariantCulture),
                ["ahead"] = at.index.ToString(CultureInfo.InvariantCulture),
            };
            Place(values, "", at.waiter);
            foreach (var (ahead, index) in look.Waiters.Take(at.index).Select((ahead, index) => (ahead, index)))
            {
                var prefix = $"ahead.{index + 1}.";
                values[prefix + "run"] = ahead.Run;
                Place(values, prefix, ahead);
            }

            var said = at.waiter.Wait;
            var code = said?.Code switch
            {
                CheckWait.Slots => Full,
                CheckWait.Memory => Memory,
                CheckWait.Reserve => Reserve,
                CheckWait.Repository => Repository,
                CheckWait.Admitting => Admitting,
                _ => Queued,
            };
            if (code == Full)
            {
                values["running"] = said!.Values.GetValueOrDefault("running") ?? "?";
                values["slots"] = said.Values.GetValueOrDefault("of") ?? "?";
            }
            else if (code == Memory)
            {
                values["free"] = said!.Values.GetValueOrDefault("free") ?? "?";
                values["need"] = said.Values.GetValueOrDefault("need") ?? "?";
                values["basis"] = said.Values.GetValueOrDefault("source") ?? "?";
            }
            else if (code == Reserve)
            {
                values["free"] = said!.Values.GetValueOrDefault("free") ?? "?";
                values["need"] = said.Values.GetValueOrDefault("need") ?? "?";
                values["basis"] = said.Values.GetValueOrDefault("source") ?? "?";
                values["reserve"] = said.Values.GetValueOrDefault("reserve") ?? "?";
            }
            else if (code == Repository)
            {
                values["repository"] = said!.Values.GetValueOrDefault("repository") ?? at.waiter.Repository;
            }

            waits[node.Id] = new WorkflowRunWait(code, values, at.waiter.Since);
        }

        return waits;
    }

    private static void Place(Dictionary<string, string> values, string prefix, VerifyWaiter waiter)
    {
        values[prefix + "class"] = waiter.Class;
        values[prefix + "source"] = waiter.Source;
        if (waiter.By is { } by) values[prefix + "by"] = by;
        if (waiter.Why is { } why) values[prefix + "why"] = why;
    }

    /// <summary>A class as the terminal says it, with where it comes from: <c>urgent (from its ask, set by the person: why)</c>; a class none set is its word alone.</summary>
    private static string Class(WorkflowRunWait wait, string prefix)
    {
        var @class = wait.Values.GetValueOrDefault(prefix + "class") ?? PriorityClasses.Normal;
        if (wait.Values.GetValueOrDefault(prefix + "by") is not { } by) return @class;
        var from = wait.Values.GetValueOrDefault(prefix + "source") == PriorityBook.Ask ? "from its ask, " : "";
        return $"{@class} ({from}set by {by}: {wait.Values.GetValueOrDefault(prefix + "why")})";
    }

    /// <summary>The terminal's words for a slot's wait: why it waits, its place and class in the queue, and each check ahead with its class.</summary>
    public static string Said(WorkflowRunWait wait)
    {
        string Value(string name) => wait.Values.GetValueOrDefault(name) ?? "?";
        var own = wait.Values.ContainsKey("by") ? $", as {Class(wait, "")}" : "";
        var count = int.TryParse(wait.Values.GetValueOrDefault("ahead"), NumberStyles.None, CultureInfo.InvariantCulture, out var ahead) ? ahead : 0;
        var before = count == 0
            ? ""
            : "; ahead: " + string.Join("; ", Enumerable.Range(1, count).Select(at => $"run {Value($"ahead.{at}.run")}, {Class(wait, $"ahead.{at}.")}"));
        var queue = $"{Value("position")} of {Value("of")} in its queue{own}{before}";
        return wait.Code switch
        {
            Full => $"Waits for a check slot: {Value("running")} of {Value("slots")} run; {queue}.",
            Memory => $"Waits for memory: {Value("free")} GB free, the next check needs {Value("need")} GB ({Value("basis")}); {queue}.",
            Reserve => $"Waits for the reserve kept for your own work: {Value("free")} GB free, the next check needs {Value("need")} GB ({Value("basis")}) and {Value("reserve")} GB stays free; {queue}.",
            Repository => $"Waits for a heavy-run slot of {Value("repository")}: its other heavy runs hold every one it has; {queue}.",
            Admitting => $"Waits for the admission's lock, which another start holds; {queue}.",
            _ => $"Waits for a check slot: {queue}.",
        };
    }
}
