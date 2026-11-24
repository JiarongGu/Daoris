using System.Globalization;

namespace Daoris.Driver;

/// <summary>A heavy thing the machine already holds for Daoris: whose project and agent, what kind, what it needs and whether its first gate has not ended.</summary>
public sealed record RoomHold(string Project, string? Agent, string Kind, LoadCost Need, bool Starting, string Who);

/// <summary>What is asked: the project and agent asking, the kind, the need (learned or declared) and the repository's rule for the kind.</summary>
public sealed record RoomAsk(string Project, string? Agent, string Kind, LoadCost Need, string Source, KindRule Rule)
{
    /// <summary>The project as a person reads it (the repository's name); the key where none is given.</summary>
    public string? Name { get; init; }
}

/// <summary>
/// The machine as the room judges it at one look: free memory now, the floor the person set, the measured headroom, the concurrency
/// limit and the signal behind it, the processors' busy, and which projects and agents have asked lately.
/// </summary>
/// <param name="Free">The lower of free physical memory and free commit, or null where neither reads: what a run's need is judged against.</param>
/// <param name="Basis">Free commit (the physical memory where no commit is read): what the person's kept room is judged against, as the reserve is.</param>
/// <param name="Floor">What the person's setting keeps free at least, bytes.</param>
/// <param name="Person">The person's measured room.</param>
/// <param name="Limit">How many heavy things Daoris holds at once now.</param>
/// <param name="Signal">The signal that last moved the limit (<see cref="RoomLimit"/>).</param>
/// <param name="Busy">The processors' busy, null where unread.</param>
/// <param name="Recent">The projects and (as <c>project|agent</c>) agents that have asked inside <see cref="RoomJudge.Fair"/>, with when.</param>
public sealed record RoomView(
    long? Free, long? Basis, long Floor, PersonRoom Person, int Limit, string Signal, double? Busy, IReadOnlyDictionary<string, DateTimeOffset> Recent, DateTimeOffset Now)
{
    /// <summary>What is kept free: the larger of the person's floor and their measured headroom.</summary>
    public long Keep => Math.Max(Floor, Person.Headroom);

    /// <summary>Which of the two sets <see cref="Keep"/>.</summary>
    public string KeepBy => Floor >= Person.Headroom ? "floor" : "headroom";
}

/// <summary>
/// The room's admission: a pure judgement of one ask over the machine's view and what it already holds. Never reads
/// a file or a clock; <see cref="RoomLedger"/> does, and hands this its view.
/// </summary>
public static class RoomJudge
{
    /// <summary>How long a project or agent that has asked still holds a seat of the limit for itself: a waiting run asks again every few seconds, so a project that is waiting keeps its seat and one that has gone quiet loses it within two minutes.</summary>
    public static readonly TimeSpan Fair = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Why the ask waits, or null where it may start. In order: its repository's bound for the kind, an agent's bound, the fair share of
    /// the project and the agent, the concurrency limit, then free memory against its need beyond what is kept. <paramref name="pinned"/> is
    /// the person's <c>checks</c> pin, which overrides the backed-off limit and the need judged against physical memory, as it always has.
    /// </summary>
    public static CheckWait? Decide(RoomView view, IReadOnlyList<RoomHold> holds, RoomAsk ask, int? pinned)
    {
        var ofProject = holds.Where(hold => hold.Project == ask.Project).ToList();
        var ofKind = ofProject.Where(hold => hold.Kind == ask.Kind).ToList();
        if (ask.Rule.Max is { } most && ofKind.Count >= most)
            return Share(ask, $"its repository's {most} {ask.Kind}{(most == 1 ? "" : "s")} at once, held by {Names(ofKind)}");
        if (ask.Rule.PerAgent is { } each && ask.Agent is not null && ofKind.Count(hold => hold.Agent == ask.Agent) >= each)
            return Share(ask, $"this agent's {each} {ask.Kind}{(each == 1 ? "" : "s")}, which it already holds: {Names(ofKind.Where(hold => hold.Agent == ask.Agent))}");

        var limit = pinned ?? view.Limit;
        if (holds.Count >= limit && holds.Count > 0) return CheckWait.OfSlots(holds.Count, limit);

        var others = Others(view, holds, ask);
        var projectCap = Math.Max(1, limit - others.Projects);
        var agentCap = Math.Max(1, projectCap - others.Peers);
        if (others.Projects > 0 && ofProject.Count >= projectCap)
            return Share(ask, $"a fair share: {ofProject.Count} of {limit} places, with {others.Projects} other {(others.Projects == 1 ? "project" : "projects")} asking");
        if (others.Peers > 0 && ask.Agent is not null && ofProject.Count(hold => hold.Agent == ask.Agent) >= agentCap)
            return Share(ask, $"a fair share of the project's {projectCap}: this agent holds {agentCap}, with {others.Peers} other {(others.Peers == 1 ? "agent" : "agents")} asking");

        return Memory(view, holds, ask, pinned is not null);
    }

