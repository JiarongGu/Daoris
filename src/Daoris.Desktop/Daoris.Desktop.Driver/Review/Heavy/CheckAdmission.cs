using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// What admits a heavy run on this machine: the person's <c>checks</c> (<see cref="ChecksSetting"/>; null is <c>auto</c>), the processors
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

    /// <summary>
    /// The slots there are: the pinned number, else the ceiling; never more than the machine's admission holds, since every step of a
    /// heavy run takes a place there, and a slot past them would only hold a run waiting for a place while its bound runs.
    /// </summary>
    public int Slots => Math.Min(Setting ?? Ceiling, Math.Max(1, Machine));

    /// <summary>
    /// The rules as every heavy run reads them, built here alone: the setting it is handed, and the machine's capacity (<paramref name="capacity"/>
    /// where a test holds it, else <see cref="MachineAdmission.CapacityOf"/> over <paramref name="variable"/> and the processors).
    /// </summary>
    public static CheckRules Of(int? setting, int processors, int? capacity, Func<string, string?> variable, Func<MemoryReading?> memory) =>
        new(setting, processors, capacity ?? MachineAdmission.CapacityOf(variable, processors), memory);
}

/// <summary>What a run waits for before it takes a slot, by code, with the values its words name.</summary>
/// <param name="Code"><see cref="Slots"/>, <see cref="Memory"/> or <see cref="Repository"/>.</param>
public sealed record CheckWait(string Code, IReadOnlyDictionary<string, string> Values)
{
    /// <summary>Every slot runs a heavy run: <c>running</c> of <c>of</c>.</summary>
    public const string Slots = "slots";

    /// <summary>Another run would leave too little memory: <c>free</c> and <c>need</c> in GB, and <c>starting</c> where some still start.</summary>
    public const string Memory = "memory";

    /// <summary>Heavy runs of its own repository hold every slot the repository has: <c>what</c> names them.</summary>
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
/// A heavy run holding a slot on the machine, as the admission counts it: once, whether it holds a check slot, a repository's slot or both.
/// </summary>
/// <param name="Since">When it took its slot.</param>
/// <param name="Key">The repository whose heavy-run slot it holds, by its key; null for a check slot whose repository slot says nothing.</param>
/// <param name="Starting">Whether its first gate has not ended yet, so its peak may be still to come.</param>
/// <param name="Who">The run, as a wait names it.</param>
public sealed record HeavyHeld(DateTimeOffset Since, string? Key, bool Starting, string Who);

/// <summary>
/// What a heavy run asks the admission for: the repository's slot it runs under (<paramref name="Key"/>, its repository's name and the
/// checkout whose <c>daoris.gates.json</c> may say how many at once, <see cref="HeavyGates.SlotsOf"/>), and the record it says itself in.
/// </summary>
public sealed record HeavyAsk(string Key, string Repository, string Checkout, Func<HeavyHolder> Holder)
{
    /// <summary>The check slot's record, where the run takes one (a check, an integration's gated line); null for a session's or a terminal's run.</summary>
    public Func<VerifyHolder>? Check { get; init; }
}

/// <summary>What the admission handed a run: its check slot where it asked one, its repository's slot, and the record written beside it.</summary>
public sealed record HeavyTaken(VerifySlot? Check, HeavySlot Repository, HeavyHolder Holder) : IDisposable
{
    /// <summary>Let go of both: the repository's slot first, as a run lets go of what it took last.</summary>
    public void Dispose()
    {
        Repository.Dispose();
        Check?.Dispose();
    }
}

/// <summary>
/// The machine's admission of heavy runs: how many run at once, and why one waits. One judgement over every heavy run on the machine,
/// read from the records beside the slots (<see cref="Holding"/>): the checks' and the integration's check slots (<see cref="VerifySlot"/>)
/// and every repository's heavy-run slots (<see cref="HeavySlot"/>), a run holding both counted once. <c>checks</c> pinned is that many,
/// within the machine's admission (<see cref="CheckRules.Slots"/>). <c>auto</c>, the default, admits the first run always (as the one slot
/// did) and another while the free physical memory and the free commit both stay at or above what a run needs, up to the ceiling
/// (<see cref="CheckRules.Ceiling"/>); memory that cannot be read admits one.
/// </summary>
/// <remarks>
/// <para><b>A run's need</b> is the merge tool's for a Process suite (<c>MEMORY_NEEDS.process</c>, 8 GB), not its 6 GB for a suite: a check
/// runs the classes its change reaches, the Process classes among them, each starting real hosts and git beside a build. It is an estimate on
/// the safe side, since a check's records keep no peak to measure.</para>
///
/// <para><b>Still starting until its first gate ends.</b> A run whose first gate has not ended may not have reached its peak, so its need is
/// counted again and two never climb to their peaks on one reading. The first gate's end is a fact its record keeps (<see cref="HeavyHolder.Now"/>
/// past the first, or <see cref="HeavyHolder.Failed"/>); a time window would be a guess the machine's load stretches, and a Daoris check runs
/// one gate (its <c>reached</c> runner) whose build and suites both run inside it, so no window could tell its build from its suites. Such a
/// run is counted as starting for its whole run: a second starts beside it with room for both its own need and the first one's.</para>
///
/// <para><b>Judged, taken and said under one lock.</b> The judgement, the take of each slot and the write of each record happen while this
/// process holds the home's admission lock (<c>locks/heavy/admitting.lock</c>, opened alone and let go with its handle, so a process that
/// dies lets go of it). No second run, in this process or another, judges between one run's judgement and its record, so two starts a few
/// milliseconds apart never both see no holder. The lock is held for a judgement's milliseconds; a run that cannot have it in
/// <see cref="AdmittingTries"/> tries waits as for a slot and looks again.</para>
///
/// <para><b>A repository's own slots</b> are as many as the machine's, or fewer where its <c>daoris.gates.json</c> says so
/// (<see cref="HeavyGates.SlotsKey"/>): a run waits while its repository's runs hold that many. Each step of a run takes a place in the
/// machine's heavy-run admission besides (<see cref="MachineAdmission"/>), beside the merge tool's gates, so the slots are held within its
/// capacity and nothing together runs more than it admits.</para>
/// </remarks>
public static class CheckAdmission
{
    /// <summary>The free memory a run needs before it starts beside another: the merge tool's need for a Process suite.</summary>
    public const long Need = 8L << 30;

