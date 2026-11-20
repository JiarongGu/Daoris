using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// What admits a check on this machine: the person's <c>checks</c> (<see cref="ChecksSetting"/>; null is <c>auto</c>), the processors
/// and the places the machine's heavy-run admission holds (<see cref="MachineAdmission.Capacity"/>), and the memory read now.
/// </summary>
/// <param name="Setting">A pinned number of checks at once, or null for <c>auto</c>.</param>
/// <param name="Processors">The machine's processors.</param>
/// <param name="Machine">How many heavy runs the machine's admission holds at once.</param>
/// <param name="Memory">The machine's free memory, read where <c>auto</c> asks it; null where it cannot be read.</param>
public sealed record CheckRules(int? Setting, int Processors, int Machine, Func<MemoryReading?> Memory)
{
    /// <summary>
    /// The most checks <c>auto</c> runs at once: a quarter of the processors, a check's build and suites keeping about four busy, within
    /// the machine's admission, whose places each check's steps take as well, and one at least.
    /// </summary>
    public int Ceiling => Math.Max(1, Math.Min(Processors / 4, Machine));

    /// <summary>The slots there are: the pinned number, else the ceiling.</summary>
    public int Slots => Setting ?? Ceiling;
}

/// <summary>What a check waits for before it takes a slot, by code, with the values its words name.</summary>
/// <param name="Code"><see cref="Slots"/>, <see cref="Memory"/> or <see cref="Repository"/>.</param>
public sealed record CheckWait(string Code, IReadOnlyDictionary<string, string> Values)
{
    /// <summary>Every slot runs a check: <c>running</c> of <c>of</c>.</summary>
    public const string Slots = "slots";

    /// <summary>Another check would leave too little memory: <c>free</c> and <c>need</c> in GB, and <c>starting</c> where some still start.</summary>
    public const string Memory = "memory";

    /// <summary>Heavy runs of its own repository hold every slot of the repository's: <c>what</c> names them.</summary>
    public const string Repository = "repository";

    /// <summary>The wait as the driver's log and the waiter's file say it, after "waiting for".</summary>
    public string Sentence => Code switch
    {
        Slots => $"a slot: {Values["running"]} of {Values["of"]} run",
        Memory => $"memory: {Values["free"]} GB free, a check needs {Values["need"]} GB"
                  + (Values.TryGetValue("starting", out var starting) ? $" with {starting} still starting" : ""),
        _ => Values.GetValueOrDefault("what") ?? "a heavy run of its repository",
    };

    public static CheckWait OfSlots(int running, int of) => new(Slots, new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["running"] = running.ToString(CultureInfo.InvariantCulture), ["of"] = of.ToString(CultureInfo.InvariantCulture),
    });

    public static CheckWait OfRepository(string repository, string what) =>
        new(Repository, new Dictionary<string, string>(StringComparer.Ordinal) { ["repository"] = repository, ["what"] = what });
}

/// <summary>
/// The machine's check slots: how many checks run at once, as the home's verification slots (<see cref="VerifySlot"/>) hold them, and
/// why one waits. <c>checks</c> pinned is that many slots. <c>auto</c>, the default, admits the first check always (as the one slot did)
/// and another while the free physical memory and the free commit both stay at or above what a check needs, up to the ceiling
/// (<see cref="CheckRules.Ceiling"/>); memory that cannot be read admits one. A check's need is the merge tool's for a suite
/// (<c>MEMORY_NEEDS.suite</c>, 6 GB: a build with MSBuild's nodes and a compiler server and a test host), an estimate on the safe side
/// since a check's records keep no peak to measure; a check that took its slot within <see cref="Starting"/> has not reached its peak, so
/// its need is counted again, and two never climb to their peaks on one reading.
/// </summary>
/// <remarks>
/// The machine's heavy-run admission stays the bound on the machine as a whole: each step of a check takes a place in it
/// (<see cref="MachineAdmission"/>), beside the merge tool's gates and the sessions' heavy runs, so the ceiling is held within its
/// capacity and nothing together runs more than it admits. A repository's heavy-run slots (<see cref="HeavySlot"/>) are as many as these,
/// and a session's heavy run is judged by the same rule over its repository's holders, so two checks of one repository run side by side.
/// </remarks>
public static class CheckAdmission
{
    /// <summary>The free memory a check needs before it starts beside another: the merge tool's need for a suite.</summary>
    public const long Need = 6L << 30;

    /// <summary>How long a check that took its slot is counted as still starting, before its build and suites reach their peak.</summary>
    public static readonly TimeSpan Starting = TimeSpan.FromMinutes(2);

    /// <summary>Why a run waits beside the runs holding a slot now, by when each took it, or null where it may take one.</summary>
    public static CheckWait? Judge(IReadOnlyList<DateTimeOffset> held, CheckRules rules, DateTimeOffset now)
    {
        var slots = rules.Slots;
        if (held.Count == 0) return null;
        if (held.Count >= slots) return CheckWait.OfSlots(held.Count, slots);
        if (rules.Setting is not null) return null;
        if (rules.Memory()?.Least is not { } free) return CheckWait.OfSlots(held.Count, 1);
        var starting = held.Count(since => now - since < Starting);
        var need = Need * (1 + starting);
        if (free >= need) return null;
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["free"] = (free / (double)(1L << 30)).ToString("F1", CultureInfo.InvariantCulture),
            ["need"] = (need / (double)(1L << 30)).ToString("0.#", CultureInfo.InvariantCulture),
        };
        if (starting > 0) values["starting"] = starting.ToString(CultureInfo.InvariantCulture);
        return new CheckWait(CheckWait.Memory, values);
    }

    /// <summary>
    /// A slot, its holder said in its record at once, or why there is none: judged over the slots' records, then taken by the first of
    /// the slots' locks that is free; every lock held is a wait for a slot. Never waits.
    /// </summary>
    public static (VerifySlot? Slot, CheckWait? Wait) TryTake(
        string home, CheckRules rules, DateTimeOffset now, Func<VerifyHolder> holder, Func<int, long, bool>? alive = null)
    {
        if (Judge([.. VerifySlot.Look(home, alive).Holders.Select(holder => holder.Since)], rules, now) is { } wait) return (null, wait);
        if (VerifySlot.TryTake(home, rules.Slots) is not { } slot) return (null, CheckWait.OfSlots(rules.Slots, rules.Slots));
        try
        {
            slot.Keep(holder());
        }
        catch
        {
            slot.Dispose();
            throw;
        }

        return (slot, null);
    }
}
