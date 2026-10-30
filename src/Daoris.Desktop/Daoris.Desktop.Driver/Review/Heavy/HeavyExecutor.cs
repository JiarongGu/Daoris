using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A check's attempt as the heavy executor takes it (FABRIC-PIPELINE1d; the pipeline design §4.2).</summary>
/// <param name="Run">The run whose check it is; its folder is under the home's <c>workflows/lines/</c>.</param>
/// <param name="Invocation">The attempt's invocation, which names its folder and its job.</param>
/// <param name="Repository">The repository's name, as what is said names it.</param>
/// <param name="Checkout">The repository's checkout Daoris knows: a runner starts there, and the commit is read there.</param>
/// <param name="Commit">The full commit the check judges.</param>
/// <param name="Accepted">The blob of <c>daoris.gates.json</c> the person accepted, <c>none</c> for this machine's mapping, or null where none was.</param>
public sealed record VerifySend(string Run, string Invocation, string Repository, string Checkout, string Commit, string? Accepted)
{
    /// <summary>The gates the check names; null for the heavy gates, or the runner where the declaration names one.</summary>
    public IReadOnlyList<string>? Gates { get; init; }

    /// <summary>The check's bound, counted from both slots held.</summary>
    public TimeSpan Bound { get; init; } = HeavyExecutor.DefaultBound;

    /// <summary>The verification tree its direct gates run in; null for one under the lines folder, by the repository's slot key.</summary>
    public string? Tree { get; init; }
}

/// <summary>What the executor runs with: its clocks, the environment its jobs are composed from, the admission's seams, and who it tells.</summary>
public sealed record VerifyWorld
{
    /// <summary>The clock the bound and a busy runner's ten minutes are read on: the machine's unless a test holds it.</summary>
    public TimeProvider Clock { get; init; } = TimeProvider.System;

    /// <summary>Between two looks at slots another run holds.</summary>
    public TimeSpan Poll { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>The environment every job is composed from (<see cref="GateEnvironment.Compose"/>): the driver's own, never a shell's.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Start { get; init; } = Own();

    public AdmissionWaiting Admission { get; init; } = AdmissionWaiting.Default;

    /// <summary>How many places the machine admits, where not this machine's.</summary>
    public int? Capacity { get; init; }

    /// <summary>A variable of the driver's own environment, which the admission reads for a hold this process is already inside.</summary>
    public Func<string, string?> Variable { get; init; } = Environment.GetEnvironmentVariable;

    /// <summary>This machine's heavy gates for a repository that declares none (HEAVYRUN1b2a), by its name.</summary>
    public Func<string, IReadOnlyList<HeavyGate>?> Mapping { get; init; } = _ => null;

    /// <summary>Where the executor says what it waits for and runs.</summary>
    public TextWriter Said { get; init; } = TextWriter.Null;

    /// <summary>Told once an attempt's result is written: the runtime asks a pass, which reconciles it.</summary>
    public Action<string, string>? Answered { get; init; }

    private static IReadOnlyList<KeyValuePair<string, string>> Own() =>
        [.. Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
            .Where(entry => entry.Key is string && entry.Value is string)
            .Select(entry => KeyValuePair.Create((string)entry.Key, (string)entry.Value!))];
}

/// <summary>What the executor answers of an attempt (the pipeline design §4.2), which the performer's reconcile reads.</summary>
public static class VerifyAnswers
{
    /// <summary>Its result is written: reached, failed or unknown.</summary>
    public const string Result = "result";

    /// <summary>It waits for its slots, or runs, here.</summary>
    public const string Running = "running";

    /// <summary>No result and no job of it here: cut off before its result, so it never ran.</summary>
    public const string NotStarted = "not-started";

    /// <summary>Nothing establishes what became of it.</summary>
    public const string Unknown = "unknown";
}

/// <summary>An attempt as the executor answers it: its state, its result where written, and why where it is not known.</summary>
public sealed record VerifyAnswer(string State, VerifyResult? Result = null)
{
    public string? Why { get; init; }
}

/// <summary>The verification run holding the home's verification slot: its process, its run and attempt, its repository, since when.</summary>
public sealed record VerifyHolder(int Pid, long Started, string Run, string Invocation, string Repository, DateTimeOffset Since);

/// <summary>An attempt the executor was sent and has not started, in the order sent, and what it waits for now.</summary>
public sealed record VerifyWaiter(int Pid, long Started, string Run, string Invocation, string Repository, DateTimeOffset Since, int Order)
{
    /// <summary>What it waits for: the run holding a slot, or the runner's own run; null before its first look.</summary>
    public string? For { get; init; }
}

/// <summary>The home's verification slot as it stands: its holder and the attempts waiting, each only while its process runs.</summary>
public sealed record VerifyLook(VerifyHolder? Holder, IReadOnlyList<VerifyWaiter> Waiters);

/// <summary>
/// The home's one verification slot (the pipeline design §4.2): one verification run in the home at a time, its handle the lock as a
/// repository's heavy-run slot's is, its holder and its waiters said beside it under <c>locks/verify/</c>.
/// </summary>
public sealed class VerifySlot : IDisposable
{
    private readonly FileStream _file;
    private readonly string _record;