    /// <summary>
    /// How often a run tries for the admission's lock before it waits as for a slot and looks again: spun, the later tries a millisecond's
    /// sleep or more apart, which is far past a judgement's milliseconds.
    /// </summary>
    public const int AdmittingTries = 400;

    /// <summary>Where the admission's lock is, beside the repositories' slots.</summary>
    public static string AdmittingPath(string home) => DaorisHome.Entry(home, HomeEntry.Locks, "heavy", "admitting.lock");

    /// <summary>
    /// Every heavy run holding a slot on the machine, the first taken first: each repository slot's holder, and each check slot's holder its
    /// repository slot does not already name (the same process, start and run), each only where its process runs. Reads files only.
    /// </summary>
    public static IReadOnlyList<HeavyHeld> Holding(string home, Func<int, long, bool>? alive = null)
    {
        var checks = VerifySlot.Look(home, alive).Holders.ToList();
        var held = new List<HeavyHeld>();
        foreach (var (key, holder) in HeavySlot.Holders(home, alive))
        {
            var at = holder.Run is { } run ? checks.FindIndex(check => check.Pid == holder.Pid && check.Started == holder.Started && check.Run == run) : -1;
            var since = at >= 0 ? checks[at].Since : holder.Since;
            if (at >= 0) checks.RemoveAt(at);
            held.Add(new HeavyHeld(since, key, Starting(holder), Who(holder)));
        }

        held.AddRange(checks.Select(check => new HeavyHeld(check.Since, null, true, $"the verification run {check.Run}")));
        return [.. held.OrderBy(one => one.Since)];
    }

    /// <summary>Whether a holder's first gate has not ended: none started, or the first still running, with no failure said.</summary>
    public static bool Starting(HeavyHolder holder) => holder.Failed is null && holder.Now is not { At: > 0 };

