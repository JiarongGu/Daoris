using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// Daoris's heavy executor: a check's attempt sent here is taken at once and runs
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

    /// <summary>Called once an attempt's slots are held and before it leaves the queue: a test's place to stage the person's stop there.</summary>
    internal Action? SlotsTaken { get; set; }
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
    /// The person's stop: a running attempt's job is ended as at its bound; one waiting is taken off the queue. Either
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
    /// The reconcile's question, answered from the attempt's folder: its result, else running while it waits or runs
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
                try
                {
                    slots = TryTake(each);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    Unrunnable(each, error);
                    continue;
                }

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

            SlotsTaken?.Invoke();

            // A stop between taking the slots and here has taken the attempt off the queue and settled it: it is not run as well.
            bool taken;
            lock (_gate)
            {
                taken = _queue.Remove(next);
                if (taken) _running = next;
            }

            if (!taken)
            {
                slots!.Value.Repository.Dispose();
                slots.Value.Home.Dispose();
                continue;
            }

            try
            {
                await RunAsync(next, slots!.Value.Home, slots.Value.Repository).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_ends.IsCancellationRequested)
            {
                return; // The driver ends: no result, so its restart sends the same attempt again.
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

    /// <summary>An attempt whose slots could not be taken: off the queue and answered <i>unknown</i>, so the loop goes on and no look reads it as running.</summary>
    private void Unrunnable(Queued queued, Exception error)
    {
        lock (_gate) _queue.Remove(queued);
        VerifySlot.Forget(home, queued.Send.Invocation);
        Settle(queued.Send, Unknown(queued.Send, $"the verification could not be started ({error.Message})"));
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

        HeavySlot? repository;
        try
        {
            repository = HeavySlot.TryTake(home, key);
        }
        catch
        {
            homeSlot.Dispose();
            throw;
        }

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
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // A job that cannot be opened, a bound the clock cannot count: answered, never left for every later look to read as running.
            result = Unknown(send, $"the verification could not be run ({error.Message})");
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
