using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// What a look considered and what it waited on, in the shapes the tick hands the page: one writer each, since each shape is the
/// page's contract. Facts only, never the driver's English, so the page says them in the reader's language; a profile name rides
/// this bridge alone. The loop's doors answer through it.
/// </summary>
internal static class TickShown
{
    /// <summary>
    /// One quest: its id, its repository, the verdict and the driver's sentence, which the page translates by the verdict and never
    /// by its words.
    /// </summary>
    /// <remarks>
    /// <para>A <c>Stopped</c> verdict names the session stopped, so the page names it as a fact; the stop's tree stays here.</para>
    /// <para>An <c>Exhausted</c> verdict with its park read carries the number the planner parked it at and when its last session
    /// ended, which <i>What needs you</i> counts its wait from; the last session and its note stay here.</para>
    /// <para>A quest held at spawn because every account its start may use is cooling waits for an account, not parked and with no
    /// Retry: whose, which (null for the tool's own sign-in), until when, and whether the agent named the time. A wait on signed-out
    /// accounts alone has no time, and its quest says its accounts by <c>SignedOut</c>, whose and which, so the page names each with
    /// its sign-in.</para>
    /// <para>A <c>TakenElsewhere</c> verdict names the machine that took the quest and its session where a teammate's record named
    /// them, or that it was taken here after the session ended and the session here not carried on.</para>
    /// <para>Each is null for every verdict it does not belong to, which the bridge leaves out.</para>
    /// </remarks>
    /// <param name="park">The quest's park as the loop last read it (<see cref="QuestParkReader"/>), or null.</param>
    /// <param name="wait">The look's wait on a cooling account, where it holds this quest (<see cref="TickReport.Waits"/>), or null.</param>
    public static object Quest(Consideration consideration, QuestPark? park = null, AccountWait? wait = null)
    {
        var parked = consideration.Verdict == StartVerdict.Exhausted
                     && park is not null
                     && string.Equals(park.Quest, consideration.Quest.Id, StringComparison.OrdinalIgnoreCase)
            ? park
            : null;
        // A wait on signed-out accounts has no reset: its quest waits for a sign-in, which `SignedOut` says, never a time.
        var waiting = wait is { Until: not null } && wait.Quests.Contains(consideration.Quest.Id, StringComparer.OrdinalIgnoreCase)
            ? wait
            : null;
        return new
        {
            Quest = consideration.Quest.Id,
            Repository = consideration.Quest.To,
            Verdict = consideration.Verdict.ToString(),
            consideration.Reason,
            HeldBy = consideration.HeldBy?.Session,
            PausedBy = consideration.PausedBy is { } pause ? new { Scope = pause.Word, pause.Id } : null,
            parked?.Strikes,
            parked?.Since,
            // The name the person gave each account, read when the look said it; the id stays the fact a sign-in takes.
            WaitsFor = waiting is null ? null : new { waiting.Agent, waiting.Account, waiting.Name, waiting.Until, waiting.Stated },
            SignedOut = consideration.SignedOut is { } passed ? new { passed.Agent, passed.Accounts, passed.Names, passed.NoAccount } : null,
            ForUpdate = InstallUpdate.IsHeldForUpdate(consideration) ? true : (bool?)null,
            TakenBy = consideration.TakenBy is { } taken ? new { taken.Machine, taken.Session, taken.Here, taken.Last } : null,
            CannotShow = consideration.CannotShow is { } sat ? new { sat.Code, sat.Environment, sat.Address } : null,
            Said = Said(consideration.Said),
        };
    }

    /// <summary>
    /// Which of a verdict's sentences the planner said: the code and the facts its words are said from, the cap as a number; null
    /// where the planner named none. One shape for the tick and a work's holds.
    /// </summary>
    public static object? Said(SittingFact? said) => said is null
        ? null
        : new { said.Code, said.Cap, said.Session, said.Quest, said.Repository, said.Environment, said.State, said.Paths };

    /// <summary>
    /// One wait: starts held because every account they may use is cooling, one per account (<see cref="TickReport.Waits"/>), or held
    /// on signed-out accounts with none cooling, one per agent with no time, so <i>What needs you</i> lists a start waiting for accounts.
    /// </summary>
    /// <remarks>
    /// An ask's intake is among them, carried by id, since a consideration names a quest alone. Nothing is asked to build it: it is the
    /// look's own wait, read from the cool-offs and the refusals its starts met, so no row on the page starts a process to find out.
    /// </remarks>
    public static object Wait(AccountWait wait) => new
    {
        wait.Agent,
        wait.Account,
        wait.Name,
        wait.Workspace,
        wait.Until,
        wait.Stated,
        wait.Quests,
        wait.Asks,
        wait.SignedOut,
        wait.NoAccount,
    };
}
