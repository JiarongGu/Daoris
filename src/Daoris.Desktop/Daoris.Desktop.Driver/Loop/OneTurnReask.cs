namespace Daoris.Driver;

/// <summary>
/// A one-turn owner's refusal at the open, handled inside its own call (D205 §3.5(f), MODELROLE1e): a second opinion's reviewer, a helper
/// and a partner each walk their entries before the spawn, and their agent may still refuse the entry's model at the open, before any
/// prompt. None of them has a next look to plan it again, so the refused (entry, account) is passed and the owner's own walk is asked
/// again: a later entry of the same agent on the same account is set on the open session, and anything else ends the refused record
/// <c>choice-not-offered</c> and opens the walk's answer in the same call. Nothing left: the owner's own outcome (no reviewer could run,
/// <c>not-started</c>, refused) with the pass reasons. Never a cool-off, never stored, gone when the owner's call ends.
/// </summary>
internal sealed class OneTurnReask
{
    private readonly Func<IReadOnlyList<ReviewerPassed>, Task<ReviewerChoice>> _walk;
    private readonly List<ReviewerPassed> _passed = [];

    /// <param name="first">The choice the owner's walk answered, whose entry runs first.</param>
    /// <param name="walk">The owner's own walk, asked again with what was passed left out.</param>
    public OneTurnReask(ReviewerChoice first, Func<IReadOnlyList<ReviewerPassed>, Task<ReviewerChoice>> walk)
    {
        Current = first;
        _walk = walk;
    }

    /// <summary>
    /// The opening note's actor line (§4.6) when a later entry of the actor ran, its passes the walk's own: <c>Ran as Second opinion's choice
    /// 2, …</c>; nothing where entry 1 ran or nothing was passed.
    /// </summary>
    public static void Say(SessionEvents events, string sessionId, string actor, ReviewerChoice choice, MachineLog? log = null)
    {
        if (choice.Effective is not { } ran) return;
        var passed = choice.Tried.Where(each => each.Entry is not null).Select(each => new BuildPass(each.Entry!, each.Held, each.Refusal)).ToList();
        RotatedOpening.SayActor(events, sessionId, actor, ran, passed);
        // The machine log's line (§4.6): the choice and each passed one with its code, as every actor's start writes them.
        log?.Write("info", "actor.chose", [("actor", actor), ("session", sessionId), ("choice", ran.From), ("agent", ran.Agent), ("model", ran.Model),
            ("passed", passed.Select(pass => pass.Said).ToList())]);
    }

    /// <summary>The choice that runs now: the first, or the one a refusal at the open moved it to.</summary>
    public ReviewerChoice Current { get; private set; }

    /// <summary>The (entry, account) passed so far in this owner's call.</summary>
    public IReadOnlyList<ReviewerPassed> Passed => _passed;

    /// <summary>
    /// The open's refusal of the running entry's model: passed on its account, the walk asked again, and a later entry of the same agent on
    /// the same account (<c>Asked</c>, <c>Choice</c>) answered to be set on the open session; null where anything else is the answer, and the
    /// record ends.
    /// </summary>
    public async Task<(ActorAsked Asked, string Choice)?> SameAccountAsync()
    {
        if (!Pass(Current)) return null;
        var again = await _walk([.. _passed]).ConfigureAwait(false);
        var running = Current;
        if (again is { Chosen: true, Effective: { } later } && running.Effective is { } was && later != was
            && string.Equals(later.Agent, was.Agent, StringComparison.OrdinalIgnoreCase)
            && string.Equals(again.Selection?.Profile, running.Selection?.Profile, StringComparison.OrdinalIgnoreCase))
        {
            // The same session goes on with the walk's later entry; its account's selection stays the one it opened on.
            Current = again with { Selection = running.Selection };
            return (later.Asked, later.From);
        }

        return null;
    }

    /// <summary>
    /// The refused record has ended <c>choice-not-offered</c>: the walk's answer for what is left, its (entry, account) pass kept if the open's
    /// callback did not. A choice that cannot run says why, with the pass reasons, as the owner's walk always has.
    /// </summary>
    public async Task<ReviewerChoice> NextAsync()
    {
        Pass(Current);
        var next = await _walk([.. _passed]).ConfigureAwait(false);
        Current = next;
        return next;
    }

    /// <summary>
    /// The running (entry, account) as passed; an agent running under its own sign-in names no account, and its entry is passed whole.
    /// False where the choice names no entry to leave out.
    /// </summary>
    private bool Pass(ReviewerChoice choice)
    {
        if (choice.Effective is not { } entry) return false;
        var account = choice.Selection?.Profile is { Length: > 0 } named ? named : null;
        if (!_passed.Any(each => each.Entry == entry && string.Equals(each.Account, account, StringComparison.OrdinalIgnoreCase)))
        {
            _passed.Add(new ReviewerPassed(entry, account));
        }

        return true;
    }
}
