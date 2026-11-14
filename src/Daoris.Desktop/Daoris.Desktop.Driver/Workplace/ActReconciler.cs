using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>Why a writer of a checkout is refused for this look by an act there: the act, and what holds it, in words naming the repository's page.</summary>
public sealed record ActHold(string Act, string Said);

/// <summary>
/// The act journal's reconciliation: at the driver's start and by every writer taking the checkout
/// lock, before its judgement and outside its memo scopes, every act not closed in the checkout is taken from its dead run, one reconciler
/// winning, and decided by its postconditions. Nothing is run again: no hook, commit, push or configuration write.
/// </summary>
/// <remarks>
/// <para><b>A live step refuses every writer</b>: one whose job, found by its name, still holds a process, or whose pid still runs under the
/// start it journaled; a job that will not open is held live. Past its deadline the reconciler ends the job and awaits it empty, else says it
/// <i>unended</i> with its pid and the Stop that ends it. A step with no end whose processes are gone is cut, said so, and its postconditions
/// decide with the rest.</para>
/// <para><b>Held, never taken</b>: an act whose run still lives, one written by a newer build (its format, phase or step kind unknown here), and
/// one the store would not let this run write for now (busy, or stamped by a newer build). Each is said, and asked again at the next look.</para>
/// <para><b>At the driver's start</b> (<see cref="StartAsync"/>) it never holds the come-up: it runs beside the first looks, one
/// checkout at a time, each under its own lock alone, a checkout it cannot take left to the next look; and the closed acts whose recovery
/// expired let go of their own refs, each in one transaction at its journaled id.</para>
/// </remarks>
public sealed class ActReconciler(ActJournal journal)
{
    /// <summary>How long a step's processes are awaited once ended, before they are said unended.</summary>
    internal TimeSpan ExitBound { get; init; } = ChildProcess.ExitBound;

    /// <summary>The test seam for ending a job past a step's deadline; null ends it (<see cref="ProcessJob.Terminate"/>).</summary>
    internal Action<ProcessJob>? Ending { get; set; }

    /// <summary>The test seam for the moment a deadline is read against; null for the journal's clock.</summary>
    internal Func<DateTimeOffset>? Now { get; set; }

    /// <summary>The reconciliation a writer of the checkout runs under its lock, in <paramref name="home"/>'s journal under this process's run.</summary>
    public static Task<ActHold?> ForWriterAsync(string home, string checkout, CancellationToken ct) =>
        new ActReconciler(new ActJournal(home)).UnderLockAsync(checkout, ct);

    /// <summary>
    /// Every act not closed in the checkout reconciled, its caller holding the lock alone: null where none is left open, else the hold refusing the writer.
    /// </summary>
    public async Task<ActHold?> UnderLockAsync(string checkout, CancellationToken ct)
    {
        // Outside the writer's memo scopes (a landing's facts, a judgement's refs): what the reconciler reads is git's.
        using var facts = GitFacts.Outside();
        using var refs = RefSnapshot.Outside();
        IReadOnlyList<ActRecord> open;
        try
        {
            open = journal.UnclosedIn(TreeLock.CommonDirOf(checkout));
        }
        catch (Exception error) when (error is SqliteException or DesktopStoreException or IOException)
        {
            return new ActHold("", $"the act journal could not be read ({error.Message.Trim()}), so `{checkout}` is left for the next look");
        }

        foreach (var act in open)
        {
            if (await OneAsync(act, ct).ConfigureAwait(false) is { } hold) return hold;
        }

        return null;
    }