    private VerifySlot(FileStream file, string record) => (_file, _record) = (file, record);

    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.Locks, "verify", "home.lock");

    public static string RecordOf(string home) => DaorisHome.Entry(home, HomeEntry.Locks, "verify", "home.json");

    public static string WaitersOf(string home) => DaorisHome.Entry(home, HomeEntry.Locks, "verify", "waits");

    /// <summary>The slot, or null while another run holds it. Never waits.</summary>
    public static VerifySlot? TryTake(string home)
    {
        var path = PathOf(home);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            return new VerifySlot(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None), RecordOf(home));
        }
        catch (IOException held) when (!OperatingSystem.IsWindows() || (held.HResult & 0xFFFF) is 32 or 33)
        {
            return null;
        }
    }

    public void Keep(VerifyHolder holder) => AtomicFile.WriteText(_record, Json(writer =>
    {
        writer.WriteNumber("pid", holder.Pid);
        writer.WriteNumber("started", holder.Started);
        writer.WriteString("run", holder.Run);
        writer.WriteString("invocation", holder.Invocation);
        writer.WriteString("repository", holder.Repository);
        writer.WriteString("since", holder.Since.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    }));

    /// <summary>Let go: the record first, then the slot.</summary>
    public void Dispose()
    {
        try
        {
            File.Delete(_record);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left: its process is gone soon, and a holder whose process is gone holds nothing.
        }

        _file.Dispose();
    }

    /// <summary>The slot's holder and its waiters, the first sent first, each only where its process runs. Reads files only.</summary>
    public static VerifyLook Look(string home, Func<int, long, bool>? alive = null)
    {
        alive ??= HeavySlot.Running;
        VerifyHolder? holder = null;
        if (Read(RecordOf(home)) is { } held && Text(held, "run") is { } run && Text(held, "invocation") is { } invocation
            && held.TryGetProperty("pid", out var pid) && pid.TryGetInt32(out var id) && held.TryGetProperty("started", out var started) && started.TryGetInt64(out var ticks)
            && held.TryGetProperty("since", out var since) && since.TryGetDateTimeOffset(out var at) && alive(id, ticks))
        {
            holder = new VerifyHolder(id, ticks, run, invocation, Text(held, "repository") ?? "", at);
        }

        var waiters = new List<VerifyWaiter>();
        var folder = WaitersOf(home);
        foreach (var file in Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*.json").ToList() : [])
        {
            if (Read(file) is { } one && Waiter(one) is { } waiter && alive(waiter.Pid, waiter.Started)) waiters.Add(waiter);
        }

        return new VerifyLook(holder, [.. waiters.OrderBy(waiter => waiter.Since).ThenBy(waiter => waiter.Order)]);
    }

    /// <summary>Say an attempt waits, in a file of its own named by its invocation; one that cannot be written costs the page a waiter.</summary>
    internal static void Wait(string home, VerifyWaiter waiter)
    {
        try
        {
            Directory.CreateDirectory(WaitersOf(home));
            AtomicFile.WriteText(Path.Combine(WaitersOf(home), waiter.Invocation + ".json"), Json(writer =>
            {
                writer.WriteNumber("pid", waiter.Pid);
                writer.WriteNumber("started", waiter.Started);
                writer.WriteString("run", waiter.Run);
                writer.WriteString("invocation", waiter.Invocation);
                writer.WriteString("repository", waiter.Repository);
                writer.WriteString("since", waiter.Since.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                writer.WriteNumber("order", waiter.Order);
                if (waiter.For is { } what) writer.WriteString("for", what);
            }));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }

    internal static void Forget(string home, string invocation)
    {
        try
        {
            File.Delete(Path.Combine(WaitersOf(home), invocation + ".json"));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static VerifyWaiter? Waiter(JsonElement root) =>
        root.TryGetProperty("pid", out var pid) && pid.TryGetInt32(out var id) && root.TryGetProperty("started", out var started) && started.TryGetInt64(out var ticks)
        && Text(root, "run") is { } run && Text(root, "invocation") is { } invocation
        && root.TryGetProperty("since", out var since) && since.TryGetDateTimeOffset(out var at)
            ? new VerifyWaiter(id, ticks, run, invocation, Text(root, "repository") ?? "", at,
                root.TryGetProperty("order", out var order) && order.TryGetInt32(out var number) ? number : 0) { For = Text(root, "for") }
            : null;

    private static JsonElement? Read(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream);
            return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string Json(Action<Utf8JsonWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray()).ReplaceLineEndings("\n") + "\n";
    }
}

/// <summary>
/// Daoris's heavy executor (FABRIC-PIPELINE1d; the pipeline design §4.2, D192 point 6): a check's attempt sent here is taken at once and runs
/// on the executor's own task, never the pass's. It holds the home's verification slot and then the repository's heavy-run slot, each tried
/// without waiting, one not free letting the other go at once, so no run holds a slot while it waits for another; the attempts sent wait in
/// the order they came, each said beside the home's slot. Once both are held its bound starts. Its gates run in one job below normal (a
/// <c>clock</c> gate or runner at normal), and at its end, its bound or the person's stop the job is ended and emptied before the repository's
/// slot and then the home's are let go; only then is the result written in the attempt's folder and the runtime told it has an answer.
/// </summary>
/// <remarks>
/// <para><b>Answered through the reconcile alone</b> (<see cref="ReconcileAsync"/>): a result there is the attempt's answer; one queued or
/// running here is <c>running</c>; none, after a restart or a drain, is <c>not-started</c> once a job its name still finds is ended, so the
/// same attempt is sent again under its key.</para>
/// <para><b>Busy</b> (a runner's exit 75 or its <c>busy</c> answer) lets go of the slots and offers the runner again ten minutes later, six
/// times at most, then writes <i>unknown</i>: no failure is written for it.</para>
/// </remarks>
public sealed class HeavyExecutor(string home, VerifyWorld? world = null) : IAsyncDisposable
{
    /// <summary>A check's bound where its node declares no <c>timeoutSeconds</c>: a full set outlasts a plugin node's fifteen minutes.</summary>
    public static readonly TimeSpan DefaultBound = TimeSpan.FromSeconds(7200);

    /// <summary>How long a busy runner is let be before it is offered again.</summary>
    public static readonly TimeSpan BusyAgain = TimeSpan.FromMinutes(10);

    /// <summary>How many times a busy runner is offered again before the check is unknown.</summary>
    public const int BusyTries = 6;

    /// <summary>The code a failed check's outcome carries; the gate it names is its result's.</summary>
    public const string GateFailed = "gate-failed";

    /// <summary>A commit holding no declaration, as an acceptance names it (a twin of the landed route's <c>none</c>).</summary>
    public const string NoDeclaration = "none";

    /// <summary>What an attempt waits for while its runner's own run holds the runner's lock.</summary>
    public const string RunnersOwnRun = "the runner's own run";

    private readonly VerifyWorld _world = world ?? new VerifyWorld();
    private readonly Lock _gate = new();
    private readonly List<Queued> _queue = [];
    private readonly HashSet<string> _answered = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _ends = new();
    private TaskCompletionSource _wake = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly long _started = ThisProcessStarted();
    private Queued? _running;
    private Task? _loop;
    private int _order;

    private sealed class Queued(VerifySend send, int order, DateTimeOffset since)
    {
        public VerifySend Send { get; } = send;
        public int Order { get; } = order;
        public DateTimeOffset Since { get; } = since;
        public DateTimeOffset? NotBefore { get; set; }
        public int Busy { get; set; }
        public string? For { get; set; }
        public CancellationTokenSource Stop { get; } = new();
    }

    /// <summary>The job an attempt's processes are held in, by its invocation, which a restart finds by name.</summary>
    public static string JobName(string invocation) => $"Local\\daoris-verify-{invocation}";

    /// <summary>
    /// Take an attempt: once its waiter is written it returns, and the rest runs on the executor's own task. The same attempt sent again
    /// while it waits, runs or has its result is taken as the one already here.
    /// </summary>
    public Task<bool> SendAsync(VerifySend send)
    {
        if (File.Exists(VerifyFiles.ResultOf(home, send.Run, send.Invocation))) return Task.FromResult(true);
        lock (_gate)
        {
            if (_running?.Send.Invocation == send.Invocation || _queue.Any(each => each.Send.Invocation == send.Invocation)) return Task.FromResult(true);
            var queued = new Queued(send, ++_order, DateTimeOffset.UtcNow);
            _queue.Add(queued);
            Waiting(queued);
            _loop ??= Task.Run(LoopAsync);
        }

        Wake();
        return Task.FromResult(true);
    }

    private void Wake() => Interlocked.Exchange(ref _wake, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();

    /// <summary>Whether this executor has written the attempt's result since it began: what the performer asks after.</summary>
    public bool Answered(string invocation)
    {
        lock (_gate) return _answered.Contains(invocation);
    }

    /// <summary>Whether the attempt waits or runs here.</summary>
    public bool Holds(string invocation)
    {
        lock (_gate) return _running?.Send.Invocation == invocation || _queue.Any(each => each.Send.Invocation == invocation);
    }

    /// <summary>
    /// The person's stop (the pipeline design §4.2): a running attempt's job is ended as at its bound; one waiting is taken off the queue. Either
    /// way its result follows, a stale attempt's to the performer, settling it and moving nothing.
    /// </summary>
    public void Stop(string invocation)
    {
        Queued? waiting;
        lock (_gate)
        {
            if (_running?.Send.Invocation == invocation)
            {
                _running.Stop.Cancel();
                return;
            }

            waiting = _queue.FirstOrDefault(each => each.Send.Invocation == invocation);
            if (waiting is null) return;
            _queue.Remove(waiting);
        }

        VerifySlot.Forget(home, invocation);
        Settle(waiting.Send, new VerifyResult(invocation, waiting.Send.Commit, VerifyVerdicts.Unknown, "the person stopped it before it ran", []) { Code = VerifyResult.Stopped });
    }

    /// <summary>
    /// The reconcile's question, answered from the attempt's folder (the pipeline design §4.2): its result, else running while it waits or runs
    /// here, else not started once a job its name still finds is ended.
    /// </summary>
    public async Task<VerifyAnswer> ReconcileAsync(string run, string invocation)
    {
        var path = VerifyFiles.ResultOf(home, run, invocation);
        if (File.Exists(path))
        {
            return VerifyResult.Read(path) is { } result
                ? new VerifyAnswer(VerifyAnswers.Result, result)
                : new VerifyAnswer(VerifyAnswers.Unknown) { Why = "its result does not read" };
        }

        if (Holds(invocation)) return new VerifyAnswer(VerifyAnswers.Running);
        if (OperatingSystem.IsWindows())
        {
            try
            {
                await ProcessJob.EndNamedAsync(JobName(invocation)).ConfigureAwait(false);
            }
            catch (System.ComponentModel.Win32Exception error)
            {
                // A job of its name that cannot be opened may still run: not known, never a second run beside it.
                return new VerifyAnswer(VerifyAnswers.Unknown) { Why = $"the job it left could not be opened ({error.Message})" };
            }
        }

        return new VerifyAnswer(VerifyAnswers.NotStarted);
    }

    public async ValueTask DisposeAsync()
    {
        await _ends.CancelAsync().ConfigureAwait(false);
        Wake();
        if (_loop is { } loop)
        {
            try
            {
                await loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task LoopAsync()
    {
        while (!_ends.IsCancellationRequested)
        {
            // Taken before the queue is read, so a send between the read and the nap still wakes it.
            var woken = Volatile.Read(ref _wake).Task;
            List<Queued> due;
            DateTimeOffset? soonest;
            bool waiting;
            var now = _world.Clock.GetUtcNow();
            lock (_gate)
            {
                due = [.. _queue.Where(each => each.NotBefore is null || each.NotBefore <= now).OrderBy(each => each.Order)];
                soonest = _queue.Where(each => each.NotBefore > now).Select(each => each.NotBefore).Min();
                waiting = _queue.Count > 0;
            }

            Queued? next = null;
            (VerifySlot Home, HeavySlot Repository)? slots = null;
            foreach (var each in due)
            {
                slots = TryTake(each);
                if (slots is not null)
                {
                    next = each;
                    break;
                }
            }

            if (next is null)
            {
                await NapAsync(woken, waiting, soonest).ConfigureAwait(false);
                continue;
            }

            lock (_gate)
            {
                _queue.Remove(next);
                _running = next;
            }

            try
            {
                await RunAsync(next, slots!.Value.Home, slots.Value.Repository).ConfigureAwait(false);
            }
            finally
            {
                lock (_gate) _running = null;
            }
        }
    }

    /// <summary>
    /// Wait for a send, a slot's next look or a busy runner's next offer, whichever comes first. While anything waits the slots are looked at
    /// again each poll, which also reads the clock again for a busy runner's offer.
    /// </summary>
    private async Task NapAsync(Task woken, bool waiting, DateTimeOffset? busy)
    {
        var waits = new List<Task> { woken };
        if (waiting) waits.Add(Task.Delay(_world.Poll, _ends.Token));
        if (busy is { } at && at - _world.Clock.GetUtcNow() is var until && until > TimeSpan.Zero) waits.Add(Task.Delay(until, _world.Clock, _ends.Token));
        await Task.WhenAny(waits).ConfigureAwait(false);
    }

    /// <summary>The home's slot, then the repository's, each tried without waiting; one not free lets the other go and says what holds it.</summary>
    private (VerifySlot Home, HeavySlot Repository)? TryTake(Queued queued)
    {
        var send = queued.Send;
        if (HeavySlot.KeyOf(send.Checkout) is not { } key)
        {
            lock (_gate) _queue.Remove(queued);
            VerifySlot.Forget(home, send.Invocation);
            Settle(send, Unknown(send, $"git's files at {send.Checkout} do not say which repository it is a checkout of, so its heavy-run slot cannot be told"));
            return null;
        }

        var homeSlot = VerifySlot.TryTake(home);
        if (homeSlot is null)
        {
            Blocked(queued, VerifySlot.Look(home).Holder is { } holder ? $"the verification of {holder.Repository} (run {holder.Run})" : "another verification");
            return null;
        }

        var repository = HeavySlot.TryTake(home, key);
        if (repository is null)
        {
            homeSlot.Dispose();
            Blocked(queued, HeavySlot.Look(home, key).Holder is { } holder
                ? holder.Run is { } run ? $"the verification run {run}" : holder.Session is { } session ? $"session {session}'s heavy run" : "a heavy run from a terminal"
                : "a heavy run");
            return null;
        }

        return (homeSlot, repository);
    }

    private void Blocked(Queued queued, string what)
    {
        if (queued.For == what) return;
        queued.For = what;
        Waiting(queued);
        _world.Said.WriteLine($"verify: {queued.Send.Repository}'s check (run {queued.Send.Run}) is waiting for {what}.");
    }

    private void Waiting(Queued queued) => VerifySlot.Wait(
        home, new VerifyWaiter(Environment.ProcessId, _started, queued.Send.Run, queued.Send.Invocation, queued.Send.Repository, queued.Since, queued.Order) { For = queued.For });

    private async Task RunAsync(Queued queued, VerifySlot homeSlot, HeavySlot repository)
    {
        var send = queued.Send;
        var key = HeavySlot.KeyOf(send.Checkout)!;
        var job = JobName(send.Invocation);
        var holder = new HeavyHolder(Environment.ProcessId, _started, send.Checkout, [], DateTimeOffset.UtcNow, null, job) { Run = send.Run };
        VerifyResult? result = null;
        var busy = false;
        var ran = Stopwatch.StartNew();
        try
        {
            homeSlot.Keep(new VerifyHolder(Environment.ProcessId, _started, send.Run, send.Invocation, send.Repository, DateTimeOffset.UtcNow));
            repository.Keep(holder);
            VerifySlot.Forget(home, send.Invocation);
            if (repository.Before is { Job: { } left } && OperatingSystem.IsWindows()) await ProcessJob.EndNamedAsync(left).ConfigureAwait(false);

            // The bound starts once both slots are held.
            using var bound = new CancellationTokenSource(send.Bound, _world.Clock);
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(bound.Token, queued.Stop.Token, _ends.Token);
            var candidate = await CandidateJob.OpenAsync(job).ConfigureAwait(false);
            try
            {
                var run = new VerifyExecution(home, send, candidate, _world, key, change =>
                {
                    holder = change(holder);
                    try
                    {
                        repository.Keep(holder);
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                    {
                    }
                });
                (result, busy) = await run.ExecuteAsync(stop.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                // A git read cut off by the bound, the stop or the driver's end: said below by which.
            }
            finally
            {
                // Empty before either slot goes.
                await candidate.DisposeAsync().ConfigureAwait(false);
            }

            if (_ends.IsCancellationRequested) result = null;
            else if (queued.Stop.IsCancellationRequested) result = (result ?? Unknown(send, "")) with { Verdict = VerifyVerdicts.Unknown, Why = "the person stopped it, and its job was ended", Code = VerifyResult.Stopped, Failed = null };
            else if (bound.IsCancellationRequested) result = (result ?? Unknown(send, "")) with { Verdict = VerifyVerdicts.Unknown, Why = $"it ran past its bound of {HeavySlot.Took(send.Bound)}, and its job was ended", Code = VerifyResult.Bound, Failed = null };
        }
        catch (DriverException error)
        {
            result = Unknown(send, error.Message);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            result = Unknown(send, $"the verification could not be kept ({error.Message})");
        }
        finally
        {
            repository.Dispose();
            homeSlot.Dispose();
        }

        if (busy && result is null && !_ends.IsCancellationRequested)
        {
            if (queued.Busy < BusyTries && !queued.Stop.IsCancellationRequested)
            {
                queued.Busy++;
                queued.NotBefore = _world.Clock.GetUtcNow() + BusyAgain;
                queued.For = RunnersOwnRun;
                lock (_gate) _queue.Add(queued);
                Waiting(queued);
                _world.Said.WriteLine($"verify: {send.Repository}'s runner is busy with its own run; offered again in {HeavySlot.Took(BusyAgain)}.");
                return;
            }

            result = Unknown(send, "the runner's own run held its lock through every offer") with { Code = VerifyResult.Busy };
        }

        if (result is null) return; // the driver ending: no result, so its restart sends the same attempt again.
        Settle(send, result with { WallSeconds = ran.Elapsed.TotalSeconds });
    }

    private void Settle(VerifySend send, VerifyResult result)
    {
        try
        {
            result.Write(VerifyFiles.ResultOf(home, send.Run, send.Invocation));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _world.Said.WriteLine($"verify: the result of run {send.Run} could not be written ({error.Message}); its restart runs it again.");
            return;
        }

        lock (_gate) _answered.Add(send.Invocation);
        _world.Said.WriteLine($"verify: {send.Repository} at {TextBounds.ShortCommit(send.Commit, 7)}: {result.Verdict}{(result.Why is { } why ? $" ({why})" : "")}.");
        _world.Answered?.Invoke(send.Run, send.Invocation);
    }

    internal static VerifyResult Unknown(VerifySend send, string why) => new(send.Invocation, send.Commit, VerifyVerdicts.Unknown, why, []) { Declaration = send.Accepted };

    private static long ThisProcessStarted()
    {
        using var self = Process.GetCurrentProcess();
        return self.StartTime.ToUniversalTime().Ticks;
    }
}

/// <summary>A declaration's <c>verification</c> runner (D192's key): its command, its folder, whether it is clock-bound and whether it answers by file.</summary>
internal sealed record VerifyRunner(string Run, string? Cwd, bool Clock, bool Result)
{
    internal static VerifyRunner? Of(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || IntegrationPromotes.RunnerOf(text) is not { } runner) return null;
        using var document = JsonDocument.Parse(text);
        var verification = document.RootElement.GetProperty("verification");
        bool Flag(string name) => verification.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
        return new VerifyRunner(runner.Run, runner.Cwd, Flag("clock"), Flag("result"));
    }
}

/// <summary>One attempt's run, holding both slots: the accepted declaration read at the commit, then its runner or its gates in the job.</summary>
internal sealed class VerifyExecution(string home, VerifySend send, CandidateJob job, VerifyWorld world, string key, Action<Func<HeavyHolder, HeavyHolder>> keep)
{
    private readonly string _folder = VerifyFiles.AttemptFolder(home, send.Run, send.Invocation);

    public async Task<(VerifyResult? Result, bool Busy)> ExecuteAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(_folder);
        if (send.Accepted is null) return (Unknown("no gates are accepted for this repository's verification, so nothing runs until the person accepts them"), false);
        // The declaration is read as git objects at the commit, the blob the person accepted (the pipeline design §4.2), never a tree's file.
        var (listed, entry, problem) = await WorkingTree.GitAsync(send.Checkout, ["ls-tree", "--full-tree", "-z", send.Commit, "--", HeavyGates.FileName], ct).ConfigureAwait(false);
        if (listed != 0) return (Unknown($"git could not read {TextBounds.ShortCommit(send.Commit, 7)} here: {problem.Trim()}"), false);
        var parts = entry.Split('\t', 2)[0].Split(' ');
        var tip = parts is [_, "blob", var blob] ? blob.Trim() : HeavyExecutor.NoDeclaration;
        if (!string.Equals(tip, send.Accepted, StringComparison.Ordinal))
        {
            return (Unknown("the gates changed since you accepted them") with { Code = VerifyResult.Changed, Declaration = tip }, false);
        }

        string? text = null;
        if (tip != HeavyExecutor.NoDeclaration)
        {
            var read = await WorkingTree.GitBytesAsync(send.Checkout, ["cat-file", "blob", tip], 256 * 1024, ct).ConfigureAwait(false);
            if (read is null) return (Unknown($"the accepted declaration {TextBounds.ShortCommit(tip, 7)} could not be read here"), false);
            text = Encoding.UTF8.GetString(read.Value.Bytes, 0, read.Value.Count);
        }

        IReadOnlyList<DeclaredGate> declared = text is null
            ? [.. (world.Mapping(send.Repository) ?? []).Select(gate => new DeclaredGate(gate.Name, gate.Run, gate.WorkingDirectory, true))]
            : DeclaredGates.Read(text);
        var runner = VerifyRunner.Of(text);
        if (send.Gates is null && runner is not null) return await RunnerAsync(runner, declared, ct).ConfigureAwait(false);

        List<DeclaredGate> chosen;
        if (send.Gates is { } names)
        {
            if (names.FirstOrDefault(name => declared.All(gate => gate.Name != name)) is { } undeclared)
            {
                return (Unknown($"the gate '{undeclared}' is not declared by the accepted declaration"), false);
            }

            chosen = [.. names.Select(name => declared.First(gate => gate.Name == name))];
        }
        else
        {
            chosen = [.. declared.Where(gate => gate.Heavy)];
        }

        if (chosen.Count == 0) return (Unknown("the accepted declaration marks no heavy gate, so the check shows nothing"), false);
        return (await GatesAsync(chosen, ct).ConfigureAwait(false), false);
    }

    private async Task<(VerifyResult? Result, bool Busy)> RunnerAsync(VerifyRunner runner, IReadOnlyList<DeclaredGate> declared, CancellationToken ct)
    {
        var answer = Path.Combine(_folder, "runner-answer.json");
        var log = Path.Combine(_folder, "runner.log");
        if (File.Exists(answer)) File.Delete(answer);
        var inputs = runner.Result
            ? new Dictionary<string, string> { [RunnerInputs.Commit] = send.Commit.ToLowerInvariant(), [RunnerInputs.Result] = answer }
            : new Dictionary<string, string>();
        var folder = runner.Cwd is { } cwd ? Path.GetFullPath(Path.Combine(send.Checkout, cwd)) : send.Checkout;
        keep(holder => holder with { Gates = [VerifyJudge.RunnerGate], Now = new HeavyGateNow(VerifyJudge.RunnerGate, 0, DateTimeOffset.UtcNow) });
        var exit = await StepAsync(runner.Run, folder, [VerifyJudge.RunnerGate], inputs, log, runner.Clock, ct).ConfigureAwait(false);
        if (exit is null) return (Ended([new VerifyGate(VerifyJudge.RunnerGate, RunnerVerdicts.Ended) { Log = log }]), false);
        var judged = runner.Result
            ? VerifyJudge.ByFile(send.Invocation, send.Commit, exit.Value, ReadText(answer), [.. declared.Select(gate => gate.Name)],
                [.. declared.Where(gate => gate.Heavy).Select(gate => gate.Name)], log)
            : VerifyJudge.Opaque(send.Invocation, send.Commit, exit.Value, log);
        return (judged.Result is { } result ? result with { Declaration = send.Accepted } : null, judged.Busy);
    }

    /// <summary>The gates one after another in Daoris's verification tree at the commit, the first failure ending the sequence.</summary>
    private async Task<VerifyResult> GatesAsync(IReadOnlyList<DeclaredGate> gates, CancellationToken ct)
    {
        var tree = send.Tree ?? Path.Combine(VerifyFiles.LinesFolder(home), "trees", key);
        await TreeAtAsync(tree, ct).ConfigureAwait(false);
        var ran = new List<VerifyGate>();
        try
        {
            for (var at = 0; at < gates.Count; at++)
            {
                var gate = gates[at];
                var log = Path.Combine(_folder, $"gate-{at + 1:D2}-{Slug(gate.Name)}.log");
                var folder = gate.Cwd is { } cwd ? Path.GetFullPath(Path.Combine(tree, cwd)) : tree;
                var index = at;
                keep(holder => holder with { Gates = [.. gates.Select(each => each.Name)], Now = new HeavyGateNow(gate.Name, index, DateTimeOffset.UtcNow) });
                var wall = Stopwatch.StartNew();
                int? exit = Directory.Exists(folder) ? await StepAsync(gate.Run, folder, [gate.Name], new Dictionary<string, string>(), log, gate.Clock, ct).ConfigureAwait(false) : Missing(log, gate);
                var rest = gates.Skip(at + 1).Select(each => new VerifyGate(each.Name, RunnerVerdicts.NotRun));
                if (exit is null) return Ended([.. ran, new VerifyGate(gate.Name, RunnerVerdicts.Ended) { Log = log, WallSeconds = wall.Elapsed.TotalSeconds }, .. rest]);
                ran.Add(new VerifyGate(gate.Name, exit == 0 ? RunnerVerdicts.Pass : RunnerVerdicts.Fail, exit) { Log = log, WallSeconds = wall.Elapsed.TotalSeconds });
                if (exit == 0) continue;
                keep(holder => holder with { Failed = new HeavyGateFailed(gate.Name, exit.Value) });
                return new VerifyResult(send.Invocation, send.Commit, VerifyVerdicts.Failed, $"the gate '{gate.Name}' failed with exit {exit}", [.. ran, .. rest])
                {
                    Failed = gate.Name, Declaration = send.Accepted,
                };
            }

            return new VerifyResult(send.Invocation, send.Commit, VerifyVerdicts.Reached, null, ran) { Declaration = send.Accepted };
        }
        finally
        {
            await WorkingTree.GitAsync(send.Checkout, ["worktree", "unlock", tree], CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The verification tree at the commit: made once, detached, then moved to each run's tip, and locked while this run uses it, the lock's
    /// reason naming this process as a replay's tree names its pass's.
    /// </summary>
    private async Task TreeAtAsync(string tree, CancellationToken ct)
    {
        if (!File.Exists(Path.Combine(tree, ".git")))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tree)!);
            await WorkingTree.GitAsync(send.Checkout, ["worktree", "prune"], ct).ConfigureAwait(false);
            var (made, _, problem) = await WorkingTree.GitAsync(send.Checkout, ["worktree", "add", "--detach", "--force", tree, send.Commit], ct).ConfigureAwait(false);
            if (made != 0) throw new DriverException($"the verification tree could not be made at {tree}: {problem.Trim()}");
        }
        else
        {
            await WorkingTree.GitAsync(send.Checkout, ["worktree", "unlock", tree], ct).ConfigureAwait(false);
            var (moved, _, problem) = await WorkingTree.GitAsync(tree, ["checkout", "--detach", "--force", send.Commit], ct).ConfigureAwait(false);
            if (moved != 0) throw new DriverException($"the verification tree could not be moved to {TextBounds.ShortCommit(send.Commit, 7)}: {problem.Trim()}");
        }

        await WorkingTree.GitAsync(send.Checkout, ["worktree", "lock", "--reason", PassLeftovers.HeldBy(Environment.ProcessId), tree], ct).ConfigureAwait(false);
    }

    /// <summary>One command in the job, composed from the driver's environment; its exit, or null where the run was ended before it.</summary>
    private async Task<int?> StepAsync(
        string run, string folder, IReadOnlyList<string> running, IReadOnlyDictionary<string, string> inputs, string log, bool clock, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return null;
        AdmissionHold? hold = null;
        try
        {
            hold = await MachineAdmission.TakeAsync(
                home, new AdmissionRequest($"verification of {send.Repository} at {TextBounds.ShortCommit(send.Commit, 7)}") { Repository = send.Repository, Tree = folder },
                world.Said, ct, world.Admission, world.Capacity, world.Variable).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            world.Said.WriteLine($"verify: the machine's admission could not be kept ({error.Message}); the check goes on without a place in it.");
        }

        using (hold)
        {
            var environment = GateEnvironment.Compose(world.Start, null, hold is { IsInside: false } held ? held.Id : null)
                .ToDictionary(each => each.Key, each => each.Value, StringComparer.OrdinalIgnoreCase);
            environment[HeavyRun.Variable] = HeavyRun.Marker(key, running);
            foreach (var (name, value) in inputs) environment[name] = value;
            HeavyStep step;
            try
            {
                step = HeavyStep.Start(job.Job, run, folder, new Dictionary<string, string>(), new StepOptions(environment, log) { BelowNormal = !clock });
            }
            catch (DriverException error)
            {
                await File.WriteAllTextAsync(log, $"error: it would not start: {error.Message}\n", new UTF8Encoding(false), CancellationToken.None).ConfigureAwait(false);
                return -1;
            }

            using (step)
            {
                try
                {
                    return await step.ExitAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    job.Job?.Terminate();
                    step.Stop();
                    return null;
                }
                finally
                {
                    // Nothing it left runs past its place in the admission.
                    await job.EndLeftAsync().ConfigureAwait(false);
                }
            }
        }
    }

    private VerifyResult Unknown(string why) => HeavyExecutor.Unknown(send, why);

    private VerifyResult Ended(IReadOnlyList<VerifyGate> gates) =>
        new(send.Invocation, send.Commit, VerifyVerdicts.Unknown, "it was ended before its verdict", gates) { Declaration = send.Accepted };

    private static int Missing(string log, DeclaredGate gate)
    {
        File.WriteAllText(log, $"error: `{gate.Name}`'s cwd, {gate.Cwd}, is no folder of the verification tree\n", new UTF8Encoding(false));
        return -1;
    }

    private static string? ReadText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path, new UTF8Encoding(false)) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string Slug(string name) => new([.. name.Select(letter => char.IsAsciiLetterOrDigit(letter) ? letter : '-')]);
}