    /// <summary>A holder as a wait names it.</summary>
    public static string Who(HeavyHolder holder) =>
        holder.Run is { } run ? $"the verification run {run}" : holder.Session is { } session ? $"session {session}'s heavy run" : "a heavy run from a terminal";

    /// <summary>Why a run waits beside the runs holding a slot on the machine now, or null where it may take one.</summary>
    public static CheckWait? Judge(IReadOnlyList<HeavyHeld> held, CheckRules rules)
    {
        var slots = rules.Slots;
        if (held.Count == 0) return null;
        if (held.Count >= slots) return CheckWait.OfSlots(held.Count, slots);
        if (rules.Setting is not null) return null;
        if (rules.Memory()?.Least is not { } free) return CheckWait.OfSlots(held.Count, Math.Max(held.Count, 1));
        var starting = held.Count(one => one.Starting);
        var need = Need * (1 + starting);
        if (free >= need) return null;
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Floored, so a reading just short of the need never reads as the need itself.
            ["free"] = (Math.Floor(free * 10.0 / (1L << 30)) / 10).ToString("F1", CultureInfo.InvariantCulture),
            ["need"] = (need / (double)(1L << 30)).ToString("0.#", CultureInfo.InvariantCulture),
        };
        if (starting > 0) values["starting"] = starting.ToString(CultureInfo.InvariantCulture);
        return new CheckWait(CheckWait.Memory, values);
    }

    /// <summary>The slots a repository has: the machine's, or fewer where its checkout's file says so.</summary>
    public static int SlotsOf(CheckRules rules, string checkout) => Math.Min(rules.Slots, HeavyGates.SlotsOf(checkout) ?? int.MaxValue);

    /// <summary>
    /// A run's slots and records, or why it has none: under the admission's lock, its repository's own slots judged, then the machine's
    /// (<see cref="Judge"/>), then its check slot taken where it asks one and its repository's slot, each record written as its slot is
    /// taken. Never waits past <see cref="AdmittingTries"/> tries for the lock.
    /// </summary>
    public static (HeavyTaken? Taken, CheckWait? Wait) TryTake(string home, CheckRules rules, HeavyAsk ask, Func<int, long, bool>? alive = null)
    {
        using var admitting = Admitting(home);
        var held = Holding(home, alive);
        if (admitting is null) return (null, Judge(held, rules) ?? CheckWait.OfSlots(held.Count, rules.Slots));

        var mine = SlotsOf(rules, ask.Checkout);
        var ours = held.Where(one => one.Key == ask.Key).ToList();
        if (ours.Count >= mine) return (null, CheckWait.OfRepository(ask.Repository, string.Join(", ", ours.Select(one => one.Who))));
        if (Judge(held, rules) is { } wait) return (null, wait);

        VerifySlot? check = null;
        try
        {
            if (ask.Check is { } checkHolder)
            {
                if (VerifySlot.TryTake(home, rules.Slots) is not { } taken) return (null, CheckWait.OfSlots(rules.Slots, rules.Slots));
                check = taken;
                check.Keep(checkHolder());
            }

            // A lock held with no record is a run between its take and its write, which this lock keeps apart, or one from before it.
            if (HeavySlot.TryTake(home, ask.Key, mine) is not { } slot)
            {
                check?.Dispose();
                return (null, CheckWait.OfRepository(ask.Repository, ours.Count > 0 ? string.Join(", ", ours.Select(one => one.Who)) : "a heavy run of its repository"));
            }

            try
            {
                var holder = ask.Holder();
                slot.Keep(holder);
                return (new HeavyTaken(check, slot, holder), null);
            }
            catch
            {
                slot.Dispose();
                throw;
            }
        }
        catch
        {
            check?.Dispose();
            throw;
        }
    }

    /// <summary>The admission's lock, tried <see cref="AdmittingTries"/> times, spinning between; null where another still holds it.</summary>
    private static FileStream? Admitting(string home)
    {
        var path = AdmittingPath(home);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var spin = new SpinWait();
        for (var tries = 0; tries < AdmittingTries; tries++)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException held) when (!OperatingSystem.IsWindows() || (held.HResult & 0xFFFF) is 32 or 33)
            {
                spin.SpinOnce();
            }
        }

        return null;
    }
}
