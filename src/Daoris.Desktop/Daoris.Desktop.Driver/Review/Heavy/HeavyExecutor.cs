using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// Daoris's heavy executor: a check's attempt sent here is taken at once and runs on a task of its own, as many at once as the check slots
/// admit (<see cref="CheckAdmission"/>), each holding a verification slot then a repository slot, tried without waiting so none holds one while
/// it waits; the rest wait in the queue's order (<see cref="QueueOrder"/>), each said with why. Its gates run in one job below normal (a <c>clock</c> gate at normal), ended
/// and emptied at its end, bound or stop before the slots go; only then is its result written and the runtime told.
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
    private readonly Dictionary<string, Queued> _running = new(StringComparer.Ordinal);
    private readonly List<Task> _runs = [];
    private Task? _loop;

    /// <summary>Called once an attempt's slots are held and before it leaves the queue: a test's place to stage the person's stop there.</summary>
    internal Action? SlotsTaken { get; set; }
    private int _order;

    private sealed class Queued(VerifySend send, int order, DateTimeOffset since, DateTimeOffset arrived)
    {
        public VerifySend Send { get; } = send;
        public int Order { get; } = order;
        public DateTimeOffset Since { get; } = since;
        public DateTimeOffset Arrived { get; } = arrived;
        public PriorityReading Priority { get; set; } = PriorityReading.None;
        public CheckWait? Refused { get; set; }
        public DateTimeOffset? NotBefore { get; set; }
        public int Busy { get; set; }
        public string? For { get; set; }
        public CheckWait? Wait { get; set; }
        public CancellationTokenSource Stop { get; } = new();

        public string? StopWhy { get; set; }
    }

    /// <summary>The job an attempt's processes are held in, by its home and invocation, which a restart finds by name and no other home shares.</summary>
    public static string JobName(string home, string invocation) =>
        $"Local\\daoris-verify-{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(home).ToLowerInvariant())))[..12]}-{invocation}";

    /// <summary>
    /// Take an attempt: once its waiter is written it returns, and the rest runs on the executor's own task. The same attempt sent again
    /// while it waits, runs or has its result is taken as the one already here.
    /// </summary>
    public Task<bool> SendAsync(VerifySend send)
    {
        if (File.Exists(VerifyFiles.ResultOf(home, send.Run, send.Invocation))) return Task.FromResult(true);
        var now = DateTimeOffset.UtcNow;
        var looked = _world.Clock.GetUtcNow();
        var arrived = CheckArrivals.Keep(home, send.Run, looked);
        var priority = PriorityBook.OfRun(PriorityBook.Read(home), send.Run, send.Ask, looked);
        lock (_gate)
        {
            if (_running.ContainsKey(send.Invocation) || _queue.Any(each => each.Send.Invocation == send.Invocation)) return Task.FromResult(true);
            var queued = new Queued(send, ++_order, now, arrived) { Priority = priority };
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
        lock (_gate) return _running.ContainsKey(invocation) || _queue.Any(each => each.Send.Invocation == invocation);
    }

    /// <summary>
    /// The person's stop, or Daoris's where <paramref name="why"/> says why (a new commit superseded it): a running attempt's job is ended as at
    /// its bound; one waiting is taken off the queue. Either way its result follows, a stale attempt's to the performer, settling it and moving nothing.
    /// </summary>
    public void Stop(string invocation, string? why = null)
    {
        Queued? waiting;
        lock (_gate)
        {
            if (_running.TryGetValue(invocation, out var running))
            {
                running.StopWhy ??= why;
                running.Stop.Cancel();
                return;
            }

            waiting = _queue.FirstOrDefault(each => each.Send.Invocation == invocation);
            if (waiting is null) return;
            _queue.Remove(waiting);
        }

        VerifySlot.Forget(home, invocation);
        Settle(waiting.Send, new VerifyResult(invocation, waiting.Send.Commit, VerifyVerdicts.Unknown, why is null ? "the person stopped it before it ran" : $"{why}, so it was not run", [])
        {
            Code = VerifyResult.Stopped,
        });
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
                await ProcessJob.EndNamedAsync(JobName(home, invocation)).ConfigureAwait(false);
            }
            catch (System.ComponentModel.Win32Exception error)
            {
                // A job of its name that cannot be opened may still run: not known, never a second run beside it.
                return new VerifyAnswer(VerifyAnswers.Unknown) { Why = $"the job it left could not be opened ({error.Message})", Unaccounted = true };
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

        Task[] runs;
        lock (_gate) runs = [.. _runs];
        await Task.WhenAll(runs).ConfigureAwait(false);
    }

    private async Task LoopAsync()
    {
        while (!_ends.IsCancellationRequested)
        {
            // Taken before the queue is read, so a send between the read and the nap still wakes it.
            var woken = Volatile.Read(ref _wake).Task;
            List<Queued> due;
            DateTimeOffset? soonest;
            var now = _world.Clock.GetUtcNow();
            lock (_gate)
            {
                due = [.. _queue.Where(each => each.NotBefore is null || each.NotBefore <= now)];
                soonest = _queue.Where(each => each.NotBefore > now).Select(each => each.NotBefore).Min();
            }

            var ordered = Ordered(due, now);
            foreach (var (each, at) in ordered.Select((each, at) => (each, at)))
            {
                HeavyTaken? held;
                try
                {
                    held = await TryTakeAsync(each).ConfigureAwait(false);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    Unrunnable(each, error);
                    continue;
                }

                if (held is null && AdmissionOrder.EndsPass(each.Refused))
                {
                    foreach (var behind in ordered.Skip(at + 1)) Blocked(behind, each.Refused!);
                    break;
                }

                if (held is null) continue;
                SlotsTaken?.Invoke();

                // A stop between taking the slots and here has taken the attempt off the queue and settled it: it is not run as well.
                lock (_gate)
                {
                    if (_queue.Remove(each))
                    {
                        _running[each.Send.Invocation] = each;
                        _runs.Add(Task.Run(() => RunOneAsync(each, held)));
                        continue;
                    }
                }

                held.Dispose();
            }

            bool waiting;
            lock (_gate) waiting = _queue.Count > 0;
            await NapAsync(woken, waiting, soonest).ConfigureAwait(false);
        }
    }

    /// <summary>One attempt's run: off the running at its end (requeued in one step, under the stop's lock, where a busy runner sent it and no stop came), a cancel not the driver's end answered, the loop woken.</summary>
    private async Task RunOneAsync(Queued queued, HeavyTaken held)
    {
        var again = false;
        try
        {
            again = await RunAsync(queued, held).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_ends.IsCancellationRequested)
        {
        }
        catch (OperationCanceledException error)
        {
            Settle(queued.Send, Unknown(queued.Send, $"the verification was cut off by something other than its bound, its stop or the driver's end ({error.Message})"));
        }
        finally
        {
            bool stopped;
            lock (_gate)
            {
                _running.Remove(queued.Send.Invocation);
                stopped = again && queued.Stop.IsCancellationRequested;
                if (again && !stopped) _queue.Add(queued);
                _runs.RemoveAll(run => run.IsCompleted);
            }

            if (stopped)
            {
                VerifySlot.Forget(home, queued.Send.Invocation);
                Settle(queued.Send, new VerifyResult(
                    queued.Send.Invocation, queued.Send.Commit, VerifyVerdicts.Unknown,
                    queued.StopWhy is { } superseded ? $"{superseded}, so it was not run again" : "the person stopped it before it ran again", [])
                {
                    Code = VerifyResult.Stopped, Declaration = queued.Send.Accepted,
                });
            }

            Wake();
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

    /// <summary>A check slot and a repository slot as the machine's admission admits, each record written as it is taken, tried without waiting; why not, said.</summary>
    private async Task<HeavyTaken?> TryTakeAsync(Queued queued)
    {
        var send = queued.Send;
        queued.Refused = null;
        if (HeavySlot.KeyOf(send.Checkout) is not { } key)
        {
            lock (_gate) _queue.Remove(queued);
            VerifySlot.Forget(home, send.Invocation);
            Settle(send, Unknown(send, $"git's files at {send.Checkout} do not say which repository it is a checkout of, so its heavy-run slot cannot be told"));
            return null;
        }

        var now = _world.Clock.GetUtcNow();
        var ask = new HeavyAsk(key, send.Repository, await AcceptedSlots.OfBlobAsync(send.Checkout, send.Accepted, _ends.Token).ConfigureAwait(false),
            () => new HeavyHolder(Environment.ProcessId, _started, send.Checkout, [], DateTimeOffset.UtcNow, null, JobName(home, send.Invocation)) { Run = send.Run })
        {
            Check = () => new VerifyHolder(Environment.ProcessId, _started, send.Run, send.Invocation, send.Repository, now),
            Gates = ["reached"],
        };
        var (taken, wait) = CheckAdmission.TryTake(home, _world.Rules(), ask);
        queued.Refused = wait;
        if (taken is null) Blocked(queued, wait!);
        return taken;
    }

    private void Blocked(Queued queued, CheckWait wait)
    {
        var what = wait.Sentence;
        if (queued.For == what) return;
        queued.For = what;
        queued.Wait = wait;
        Waiting(queued);
        _world.Said.WriteLine($"verify: {queued.Send.Repository}'s check (run {queued.Send.Run}) is waiting for {what}.");
    }

    /// <summary>The attempts due in the queue's order, each one's class read again now, never kept on the run, its row said again where it moved.</summary>
    private List<Queued> Ordered(List<Queued> due, DateTimeOffset now)
    {
        if (due.Count == 0) return due;
        var look = PriorityLook.Read(home, now);
        var standing = new List<Queued>();
        foreach (var each in due)
        {
            var priority = PriorityBook.OfRun(look.Events, each.Send.Run, each.Send.Ask, now);
            var word = HoldBook.Reading(look.Holds, each.Send.Run, each.Send.Ask);
            var held = word is { Held: true } ? $"{HoldBook.Said(word)}: it is tried again once the hold is lifted" : null;
            if (held is null) standing.Add(each);
            if (priority == each.Priority && (held is null || each.For == held)) continue;
            each.Priority = priority;
            if (held is not null) (each.For, each.Wait) = (held, null);
            lock (_gate)
            {
                if (_queue.Contains(each)) Waiting(each);
            }
        }

        return [.. QueueOrder.Of(standing, each => each.Priority.Class, each => each.Arrived).ThenBy(each => each.Order)];
    }

    private void Waiting(Queued queued) => VerifySlot.Wait(
        home, new VerifyWaiter(Environment.ProcessId, _started, queued.Send.Run, queued.Send.Invocation, queued.Send.Repository, queued.Since, queued.Order)
        {
            For = queued.For, Wait = queued.Wait,
            Class = queued.Priority.Class, Source = queued.Priority.Source, By = queued.Priority.Event?.By, Why = queued.Priority.Event?.Why,
            Arrived = queued.Arrived,
        });

    /// <summary>The attempt's run, both slots let go at its end; true where a busy runner sends it back to the queue for a later offer.</summary>
    private async Task<bool> RunAsync(Queued queued, HeavyTaken held)
    {
        var send = queued.Send;
        var key = HeavySlot.KeyOf(send.Checkout)!;
        var job = JobName(home, send.Invocation);
        var repository = held.Repository;
        var holder = held.Holder;
        VerifyResult? result = null;
        var busy = false;
        var ran = Stopwatch.StartNew();
        _world.Bus?.Tee(() => CheckBusFacts.CheckStarted(send));
        try
        {
            VerifySlot.Forget(home, send.Invocation);
            if (repository.Before is { Job: { } left } && OperatingSystem.IsWindows()) await ProcessJob.EndNamedAsync(left).ConfigureAwait(false);

            // The bound starts once both slots are held.
            using var bound = new CancellationTokenSource(send.Bound, _world.Clock);
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(bound.Token, queued.Stop.Token, _ends.Token);
            var candidate = await CandidateJob.OpenAsync(job).ConfigureAwait(false);
            try
            {
                var run = new VerifyExecution(home, send, candidate, _world, key, repository.Name, change =>
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
                // What the run took is read from its job while the job still holds it, then it is emptied before either slot goes.
                var whole = !stop.IsCancellationRequested && !_ends.IsCancellationRequested && !busy && result is { Verdict: VerifyVerdicts.Reached } done && done.Gates.All(gate => gate.Verdict != VerifyGate.Kept);
                new RunMeter(home, key, LoadKind.Test, _world.Clock.GetUtcNow).Note("reached", candidate.Job, ran.Elapsed, whole, result is { Verdict: VerifyVerdicts.Failed }, result?.Why);
                await candidate.DisposeAsync().ConfigureAwait(false);
            }

            if (_ends.IsCancellationRequested)
            {
                result = null;
                Closed(send);
            }
            else if (queued.Stop.IsCancellationRequested) result = (result ?? Unknown(send, "")) with { Verdict = VerifyVerdicts.Unknown, Why = queued.StopWhy is { } superseded ? $"{superseded}, and its job was ended" : "the person stopped it, and its job was ended", Code = VerifyResult.Stopped, Failed = null };
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
            held.Dispose();
        }

        if (busy && result is null && !_ends.IsCancellationRequested)
        {
            if (queued.Busy < BusyTries && !queued.Stop.IsCancellationRequested)
            {
                queued.Busy++;
                queued.NotBefore = _world.Clock.GetUtcNow() + BusyAgain;
                queued.For = RunnersOwnRun;
                queued.Wait = null;
                Waiting(queued);
                _world.Bus?.Tee(() => CheckBusFacts.CheckRequeued(send, queued.Busy));
                _world.Said.WriteLine($"verify: {send.Repository}'s runner is busy with its own run; offered again in {HeavySlot.Took(BusyAgain)}.");
                return true;
            }

            result = Unknown(send, "the runner's own run held its lock through every offer") with { Code = VerifyResult.Busy };
        }

        if (result is null)
        {
            // The driver ending: no result, so its restart sends the same attempt again, after this settle of its start.
            _world.Bus?.Tee(() => CheckBusFacts.CheckEnded(send, CheckBusFacts.Closing));
            return false;
        }

        Settle(send, result with { WallSeconds = ran.Elapsed.TotalSeconds });
        return false;
    }

    /// <summary>The executor's own close ended this send's job: its loss is explained (<see cref="LostChecks"/>).</summary>
    private void Closed(VerifySend send)
    {
        try
        {
            AttemptMarks.Write(AttemptMarks.Closed(home, send.Run, send.Invocation, send.Delivery), send.Delivery, _world.Clock.GetUtcNow());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            _world.Said.WriteLine($"verify: the close of run {send.Run}'s check could not be marked ({error.Message}); its loss counts as unexplained.");
        }
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
            _world.Bus?.Tee(() => CheckBusFacts.CheckEnded(send, CheckBusFacts.Unkept));
            return;
        }

        lock (_gate) _answered.Add(send.Invocation);
        if (result.Verdict == VerifyVerdicts.Reached) CheckArrivals.Forget(home, send.Run);
        var told = _world.Bus?.Tee(() => CheckBusFacts.CheckSettled(send, result)) == true;
        // The result stands on disk: a writer or a callback that throws must not end the loop, which nothing starts again.
        try
        {
            _world.Said.WriteLine($"verify: {send.Repository} at {TextBounds.ShortCommit(send.Commit, 7)}: {result.Verdict}{(result.Why is { } why ? $" ({why})" : "")}.");
            if (!told) _world.Answered?.Invoke(send.Run, send.Invocation);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Debug.WriteLine($"verify: telling of run {send.Run}'s result failed: {error.Message}");
        }
    }

    internal static VerifyResult Unknown(VerifySend send, string why) => new(send.Invocation, send.Commit, VerifyVerdicts.Unknown, why, []) { Declaration = send.Accepted };

    private static long ThisProcessStarted()
    {
        using var self = Process.GetCurrentProcess();
        return self.StartTime.ToUniversalTime().Ticks;
    }
}
