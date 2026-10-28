using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What <c>daoris-driver run-heavy</c> was asked: one gate by name, or every heavy one (null); or, with <see cref="Look"/>, who holds
/// the slot and who waits, running nothing (HEAVYSLOT1); or, with <see cref="Command"/>, a command of the caller's own as a heavy run
/// (HEAVYQUEUE1).
/// </summary>
public sealed record HeavyRunAsk(string? Gate)
{
    /// <summary><c>--look</c>: say the slot as it stands (<see cref="HeavyRun.Look"/>), and run nothing.</summary>
    public bool Look { get; init; }

    /// <summary>
    /// <c>-- &lt;command…&gt;</c> (HEAVYQUEUE1): the words after it, a command the caller chose (a filtered <c>dotnet test</c>, a <c>vitest</c>
    /// run) run as a heavy run: after the machine's admission, in the folder it was run in, no repository's slot taken and no gate read.
    /// </summary>
    public IReadOnlyList<string>? Command { get; init; }
}

/// <summary>Where a heavy run runs: the home its slot is under, the folder it was run in, and its environment, read through a seam.</summary>
/// <param name="Home">The home whose <c>locks/</c> holds the slot.</param>
/// <param name="Directory">The folder the run was started in; its checkout is the nearest above holding a <c>.git</c>.</param>
/// <param name="Variable">A variable of the run's environment, or null.</param>
public sealed record HeavyRunWorld(string Home, string Directory, Func<string, string?> Variable)
{
    /// <summary>How the run waits for the slot.</summary>
    public HeavyWaiting Waiting { get; init; } = HeavyWaiting.Default;

    /// <summary>How the run waits for the machine's admission (HEAVYQUEUE1).</summary>
    public AdmissionWaiting Admission { get; init; } = AdmissionWaiting.Default;

    /// <summary>This process's: its folder and its environment.</summary>
    public static HeavyRunWorld OfThisProcess(string home) => new(home, Environment.CurrentDirectory, Environment.GetEnvironmentVariable);
}

/// <summary>What a run hands its gates: the slot it holds, and the gates running, outermost first.</summary>
public sealed record HeavyMarker(string Slot, IReadOnlyList<string> Gates);

/// <summary>
/// <c>daoris-driver run-heavy [--gate &lt;name&gt;]</c> (HEAVYRUN1a, D180's HEAVYRUN1 note): run in a checkout, it runs the gates its
/// <c>daoris.gates.json</c> marks heavy one after another (<see cref="HeavyGates"/>), or where it marks none this machine's mapping
/// for it (<see cref="HeavyMapping"/>, HEAVYRUN1b2a), holding its repository's one heavy-run slot (<see cref="HeavySlot"/>), every
/// gate's processes in one job that is ended and emptied before the slot is let go.
/// </summary>
/// <remarks>
/// <para><b>Why.</b> Two driven sessions on one repository each ran its full build and suites at once on the installed Daoris, and
/// the machine spent an hour doing little else. A session's heavy checks run through this, so a second session's wait behind the
/// first, said with whom and how long, rather than both at once.</para>
///
/// <para><b>One after another, the first failure ending the sequence.</b> Exit codes are the family's: 0 every gate passed · 1 a gate
/// failed, a gate named is not heavy, or the run was stopped · 2 it could not run: no checkout, no gates file, no heavy gate.</para>
///
/// <para><b>What a gate starts ends before the slot goes.</b> Each gate joins the run's job before its first instruction
/// (<see cref="HeavyStep"/>). A failure, a stop and a pass alike end the job and wait until it holds nothing, then let go of the slot:
/// a build's lingering workers included. A run killed outright closes its job and its slot with its process; the next holder ends
/// that job if it is still there before its own gates start (<see cref="ProcessJob.EndNamedAsync"/>). The gates run with
/// <c>MSBUILDDISABLENODEREUSE=1</c>, so a build node they start serves this run alone and ends with it, never a neighbour's build.</para>
///
/// <para><b>Its test runners bounded.</b> A gate the repository declares as Jest or Vitest, itself or through its npm, yarn or pnpm
/// script, runs with <c>--maxWorkers=2</c> where the installed version's flag, and the tool's forwarding at its recorded version, are
/// known, and as written otherwise; each gate's line says which and why (<see cref="HeavyWorkers"/>, HEAVYRUN1a2, HEAVYRUN1a3).</para>
///
/// <para><b>Never waits on itself.</b> The run hands its gates <see cref="Variable"/>, naming its slot and the gates running. A gate
/// that runs <c>run-heavy</c> again in the same repository runs inside the held slot without waiting, and one asked for a gate
/// already running in it is refused, since it would run inside itself without end.</para>
///
/// <para>Full access (D184) neither passes the slot nor changes a gate: this reads no access setting at all; of the driver's file it
/// reads only the heavy gates this machine sets for a repository that marks none.</para>
/// </remarks>
public static class HeavyRun
{
    /// <summary>The verb's usage, said with a word it does not take.</summary>
    public const string Usage = "usage: daoris-driver run-heavy [--gate <name> | --look | -- <command>]";

