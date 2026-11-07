namespace Daoris.Driver;

/// <summary>
/// The watch over the sessions a driver runs: a run whose root has exited concluded by what it said within a bound, and the watcher's
/// look at the running sessions, each step said as a line.
/// </summary>
internal sealed class SessionWatching(string home, DriverConfig config, LoopContext context, SessionOutput? output)
{
    /// <summary>
    /// How long a run's capture is waited for once its root exited: the door's drain and the ten seconds of a lingering process fit
    /// inside it. Settable for a test, never read from <c>driver.json</c>.
    /// </summary>
    public TimeSpan CaptureBound { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 🔴 A run whose root has exited is concluded by what it said, within <see cref="CaptureBound"/>. Past it with its capture still
    /// held open, by something its agent started holding the harness's output, everything left in the run's job is ended, said as a note
    /// line (<c>ended.gone</c>), and the capture drains; one still open after <see cref="SessionWaits.AbandonedWait"/> is abandoned,
    /// still observed. Never a cut-off: the caller concludes by the exit, so a session that finished its turn and left a dev server
    /// holding its pipe ends as it finished.
    /// </summary>
    /// <returns>Whether the capture finished, so what it read may be awaited; false where it is abandoned.</returns>
    public async Task<bool> CaptureWithinAsync(Task capture, string sessionId)
    {
        if (await Task.WhenAny(capture, Task.Delay(CaptureBound)).ConfigureAwait(false) == capture)
        {
            await capture.ConfigureAwait(false);
            return true;
        }

        context.Processes.JobOf(sessionId)?.Terminate();
        var gone = Observation.Gone;
        output?.Append(sessionId, $"— {gone.Note}");
        context.Events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = gone.Note, Parts = gone.Parts }, say: null);
        context.Log?.Info("session.gone", ("session", sessionId), ("bound", (int)CaptureBound.TotalSeconds));
        if (await Task.WhenAny(capture, Task.Delay(SessionWaits.AbandonedWait)).ConfigureAwait(false) == capture)
        {
            await capture.ConfigureAwait(false);
            return true;
        }

        await SessionWaits.AbandonedAsync(null, capture, sessionId, context.Log, output, TimeSpan.Zero).ConfigureAwait(false);
        return false;
    }

    /// <summary>
    /// The watcher's look: its steps this look, each said as a line, which is the headless host's row, since it has no <i>What needs
    /// you</i>; the window's row is <c>NeedsAPerson</c>'s.
    /// </summary>
    /// <param name="said">The look's classing, each step's line classed as it is made.</param>
    public IReadOnlyList<string> Look(LookSaid said)
    {
        var watch = SessionWatch.Of(context.Events);
        watch.Log = context.Log;
        watch.Follow(QuietSetting.Of(config));
        var lines = new LookLines(said, "watch");
        var steps = watch.Look(
            id => context.Processes.JobOf(id) is { } job ? new JobRead(job.Holds, job.Cpu) : null, TimeSpan.FromSeconds(config.PollSeconds),
            HeavyWaitOf);
        foreach (var step in steps)
        {
            var who = step.Kind == SessionWatch.Intake ? $"intake {step.Session}" : $"session {step.Session}";
            switch (step)
            {
                case { Step: SessionWatch.Marked }:
                    lines.Progress($"watch  {who} is quiet: nothing heard from it for {step.Minutes} minutes, and nothing it runs uses the machine; "
                        + "Daoris looked in and ended nothing.");
                    break;
                case { Step: SessionWatch.Brought }:
                    var row = watch.RowOf(step.Session);
                    lines.Wait($"watch  {who} has said nothing for {step.Minutes} minutes"
                        + (row is { Busy: true } ? ", while something it runs keeps the machine busy" : ", and nothing it runs uses the machine")
                        + ": it needs you. Daoris ends nothing; stop it or leave it running from Daoris's window, or end this run with Ctrl-C."
                        + (watch.EndAfter is null ? " A headless run nobody watches takes --quiet-end <minutes>." : ""));
                    break;
                case { Step: SessionWatch.Ended }:
                    lines.Progress($"watch  {who} said nothing for {step.Minutes} minutes: --quiet-end ends its run, and its work goes on in the same tree.");
                    break;
                case { Outcome: "heard" or "busy" or "moving" or "waits" }:
                    lines.Progress($"watch  {who} is no longer quiet ({step.Outcome}).");
                    break;
            }
        }

        return lines.Said;
    }

    /// <summary>
    /// Whether a session waits for its repository's heavy-run slot, from the slot's records: the holder's session, or null for a run
    /// from a terminal; null where it waits for none, or its tree is in no checkout.
    /// </summary>
    private HeavyWait? HeavyWaitOf(string session, string? tree)
    {
        // A run asking for a place in the machine's admission waits for the machine, as one waiting for the slot waits for a run: it
        // names no session to help, and is brought to the person at three times the bound all the same.
        if (MachineAdmission.Look(home).Waits.Any(wait => string.Equals(wait.Session, session, StringComparison.OrdinalIgnoreCase))) return new HeavyWait(null);
        if (tree is null || HeavySlot.KeyOf(tree) is not { } key) return null;
        var look = HeavySlot.Look(home, key);
        return look.Waiters.Any(waiter => string.Equals(waiter.Session, session, StringComparison.OrdinalIgnoreCase))
            ? new HeavyWait(look.Holder?.Session)
            : null;
    }
}
