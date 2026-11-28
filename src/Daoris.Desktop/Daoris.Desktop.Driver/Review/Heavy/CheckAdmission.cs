using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// What admits a heavy run on this machine: the person's <c>checks</c> (<see cref="ChecksSetting"/>; null is <c>auto</c>), the processors
/// and the places the machine's heavy-run admission holds (<see cref="MachineAdmission.Capacity"/>), and the memory read now.
/// </summary>
/// <param name="Setting">A pinned number of checks at once, or null for <c>auto</c>.</param>
/// <param name="Processors">The machine's processors.</param>
/// <param name="Machine">How many heavy runs the machine's admission holds at once.</param>
/// <param name="Memory">The machine's free memory, read where <c>auto</c> asks it and for the reserve; null where it cannot be read.</param>
/// <param name="ReserveGb">The GB kept free at least for the person's own work (<see cref="ReserveSetting"/>, the floor under the measured headroom); null is <c>auto</c>, no floor.</param>
public sealed record CheckRules(int? Setting, int Processors, int Machine, Func<MemoryReading?> Memory, int? ReserveGb = null)
{
    /// <summary>The machine's seams the room reads beside the memory: the clock, the processors and Daoris's own share; this machine's unless a test holds them.</summary>
    public RoomWorld World { get; init; } = RoomWorld.Machine;

    public long FloorOf() => ReserveSetting.Bytes(ReserveGb);

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
    public static CheckRules Of(int? setting, int processors, int? capacity, Func<string, string?> variable, Func<MemoryReading?> memory, int? reserveGb = null) =>
        new(setting, processors, capacity ?? MachineAdmission.CapacityOf(variable, processors), memory, reserveGb);
}

/// <summary>What a run waits for before it takes a slot, by code, with the values its words name.</summary>
/// <param name="Code"><see cref="Slots"/>, <see cref="Memory"/>, <see cref="Reserve"/>, <see cref="Repository"/> or <see cref="Admitting"/>.</param>
public sealed record CheckWait(string Code, IReadOnlyDictionary<string, string> Values)
{
    /// <summary>Every slot runs a heavy run: <c>running</c> of <c>of</c>.</summary>
    public const string Slots = "slots";

    /// <summary>Another run would leave too little memory: <c>free</c> and <c>need</c> in GB, and <c>starting</c> where some still start.</summary>
    public const string Memory = "memory";

    /// <summary>Another run would leave less free than the person's reserve: <c>free</c>, <c>need</c> and <c>reserve</c> in GB, and <c>starting</c>.</summary>
    public const string Reserve = "reserve";

    /// <summary>Heavy runs of its own repository hold every slot the repository has: <c>what</c> names them.</summary>
    public const string Repository = "repository";

    /// <summary>The admission's own lock, which judges every heavy run together, is held by another start past the tries: said as that, never as a slot count.</summary>
    public const string Admitting = "admitting";

    /// <summary>The wait as the driver's log and the waiter's file say it, after "waiting for".</summary>
    public string Sentence => Code switch
    {
        Slots => $"a slot: {Values["running"]} of {Values["of"]} run",
        Admitting => "the admission's lock, which another start holds",
        Memory => $"memory: {Values["free"]} GB free, a check needs {Values["need"]} GB{Basis}" + Starting,
        Reserve => $"the reserve for your own work: {Values["free"]} GB free, a check needs {Values["need"]} GB{Basis} and {Values["reserve"]} GB stays free" + Starting,
        _ => Values.GetValueOrDefault("what") ?? "a heavy run of its repository",
    };

    private string Basis => Values.TryGetValue("source", out var source) ? $" ({source})" : "";

    private string Starting => Values.TryGetValue("starting", out var starting) ? $" with {starting} still starting" : "";

    public static CheckWait Held { get; } = new(Admitting, new Dictionary<string, string>(StringComparer.Ordinal));

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
public sealed record HeavyHeld(DateTimeOffset Since, string? Key, bool Starting, string Who)
{
    public string? Agent { get; init; }

    public IReadOnlyList<string> Gates { get; init; } = [CheckAdmission.DefaultGate];
}

/// <summary>
/// What a heavy run asks the admission for: the repository's slot it runs under (<paramref name="Key"/>, its repository's name and the
/// slots the repository's accepted declaration says, <see cref="AcceptedSlots"/>, null where it says none), and the record it says itself in.
/// </summary>
public sealed record HeavyAsk(string Key, string Repository, int? Declared, Func<HeavyHolder> Holder)
{
    /// <summary>The check slot's record, where the run takes one (a check, an integration's gated line); null for a session's or a terminal's run.</summary>
    public Func<VerifyHolder>? Check { get; init; }

    public string? Agent { get; init; }

    public IReadOnlyList<string> Gates { get; init; } = [CheckAdmission.DefaultGate];