    /// <summary>What a run hands its gates' environment: <see cref="Marker"/>, the slot it holds and the gates running.</summary>
    public const string Variable = "DAORIS_HEAVY_RUN";

    /// <summary>The repository every driven session is handed by name, which a run looks this machine's mapping up under (HEAVYRUN1b2a).</summary>
    public const string RepositoryVariable = "DAORIS_REPOSITORY";

    /// <summary>The words after <c>run-heavy</c>, or null with what is wrong with them.</summary>
    public static HeavyRunAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        string? gate = null;
        var look = false;
        List<string>? command = null;
        for (var at = 0; at < args.Count; at++)
        {
            if (args[at] == "--")
            {
                command = [.. args.Skip(at + 1)];
                break;
            }

            if (args[at] == "--look")
            {
                look = true;
                continue;
            }

            if (args[at] != "--gate")
            {
                problem = $"`{args[at]}` is not a word `run-heavy` takes: it takes `--gate <name>`, `--look`, `-- <command>`, or nothing for every heavy gate.";
                return null;
            }

            if (at + 1 >= args.Count || args[at + 1].StartsWith("--", StringComparison.Ordinal) || args[at + 1].Trim().Length == 0)
            {
                problem = "`--gate` takes a gate's name.";
                return null;
            }

            if (gate is not null)
            {
                problem = "`run-heavy` runs one named gate, or every heavy one.";
                return null;
            }

            gate = args[++at].Trim();
        }

        if (look && gate is not null)
        {
            problem = "`--look` says who holds the slot and runs nothing, so it takes no gate.";
            return null;
        }

        if (command is not null)
        {
            if (command.Count == 0 || command[0].Trim().Length == 0)
            {
                problem = "`--` takes the command to run after it: `run-heavy -- dotnet test --filter Foo`.";
                return null;
            }

            if (look || gate is not null)
            {
                problem = "`-- <command>` runs a command of its own, so it takes neither `--gate` nor `--look`.";
                return null;
            }
        }