    /// <summary>
    /// The driver's start: every checkout with an act not closed, one at a time under its own lock alone, and the expired recovery let go of.
    /// What it says is what held an act or left it; an act closed says nothing here and is read from its record.
    /// </summary>
    public async Task<IReadOnlyList<string>> StartAsync(CancellationToken ct)
    {
        var said = new List<string>();
        IReadOnlyList<ActRecord> open;
        try
        {
            open = journal.Unclosed();
        }
        catch (Exception error) when (error is SqliteException or DesktopStoreException or IOException)
        {
            return [$"the act journal could not be read ({error.Message.Trim()}), so its acts are reconciled at the next look"];
        }

        foreach (var checkout in open.GroupBy(each => each.CommonDir, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            var act = checkout.First();
            using var held = ActRuns.Hold(journal.Home, act.Checkout, act.Repository);
            if (held is null)
            {
                said.Add($"`{act.Repository}`'s checkout was held by another of Daoris's writes, so act {act.Id} is reconciled at the next look");
                continue;
            }

            if (await UnderLockAsync(act.Checkout, ct).ConfigureAwait(false) is { } hold) said.Add(hold.Said);
        }

        said.AddRange(await ExpireAsync(ct).ConfigureAwait(false));
        return said;
    }

    /// <summary>Each closed act whose recovery expired: its own ref deleted at its journaled id, or kept and said where it moved.</summary>
    private async Task<IReadOnlyList<string>> ExpireAsync(CancellationToken ct)
    {
        var said = new List<string>();
        foreach (var act in journal.Expiring(Moment()))
        {
            ct.ThrowIfCancellationRequested();
            var kept = act.Recovery is null ? null : JsonNode.Parse(act.Recovery)?["actRef"]?["id"]?.GetValue<string>();
            if (kept is null)
            {
                ActFiles.Remove(journal.Home, act.Id);
                journal.Expired(act.Id);
                continue;
            }

            var gone = await ActRefs.ExpireAsync(act.Checkout, act.Id, kept, ct).ConfigureAwait(false);
            var stands = await StandsAsync(act.Checkout, ActRefs.Of(act.Id), ct).ConfigureAwait(false);
            if (gone.Done || stands is { Length: 0 })
            {
                ActFiles.Remove(journal.Home, act.Id);
                journal.Expired(act.Id);
            }
            else said.Add($"act {act.Id}'s own ref `{ActRefs.Of(act.Id)}` no longer stands at {TextBounds.ShortCommit(kept, 8)}, so it was kept ({gone.Said})");
        }

        return said;
    }

    /// <summary>One act reconciled: null where it is closed now, or why it holds the writer.</summary>
    private async Task<ActHold?> OneAsync(ActRecord act, CancellationToken ct)
    {
        var steps = journal.Steps(act.Id);
        if (act.Format > ActJournal.Format || !ActPhases.All.Contains(act.Phase, StringComparer.Ordinal)
            || steps.Any(step => !ActStepKinds.All.Contains(step.Kind, StringComparer.Ordinal)))
        {
            return Hold(act, $"act {act.Id} ({act.Kind}) in `{act.Repository}`'s checkout was written by a newer Daoris, which reconciles it; this build leaves "
                + "the checkout as it is: see the repository's page");
        }

        ActClaim claim;
        if (string.Equals(act.ClaimRun, journal.Run, StringComparison.Ordinal))
        {
            if (ActRuns.RunsHere(act.Id)) return Hold(act, $"act {act.Id} ({act.Kind}) is running in `{act.Repository}`'s checkout: see the repository's page");
            claim = new ActClaim(act.Id, act.ClaimRun, act.ClaimGeneration);
        }
        else
        {
            ActTaken taken;
            try
            {
                taken = journal.Take(act.Id, act.ClaimGeneration);
            }
            catch (DesktopStoreBusyException)
            {
                return Busy(act);
            }
            catch (DesktopStoreNewerException)
            {
                return Hold(act, $"a newer Daoris stamped the desktop's store, so act {act.Id} in `{act.Repository}`'s checkout is left to it: see the repository's page");
            }

            if (taken.Claim is null)
            {
                if (journal.Act(act.Id) is { Open: false }) return null;
                return Hold(act, $"act {act.Id} ({act.Kind}) is open in `{act.Repository}`'s checkout and {taken.Refused}: see the repository's page");
            }

            claim = taken.Claim;
        }

        try
        {
            foreach (var step in steps.Where(each => each.Open || each.Unended))
            {
                if (await LiveAsync(act, step, claim).ConfigureAwait(false) is { } live) return live;
            }

            var (outcome, _) = await CloseAsync(claim, act.Checkout, null, ct).ConfigureAwait(false);
            return outcome is null ? Hold(act, $"git could not read act {act.Id}'s postconditions in `{act.Repository}`'s checkout, so it is reconciled at the next look") : null;
        }
        catch (DesktopStoreBusyException)
        {
            return Busy(act);
        }
        catch (ActRefusedException lost)
        {
            return journal.Act(act.Id) is { Open: false } ? null : Hold(act, $"act {act.Id} was not reconciled here ({lost.Why})");
        }
    }

    /// <summary>A step that may still run: its hold while it does, ended past its deadline, or null where nothing of it runs now.</summary>
    private async Task<ActHold?> LiveAsync(ActRecord act, ActStep step, ActClaim claim)
    {
        ProcessJob? job;
        try
        {
            job = ProcessJob.OpenToEnd(act.Job);
        }
        catch (System.ComponentModel.Win32Exception error)
        {
            return Hold(act, $"act {act.Id}'s job would not open ({error.Message}), so its step {step.Ordinal} is held live: see the repository's page");
        }

        using (job)
        {
            var inJob = job?.Active is > 0;
            var byPid = step.Pid is { } pid && step.PidStarted is { } started && ProcessLiveness.Alive(pid, started);
            if (!inJob && !byPid)
            {
                if (step.Open)
                {
                    journal.End(claim, step.Ordinal, null, false,
                        $"its start at {Moments.Write(step.BeganAt)} has no end: the Daoris process that ran it ended, and its job with it; its postconditions decide");
                }

                return null;
            }

            var who = step.Pid is { } id ? $"pid {id}" : "its job's processes";
            if (Moment() < step.Deadline)
            {
                return Hold(act, $"act {act.Id}'s step {step.Ordinal} runs until {Moments.Write(step.Deadline)} ({who}): nothing is written to `{act.Repository}`'s "
                    + "checkout beside it; see the repository's page");
            }

            var ended = job is not null ? await EndJobAsync(job).ConfigureAwait(false) : await EndPidAsync(step).ConfigureAwait(false);
            if (!ended)
            {
                return Hold(act, $"act {act.Id}'s step {step.Ordinal} did not end when stopped past its deadline ({who}): Daoris could not tell whether it "
                    + $"finished, and nothing was retried; press Stop on `{act.Repository}`'s page, or end it yourself"
                    + (step.Pid is { } stop ? $": taskkill /PID {stop} /T /F" : ""));
            }

            if (step.Open)
            {
                journal.End(claim, step.Ordinal, null, false, $"it ran past its deadline {Moments.Write(step.Deadline)} and was ended by the reconciler; its postconditions decide");
            }

            return null;
        }
    }

    private async Task<bool> EndJobAsync(ProcessJob job)
    {
        (Ending ?? (each => each.Terminate()))(job);
        return await job.WaitEmptyAsync(ExitBound).ConfigureAwait(false);
    }

    /// <summary>A step's process found by its pid alone, outside any job: its tree ended and its exit awaited.</summary>
    private async Task<bool> EndPidAsync(ActStep step)
    {
        try
        {
            using var process = Process.GetProcessById(step.Pid!.Value);
            if (Ending is null) process.Kill(entireProcessTree: true);
            using var bound = new CancellationTokenSource(ExitBound);
            await process.WaitForExitAsync(bound.Token).ConfigureAwait(false);
            return true;
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (Exception error) when (error is OperationCanceledException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// The postconditions of every step that began read, each step's outcome and the act's written, and the record closed: the outcome and
    /// its words, or a null outcome where git or a file could not say, nothing written. <paramref name="why"/> says why no step ran, where none did.
    /// </summary>
    internal async Task<(string? Outcome, string Said)> CloseAsync(ActClaim claim, string checkout, string? why, CancellationToken ct = default)
    {
        var steps = journal.Steps(claim.Act);
        var outcomes = new Dictionary<int, string>();
        foreach (var step in steps.Where(each => each.BeganAt is not null))
        {
            if (await ActSteps.ReadAsync(checkout, step, ct).ConfigureAwait(false) is not { } outcome) return (null, "");
            outcomes[step.Ordinal] = outcome;
        }

        var act = journal.Act(claim.Act)!;
        var whole = outcomes.Count == 0 ? ActOutcomes.NotDone
            : outcomes.Values.Contains(ActOutcomes.Unknown) ? ActOutcomes.Unknown
            : outcomes.Values.All(each => each == ActOutcomes.Done) && outcomes.Count == steps.Count ? ActOutcomes.Done
            : outcomes.Values.All(each => each == ActOutcomes.NotDone) ? ActOutcomes.NotDone
            : ActOutcomes.Partial;
        var said = $"act {act.Id} ({act.Kind}) in `{act.Repository}`'s checkout: "
            + (why is not null ? $"{why}; " : "")
            + string.Join(", ", steps.Select(step => $"step {step.Ordinal} ({step.Kind}) {outcomes.GetValueOrDefault(step.Ordinal, "not begun")}"))
            + whole switch
            {
                ActOutcomes.Unknown => ". Daoris could not tell whether this finished; nothing was retried",
                ActOutcomes.Partial => ". Part of it stands; nothing was retried",
                _ => "",
            };
        journal.Reconciled(claim, whole, said, outcomes);
        var kept = act.Recovery is not null && JsonNode.Parse(act.Recovery)?["actRef"] is JsonObject;
        journal.Close(claim, kept ? journal.Clock.GetUtcNow() + ActRuns.Kept : null);
        return (whole, said);
    }

    private static async Task<string?> StandsAsync(string root, string reference, CancellationToken ct)
    {
        var listed = await MachinePorts.Repository.RefsAsync(root, [reference], null, null, ct).ConfigureAwait(false);
        return listed is null ? null : string.Join(",", listed.Where(each => each.Name == reference).Select(each => each.Name));
    }

    private DateTimeOffset Moment() => Now?.Invoke() ?? journal.Clock.GetUtcNow();

    private static ActHold Hold(ActRecord act, string said) => new(act.Id, said);

    private static ActHold Busy(ActRecord act) =>
        new(act.Id, $"the desktop's store was busy, so act {act.Id} in `{act.Repository}`'s checkout is reconciled at the next look");
}