    public string Kind { get; init; } = LoadKind.Test;
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
/// within the machine's admission (<see cref="CheckRules.Slots"/>). <c>auto</c>, the default, is the room's measured judgement (<see cref="RoomJudge"/>): the first run
/// always (the person's floor apart), another while what is free now, less what starting runs will take and less what the person's work is
/// measured to need back, holds its learned need, within the limit pressure has left and its project's and agent's fair share.
/// </summary>
/// <remarks>
/// <para><b>A run's need</b> is the largest of its gates' needs, each learned under the repository's key (<see cref="LearnedNeeds.OfGates"/>,
/// kept by <see cref="RunMeter"/>): its peak memory, a tenth over, once two have run; until then the declared cost of its kind
/// (<see cref="LoadKind.Declared"/>, the merge tool's 8 GB for a Process suite). A wait says which.</para>
///
/// <para><b>Still starting until its first gate ends.</b> A run whose first gate has not ended may not have reached its peak, so its need is
/// counted again and two never climb to their peaks on one reading. The first gate's end is a fact its record keeps (<see cref="HeavyHolder.Now"/>
/// past the first, or <see cref="HeavyHolder.Failed"/>); a time window would be a guess the machine's load stretches. A Daoris check's one
/// <c>reached</c> gate holds its build and its suites, so its runner says once that its builds are done (<c>reached-built: {}</c>, read by
/// <see cref="BuiltWatch"/> into <see cref="HeavyHolder.Built"/>) and it counts as starting until then; a runner that never says it counts for its whole run.</para>
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
    public const string DefaultGate = "heavy";

    /// <summary>What one more check run needs where the machine is quiet: the largest need learned for a check's gate here, else the test kind's declared cost.</summary>
    public static long QuietNeed(string home)
    {
        var learned = LearnedNeeds.Read(LearnedNeeds.PathOf(home))
            .Where(pair => pair.Key.EndsWith("|" + LoadKind.Test + "|reached", StringComparison.Ordinal))
            .Select(pair => LearnedNeeds.Need(pair.Value, LoadKind.Declared(LoadKind.Test)))
            .Where(need => need.Source == NeedReading.Learned)
            .Select(need => need.Cost.Memory)
            .ToList();
        return learned.Count > 0 ? learned.Max() : LoadKind.Declared(LoadKind.Test).Memory;
    }


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
            held.Add(new HeavyHeld(since, key, Starting(holder), Who(holder)) { Agent = holder.Session, Gates = GatesOf(holder) });
        }

        held.AddRange(checks.Select(check => new HeavyHeld(check.Since, null, true, $"the verification run {check.Run}")));
        return [.. held.OrderBy(one => one.Since)];
    }

    public static IReadOnlyList<string> GatesOf(HeavyHolder holder) =>
        holder.Run is not null ? ["reached"] : holder.Gates.Count > 0 ? holder.Gates : [DefaultGate];

    /// <summary>Whether a holder may still climb to its peak: its first gate has not ended and no failure or runner word of its builds being done is said.</summary>
    public static bool Starting(HeavyHolder holder) => holder.Failed is null && holder.Built is null && holder.Now is not { At: > 0 };

    /// <summary>A holder as a wait names it.</summary>
    public static string Who(HeavyHolder holder) =>
        holder.Run is { } run ? $"the verification run {run}" : holder.Session is { } session ? $"session {session}'s heavy run" : "a heavy run from a terminal";

    /// <summary>Why a run waits beside the runs holding a slot, or null where it may take one: the room's judgement over each run's learned need (declared by its kind until it has run).</summary>
    public static CheckWait? Judge(string home, IReadOnlyList<HeavyHeld> held, CheckRules rules, HeavyAsk ask, KindRule? rule = null)
    {
        var view = RoomLedger.Observe(home, rules, ask.Key, ask.Agent, rules.Ceiling, held.Count);
        var learned = LearnedNeeds.Read(LearnedNeeds.PathOf(home));
        NeedReading Need(string project, string kind, IReadOnlyList<string> gates) =>
            LearnedNeeds.OfGates(learned, project, kind, gates, LoadKind.Declared(kind));

        // A check slot whose repository slot is not written yet has no project and no history: it counts against the limit, at the declared cost.
        var holds = held.Select(one =>
        {
            var need = one.Key is { } key ? Need(key, LoadKind.Test, one.Gates) : new NeedReading(LoadKind.Declared(LoadKind.Test), NeedReading.DeclaredSource, 0);
            return new RoomHold(one.Key ?? "", one.Agent, LoadKind.Test, need.Cost, one.Starting, one.Who) { Source = need.Source };
        }).ToList();
        var asked = Need(ask.Key, ask.Kind, ask.Gates);
        return RoomJudge.Decide(view, holds, new RoomAsk(ask.Key, ask.Agent, ask.Kind, asked.Cost, asked.Source, rule ?? KindRule.Default(ask.Kind)) { Name = ask.Repository }, rules.Setting is null ? null : rules.Slots);
    }

    /// <summary>The slots a repository has: the machine's, or fewer where its accepted declaration says so.</summary>
    public static int SlotsOf(CheckRules rules, int? declared) => Math.Min(rules.Slots, declared ?? int.MaxValue);

    /// <summary>
    /// A run's slots and records, or why it has none: under the admission's lock, its repository's own slots judged, then the machine's
    /// (<see cref="Judge"/>), then its check slot taken where it asks one and its repository's slot, each record written as its slot is
    /// taken. Never waits past <see cref="AdmittingTries"/> tries for the lock.
    /// </summary>
    public static (HeavyTaken? Taken, CheckWait? Wait) TryTake(string home, CheckRules rules, HeavyAsk ask, Func<int, long, bool>? alive = null)
    {
        using var admitting = Admitting(home);
        var held = Holding(home, alive);
        if (admitting is null) return (null, CheckWait.Held);

        var mine = SlotsOf(rules, ask.Declared);
        var ours = held.Where(one => one.Key == ask.Key).ToList();
        if (ours.Count >= mine) return (null, CheckWait.OfRepository(ask.Repository, string.Join(", ", ours.Select(one => one.Who))));
        if (Judge(home, held, rules, ask) is { } wait) return (null, wait);

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