        return new HeavyRunAsk(gate) { Look = look, Command = command };
    }

    /// <summary>Run the checkout's heavy gates, or the one asked, saying on <paramref name="output"/> what it waits for and runs.</summary>
    /// <returns>0 every gate passed · 1 one failed, was refused or was stopped · 2 it could not run.</returns>
    public static async Task<int> RunAsync(HeavyRunAsk ask, HeavyRunWorld world, TextWriter output, CancellationToken ct)
    {
        try
        {
            return await RunCheckoutAsync(ask, world, output, ct).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            output.WriteLine($"run-heavy: {error.Message}");
            return 2;
        }
    }

    /// <summary>The marker a run hands its gates: the slot it holds, and the gates running, outermost first.</summary>
    public static string Marker(string slot, IReadOnlyList<string> gates)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("slot", slot);
            writer.WriteStartArray("gates");
            foreach (var gate in gates) writer.WriteStringValue(gate);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>The marker <paramref name="value"/> carries, or null where it carries none.</summary>
    public static HeavyMarker? ReadMarker(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            using var document = JsonDocument.Parse(value);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                   && root.TryGetProperty("slot", out var slot) && slot.ValueKind == JsonValueKind.String
                   && root.TryGetProperty("gates", out var gates) && gates.ValueKind == JsonValueKind.Array
                ? new HeavyMarker(
                    slot.GetString()!,
                    [.. gates.EnumerateArray().Where(gate => gate.ValueKind == JsonValueKind.String).Select(gate => gate.GetString()!)])
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<int> RunCheckoutAsync(HeavyRunAsk ask, HeavyRunWorld world, TextWriter output, CancellationToken ct)
    {
        if (ask.Command is { } command) return await RunCommandAsync(command, world, output, ct).ConfigureAwait(false);
        if (HeavySlot.CheckoutOf(world.Directory) is not { } checkout)
        {
            throw new DriverException($"{world.Directory} is in no git checkout: `run-heavy` runs a checkout's own gates, from inside it.");
        }

        // FABRIC-PIPELINE1b (the pipeline design §3.2's second layer): a tree a session holds under a light pipeline is refused at once,
        // before any gate is read or the slot is asked: its heavy gates are the verification's. The session is read from git's own lock on
        // the tree, never from a variable it could leave out; a run from a checkout no session holds is the person's, and runs as today.
        if (SessionLocks.HolderOf(checkout) is { } heldBy && SessionChecks.Held(world.Home, heldBy) == SessionChecks.Light)
        {
            output.WriteLine(
                $"run-heavy: this tree is session `{heldBy}`'s, and its development pipeline is light: the repository's heavy gates run in its "
                + "verification after the work lands, never in a session's tree. Run the checks your change reaches, filtered, instead.");
            return 1;
        }

        var repository = HeavySlot.RepositoryOf(checkout) ?? Path.GetFileName(checkout);

        // HEAVYRUN1b2a: where its own file marks none, this machine's mapping, under the repository its session is handed, which is
        // the name every door keys a repository's settings by; a run a person starts names it by its folder. Its home's driver file
        // is read only then, and one that does not read is a tool error, never "none set".
        var keyedAs = world.Variable(RepositoryVariable)?.Trim() is { Length: > 0 } handed ? handed : repository;
        var declared = HeavyGates.Resolve(
            checkout, keyedAs, () => HeavyMapping.For(DriverConfig.Load(DriverConfig.ResolvePath(world.Variable, world.Home)), keyedAs));
        var choice = HeavyGates.Choose(declared, ask.Gate);
        if (choice.Refusal is { } refusal)
        {
            output.WriteLine($"run-heavy: {refusal}");
            return choice.Exit;
        }

        if (declared.Mapped)
        {
            output.WriteLine($"run-heavy: `{keyedAs}` marks no heavy gate of its own, so this machine's run for it: {string.Join(", ", choice.Gates.Select(gate => gate.Name))}.");
        }

        var key = HeavySlot.KeyOf(checkout)
                  ?? throw new DriverException($"git's files at {checkout} do not say which repository it is a checkout of, so its heavy-run slot cannot be told.");

        // A gate of a run that holds this repository's slot runs the wrapper again: it runs inside that slot, never waiting on it.
        if (ReadMarker(world.Variable(Variable)) is { } outer && outer.Slot == key)
        {
            if (choice.Gates.FirstOrDefault(gate => outer.Gates.Contains(gate.Name)) is { } again)
            {
                output.WriteLine($"run-heavy: `{again.Name}` is already running in the heavy run this one is inside, so it is not run again inside itself.");
                return 1;
            }

            output.WriteLine($"run-heavy: inside the heavy run that holds `{repository}`'s slot, so this one runs in it without waiting.");
            return await RunGatesAsync(checkout, choice.Gates, key, outer.Gates, Job(null), output, ct).ConfigureAwait(false);
        }

        var session = world.Variable(IntakeRoom.SessionVariable) is { Length: > 0 } ofSession ? ofSession : null;
        var started = ThisProcessStarted();
        HeavySlot slot;
        try
        {
            // HEAVYSLOT1: while it waits it says so beside the slot, for the page to say who waits behind whom.
            var waiter = new HeavyWaiter(Environment.ProcessId, started, checkout, DateTimeOffset.UtcNow, session);
            slot = await HeavySlot.TakeAsync(world.Home, key, repository, output, ct, world.Waiting, waiter).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            output.WriteLine("run-heavy: stopped while waiting for the slot; nothing ran.");
            return 1;
        }

        using (slot)
        {
            if (slot.Before is { Job: { } left } before && await EndLeftAsync(left).ConfigureAwait(false))
            {
                output.WriteLine($"run-heavy: the run before this one (process {before.Pid}) ended holding the slot; what it left running was ended first.");
            }

            var named = OperatingSystem.IsWindows() ? $"Local\\daoris-heavy-{Guid.NewGuid():N}" : null;
            var job = Job(named);
            var holder = new HeavyHolder(
                Environment.ProcessId, started, checkout, [.. choice.Gates.Select(gate => gate.Name)], DateTimeOffset.UtcNow, session, named);
            try
            {
                slot.Keep(holder);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                job?.Dispose();
                throw new DriverException($"what this run runs could not be written beside its slot ({error.Message}), so nothing ran.");
            }

            // HEAVYSLOT1: the record says the gate running from its start, and the gate that failed before the slot goes. A rewrite
            // that fails costs the page a gate's name, never the run: the record it replaces still names this run and its job.
            void Keep(Func<HeavyHolder, HeavyHolder> change)
            {
                holder = change(holder);
                try
                {
                    slot.Keep(holder);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    output.WriteLine($"run-heavy: the gate running could not be written beside the slot ({error.Message}); the run goes on.");
                }
            }

            // HEAVYQUEUE1: the repository's slot says one run of its gates at a time; the machine's admission says how many heavy runs the
            // machine holds at all, the merge queue's gates among them. It comes after the slot, so a run waiting for its repository's turn
            // holds no place; a wait for it is said here and on the page, and the job is ended if the wait is.
            AdmissionHold? hold;
            try
            {
                var names = string.Join(", ", choice.Gates.Select(gate => $"`{gate.Name}`"));
                hold = await AdmitAsync(
                    world, new AdmissionRequest($"heavy gates {names}") { Session = session, Repository = repository, Tree = checkout }, output, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                job?.Dispose();
                output.WriteLine("run-heavy: stopped while waiting for the machine's admission; nothing ran.");
                return 1;
            }

            using (hold)
            {
                return await RunGatesAsync(checkout, choice.Gates, key, [], job, output, ct, Keep, Inside(hold)).ConfigureAwait(false);
            }
        }
    }

    /// <summary>What a run started inside a hold is handed, so it takes no second place: <see cref="MachineAdmission.InsideVariable"/>, where the hold is this run's.</summary>
    private static IReadOnlyDictionary<string, string>? Inside(AdmissionHold? hold) =>
        hold is null || hold.IsInside ? null : new Dictionary<string, string> { [MachineAdmission.InsideVariable] = hold.Id };

    /// <summary>
    /// A place in the machine's admission, waited for with no bound. A record that cannot be kept (the home is read-only, a lock held
    /// too long) costs the run its place and nothing else: it is said and the run goes on, as a heavy gate waiting for memory that
    /// cannot be read does.
    /// </summary>
    private static async Task<AdmissionHold?> AdmitAsync(HeavyRunWorld world, AdmissionRequest request, TextWriter output, CancellationToken ct)
    {
        try
        {
            return await MachineAdmission.TakeAsync(world.Home, request, output, ct, world.Admission, variable: world.Variable).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            output.WriteLine($"run-heavy: the machine's admission could not be kept ({error.Message}); this run goes on without a place in it.");
            return null;
        }
    }

    /// <summary>
    /// <c>run-heavy -- &lt;command…&gt;</c> (HEAVYQUEUE1): a command the caller chose, a focused <c>dotnet test</c> or a test file's run, as a heavy
    /// run of the machine: it waits for a place in the machine's admission (the queue's gates take places in the same one), saying what it
    /// waits for, runs in the folder it was started in with everything it starts held in a job that is ended before the place is let
    /// go, and answers as a gate does: 0 passed, 1 failed (its exit said) or stopped. No repository's slot is taken and no gate is read: it is not a gate, and two
    /// sessions' focused runs may run side by side as far as the machine admits.
    /// </summary>
    private static async Task<int> RunCommandAsync(IReadOnlyList<string> command, HeavyRunWorld world, TextWriter output, CancellationToken ct)
    {
        var folder = Path.GetFullPath(world.Directory);
        var checkout = HeavySlot.CheckoutOf(folder);
        var repository = world.Variable(RepositoryVariable)?.Trim() is { Length: > 0 } handed ? handed : checkout is null ? null : HeavySlot.RepositoryOf(checkout);
        var session = world.Variable(IntakeRoom.SessionVariable) is { Length: > 0 } ofSession ? ofSession : null;
        var line = CommandText(command);
        AdmissionHold? hold;
        try
        {
            hold = await AdmitAsync(
                world, new AdmissionRequest(line) { Session = session, Repository = repository, Tree = checkout ?? folder }, output, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            output.WriteLine("run-heavy: stopped while waiting for the machine's admission; nothing ran.");
            return 1;
        }

        using (hold)
        {
            var job = Job(OperatingSystem.IsWindows() ? $"Local\\daoris-heavy-{Guid.NewGuid():N}" : null);
            return await RunGatesAsync(
                checkout ?? folder, [new HeavyGate("command", line, null, true)], "", [], job, output, ct, null, Inside(hold), folder).ConfigureAwait(false);
        }
    }

    /// <summary>The words as one command line for the shell: a word with a space or a shell character in quotes, a quote in it escaped as the runtimes read one.</summary>
    internal static string CommandText(IReadOnlyList<string> words) => string.Join(' ', words.Select(word =>
        word.Length > 0 && word.IndexOfAny([' ', '\t', '&', '|', '<', '>', '^', '(', ')', '"']) < 0
            ? word
            : $"\"{word.Replace("\"", "\\\"")}\""));

    /// <summary>
    /// <c>run-heavy --look</c> (HEAVYSLOT1, the UI/UX design §3.5's terminal twin of a repository's *Heavy run* row): who holds the
    /// checkout's repository's slot, at which gate and for how long, and who waits, read as the page reads it; runs nothing.
    /// </summary>
    /// <returns>0 said · 2 it could not tell: no checkout, or git's files do not say its repository.</returns>
    public static int Look(HeavyRunWorld world, TextWriter output, Func<int, long, bool>? alive = null)
    {
        if (HeavySlot.CheckoutOf(world.Directory) is not { } checkout)
        {
            output.WriteLine($"run-heavy: {world.Directory} is in no git checkout: `run-heavy --look` reads a checkout's repository's slot, from inside it.");
            return 2;
        }

        var repository = HeavySlot.RepositoryOf(checkout) ?? Path.GetFileName(checkout);
        if (HeavySlot.KeyOf(checkout) is not { } key)
        {
            output.WriteLine($"run-heavy: git's files at {checkout} do not say which repository it is a checkout of, so its heavy-run slot cannot be told.");
            return 2;
        }

        var look = HeavySlot.Look(world.Home, key, alive);
        var now = world.Waiting.Clock();
        if (look.Holder is not { } holder)
        {
            output.WriteLine($"run-heavy: no heavy run of `{repository}` now.");
        }
        else
        {
            var who = holder.Session is { } session ? $"session {session}" : "a run from a terminal";
            var where = holder.Failed is { } failed
                ? $"failed at `{failed.Gate}` (exit {failed.Exit}), ending what it started"
                : holder.Now is { } gate
                    ? $"{gate.At + 1} of {holder.Gates.Count}, `{gate.Gate}` for {HeavySlot.Took(now - gate.Since)}"
                    : "starting its gates";
            output.WriteLine($"run-heavy: `{repository}`'s heavy run: {who} (process {holder.Pid}), {where}; {HeavySlot.Took(now - holder.Since)} in all.");
        }

        foreach (var waiter in look.Waiters)
        {
            var who = waiter.Session is { } session ? $"session {session}" : "a run from a terminal";
            output.WriteLine($"run-heavy: waits: {who} (process {waiter.Pid}), for {HeavySlot.Took(now - waiter.Since)}.");
        }

        return 0;
    }

    /// <summary>
    /// Each gate in turn, the first failure ending the sequence; then the job ended and emptied whatever came of it, before the caller
    /// lets go of the slot. The job is this method's to end.
    /// </summary>
    private static async Task<int> RunGatesAsync(
        string checkout, IReadOnlyList<HeavyGate> gates, string key, IReadOnlyList<string> running, ProcessJob? job, TextWriter output,
        CancellationToken ct, Action<Func<HeavyHolder, HeavyHolder>>? keep = null, IReadOnlyDictionary<string, string>? extra = null, string? root = null)
    {
        var ran = Stopwatch.StartNew();
        HeavyGate? current = null;
        HeavyStep? step = null;
        var failed = false;
        try
        {
            for (var at = 0; at < gates.Count && !failed; at++)
            {
                current = gates[at];
                var folder = current.WorkingDirectory is { } cwd ? Path.GetFullPath(Path.Combine(checkout, cwd)) : root ?? checkout;
                if (!Directory.Exists(folder)) throw new DriverException($"`{current.Name}`'s cwd, {current.WorkingDirectory}, is no folder of {checkout}.");

                // Its test runner held to two workers where the repository declares it Jest or Vitest, said either way (HEAVYRUN1a2).
                var bound = HeavyWorkers.Bind(current, folder, checkout);
                output.WriteLine($"run-heavy: `{current.Name}`: {bound.Run}");
                output.WriteLine($"run-heavy: {bound.Said}");
                output.Flush();
                var added = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [Variable] = Marker(key, [.. running, current.Name]),
                    // A build node this run starts serves it alone and ends with its build, never a neighbour's (HEAVYRUN1a).
                    ["MSBUILDDISABLENODEREUSE"] = "1",
                };
                foreach (var (name, value) in extra ?? new Dictionary<string, string>()) added[name] = value;

                var gateAt = at;
                var gateName = current.Name;
                keep?.Invoke(holder => holder with { Now = new HeavyGateNow(gateName, gateAt, DateTimeOffset.UtcNow) });
                step = HeavyStep.Start(job, bound.Run, folder, added);
                var code = await step.ExitAsync(ct).ConfigureAwait(false);
                step.Dispose();
                step = null;
                if (code == 0) continue;

                keep?.Invoke(holder => holder with { Failed = new HeavyGateFailed(gateName, code) });
                var after = gates.Skip(at + 1).Select(gate => gate.Name).ToList();
                output.WriteLine($"run-heavy: `{current.Name}` failed (exit {code}){(after.Count > 0 ? $"; not run after it: {string.Join(", ", after)}" : "")}.");
                failed = true;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            step?.Stop();
            step?.Dispose();
            await EndAsync(job, output, sayLeft: false).ConfigureAwait(false);
            output.WriteLine($"run-heavy: stopped while `{current?.Name}` ran; everything it started was ended before the slot was let go.");
            return 1;
        }
        catch
        {
            step?.Stop();
            step?.Dispose();
            await EndAsync(job, output, sayLeft: false).ConfigureAwait(false);
            throw;
        }

        await EndAsync(job, output, sayLeft: true).ConfigureAwait(false);
        if (failed) return 1;

        output.WriteLine(
            $"run-heavy: {gates.Count} heavy gate{(gates.Count == 1 ? "" : "s")} passed in {HeavySlot.Took(ran.Elapsed)}: {string.Join(", ", gates.Select(gate => gate.Name))}.");
        return 0;
    }

    /// <summary>End what is left in the job and wait until it holds nothing, then close it: before the slot goes, never after.</summary>
    private static async Task EndAsync(ProcessJob? job, TextWriter output, bool sayLeft)
    {
        if (job is null) return;
        using (job)
        {
            // A count that does not read is ended all the same: "cannot tell" is never "nothing left".
            var left = job.Active;
            if (left == 0) return;
            job.Terminate();
            await job.WaitEmptyAsync().ConfigureAwait(false);
            if (sayLeft && left is { } count) output.WriteLine($"run-heavy: ended {count} process{(count == 1 ? "" : "es")} the gates left running before letting go of the slot.");
        }
    }

    /// <summary>The job a run's gates are held in: on Windows one there must be, or no gate runs; elsewhere there is none.</summary>
    private static ProcessJob? Job(string? named)
    {
        if (!OperatingSystem.IsWindows()) return null;
        return ProcessJob.Create(named)
               ?? throw new DriverException("the system would not make a job to hold the gates' processes, so they could not be ended together; nothing ran.");
    }

    private static async Task<bool> EndLeftAsync(string job)
    {
        try
        {
            return await ProcessJob.EndNamedAsync(job).ConfigureAwait(false);
        }
        catch (System.ComponentModel.Win32Exception error)
        {
            throw new DriverException($"the run before this one left its job, {job}, which could not be opened to end what it holds ({error.Message}); nothing ran.");
        }
    }

    private static long ThisProcessStarted()
    {
        using var self = Process.GetCurrentProcess();
        return self.StartTime.ToUniversalTime().Ticks;
    }
}