    private static CheckWait? Memory(RoomView view, IReadOnlyList<RoomHold> holds, RoomAsk ask, bool pinned)
    {
        // The first run asks only the person's floor, and a pinned `checks` overrides the measured room: only the floor is kept.
        var first = holds.Count == 0;
        var kept = first || pinned ? view.Floor : view.Keep;
        if (kept == 0 && (first || pinned)) return null;
        if (view.Free is not { } least) return first || pinned ? null : CheckWait.OfSlots(holds.Count, Math.Max(holds.Count, 1));
        var starting = holds.Where(hold => hold.Starting).ToList();
        var pending = starting.Sum(hold => hold.Need.Memory);
        var need = ask.Need.Memory;
        var basis = view.Basis ?? least;
        var shortNeed = !first && !pinned && least - pending < need;
        if (!shortNeed && basis - pending - kept >= need) return null;
        var values = new Dictionary<string, string>(StringComparer.Ordinal) { ["free"] = Gb(shortNeed ? least : basis), ["need"] = Gb(need + pending, "0.#") };
        if (starting.Count > 0) values["starting"] = starting.Count.ToString(CultureInfo.InvariantCulture);
        if (shortNeed) return new CheckWait(CheckWait.Memory, values);
        values["reserve"] = Gb(kept, "0.#");
        return new CheckWait(CheckWait.Reserve, values);
    }

    /// <summary>The other projects, and the other agents of this ask's project, that hold something or have asked inside <see cref="Fair"/>.</summary>
    private static (int Projects, int Peers) Others(RoomView view, IReadOnlyList<RoomHold> holds, RoomAsk ask)
    {
        var projects = new HashSet<string>(StringComparer.Ordinal);
        var agents = new HashSet<string>(StringComparer.Ordinal);
        foreach (var hold in holds)
        {
            if (hold.Project != ask.Project) projects.Add(hold.Project);
            else if (hold.Agent is not null && hold.Agent != ask.Agent) agents.Add(hold.Agent);
        }

        foreach (var (key, at) in view.Recent)
        {
            if (view.Now - at > Fair) continue;
            var cut = key.IndexOf('|', StringComparison.Ordinal);
            if (cut < 0)
            {
                if (key != ask.Project) projects.Add(key);
            }
            else if (key[..cut] == ask.Project && key[(cut + 1)..] != ask.Agent)
            {
                agents.Add(key[(cut + 1)..]);
            }
        }

        return (projects.Count, agents.Count);
    }

    private static CheckWait Share(RoomAsk ask, string what) => CheckWait.OfRepository(ask.Name ?? ask.Project, what);

    private static string Names(IEnumerable<RoomHold> holds) => string.Join(", ", holds.Select(hold => hold.Who));

    internal static string Gb(long bytes, string format = "F1") =>
        format == "F1" ? (Math.Floor(bytes * 10.0 / (1L << 30)) / 10).ToString("F1", CultureInfo.InvariantCulture) : (bytes / (double)(1L << 30)).ToString(format, CultureInfo.InvariantCulture);
}
