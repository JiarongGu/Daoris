using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The roles' terminals: a role's terminal started and its hold kept, the holds a catalogue
/// change ended, and a holder's end. It reaches the standing confirmation's memory (<see cref="StandingState"/>) in one mutation step
/// under its one lock; a tab is opened, told and closed outside it.
/// </summary>
internal sealed class RoleTerminals(StandingConfirmation owner, StandingState state)
{
    private readonly MachineLog _log = owner.Log;
    private readonly TimeProvider _clock = owner.Clock;

    /// <summary>
    /// The window's start of a role's terminal (<c>START_ROLE_TERMINAL { role, share? }</c>): the role read from the catalogue as it stands,
    /// staffed by the terminal, held under a new generation by a new holder whose tab <paramref name="open"/> starts with a new key before
    /// the mutation step installs its hash, or, where the owner chose it, by a living holder the role is added to; nothing kept
    /// where the tab could not start or its harness is not Daoris's own (<see cref="HolderHarness.Refused"/>). The generation of a hold it
    /// replaced is answered for the caller to sweep after this step, never inside it.
    /// </summary>
    /// <param name="open">Opens a new holder's tab with the key in its harness's environment; called outside the lock.</param>
    public async Task<HoldChange> StartAsync(HoldRequest request, Func<HolderOpen, Task<HolderOpened>> open)
    {
        var role = request.Role;
        if (owner.Appointed().FirstOrDefault(each => each.Id == role) is not { Hold.Kind: { } kind } staffed)
        {
            return Refuse(StandingConfirmation.UnknownRole, $"This Daoris has no appointed role `{role}`.");
        }

        if (!staffed.Hold!.Terminal) return Refuse(StandingConfirmation.NotTerminal, $"`{role}` is not held by a terminal: staff it with the terminal among the roles first.");
        string? shared = null;
        IReadOnlyList<string> living;
        lock (state.Gate)
        {
            if (state.Holders.Count > 0 && request.Share is null)
            {
                return Refuse(StandingConfirmation.ChooseTerminal,
                    "A role's terminal Daoris started is open: add this role to it, or give it a terminal of its own.");
            }

            if (request.Share is { } named && named != "own")
            {
                if (!state.Holders.TryGetValue(named, out var target)) return Refuse(StandingConfirmation.NoHolder, "That terminal no longer holds a role: give this one a terminal of its own.");
                if (target.Revoked.Contains(role) || state.Holds.Any(each => each.Holder == target.Id && each.Role == role))
                {
                    return Refuse(StandingConfirmation.RevokedHolder, $"The {kind} was turned off in that terminal this run: it is started again only in a terminal of its own.");
                }

                shared = named;
            }

            living = [.. state.Holders.Keys];
        }

        var now = _clock.GetUtcNow();
        string? key = null;
        HolderOpened? opened = null;
        var id = "h" + Guid.NewGuid().ToString("N")[..11];
        if (shared is null)
        {
            // The key is drawn here, handed to the holder's harness alone, and never kept: only its hash is.
            key = HolderKey.Draw();
            try
            {
                opened = await open(new HolderOpen(id, key, [kind], now, living)).ConfigureAwait(false);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException
                                              or System.ComponentModel.Win32Exception)
            {
                opened = new HolderOpened(null, null, StandingConfirmation.Unstarted, null);
            }

            if (opened.Tab is null)
            {
                _log.Warn(StandingConfirmation.RoleLogEvent, ("state", opened.Why ?? StandingConfirmation.Unstarted), ("role", role));
                return opened.Why == HolderHarness.Refused
                    ? Refuse(HolderHarness.Refused,
                        $"Daoris starts only its own pinned claude-code as a role's terminal, and {opened.Message ?? "it has none"}: pin claude-code "
                        + "and install it from its maker's channel (`daoris agent pin claude-code <version>`), then start it again. Nothing was started.")
                    : Refuse(StandingConfirmation.Unstarted, $"The {kind}'s terminal could not be started, so nothing was kept.");
            }
        }

        var hold = new StandingState.Hold(role, kind, Guid.NewGuid().ToString("N"), shared ?? id, now);
        var told = new List<(string Tab, string Line, string? Title)>();
        IReadOnlyList<string> ended = [];
        HoldChange? refused = null;
        var cancelled = 0;
        lock (state.Gate)
        {
            if (opened?.Tab is { } tab && state.EndedTabs.Contains(tab))
            {
                // A tab that ended while its start is still opening it: the hold is never kept.
                refused = Refuse(StandingConfirmation.Unstarted, $"The {kind}'s terminal ended as it opened, so nothing was kept.");
            }
            else if (shared is not null && !state.Holders.ContainsKey(shared))
            {
                refused = Refuse(StandingConfirmation.NoHolder, "That terminal no longer holds a role: give this one a terminal of its own.");
            }
            else
            {
                var replaced = state.Holds.Where(each => each.Role == role).ToList();
                cancelled = state.Revoked(replaced, told);
                if (shared is null) state.Holders[id] = new StandingState.Holding(id, HolderKey.Hash(key)!, opened!.Tab, opened.Process, now);
                state.Holds.Add(hold);
                if (shared is not null && state.Holders[shared].Tab is { } sharedTab)
                {
                    var roles = state.RolesOf(shared);
                    told.Add((sharedTab, HolderTerminal.Added(kind, now, roles), HolderTerminal.Title(roles)));
                }

                ended = [.. replaced.Select(each => each.Generation)];
            }
        }

        if (refused is not null)
        {
            // A tab opened for a hold that is not kept is closed: its key admits nothing.
            if (opened?.Tab is { } orphan) owner.Close?.Invoke(orphan);
            return refused;
        }

        Said(told);
        if (cancelled > 0) _log.Info("role.cancelled", ("role", kind), ("count", StandingState.Count(cancelled)));
        _log.Info(StandingConfirmation.RoleLogEvent, ("state", shared is null ? "started" : "added"), ("role", kind), ("scope", staffed.Scope.Kind));
        return new HoldChange(new StandingChange(true, null, null), ended);
    }

    /// <summary>
    /// The catalogue changed (a role's press through the role door): every hold whose role no longer reads as held by the terminal, or
    /// as the kind it is started as, ended in one mutation step, its admitted acts, pending operations and bindings cancelled; a holder
    /// left holding nothing goes, its tab told so; a catalogue that cannot be read ends none. Its generations are answered for the caller to sweep after this step.
    /// </summary>
    public IReadOnlyList<string> RolesChanged()
    {
        if (owner.Slots() is not { } slots)
        {
            _log.Warn(StandingConfirmation.RoleLogEvent, ("state", "unread"));
            return [];
        }

        var staffed = StandingConfirmation.AppointedOf(slots);
        var told = new List<(string Tab, string Line, string? Title)>();
        IReadOnlyList<string> ended;
        int cancelled;
        lock (state.Gate)
        {
            var gone = state.Holds.Where(each => staffed.FirstOrDefault(role => role.Id == each.Role) is not { Hold: { Terminal: true } held } || held.Kind != each.Kind).ToList();
            cancelled = state.Revoked(gone, told);
            ended = [.. gone.Select(each => each.Generation)];
        }

        Said(told);
        if (ended.Count > 0) _log.Info(StandingConfirmation.RoleLogEvent, ("state", "ended"), ("count", StandingState.Count(ended.Count)));
        if (cancelled > 0) _log.Info("role.cancelled", ("count", StandingState.Count(cancelled)));
        return ended;
    }

    /// <summary>
    /// A holder's tab ended (<c>HolderEnded</c>), by any of its paths: the holder's hash and every hold it holds dropped in one
    /// mutation step, their acts and operations cancelled; the generations it ended answered for the sweep, and told to
    /// <see cref="StandingConfirmation.Ended"/>. A tab that ends while its start still opens it is remembered, so that hold is never kept.
    /// </summary>
    public IReadOnlyList<string> HolderEnded(string tab)
    {
        IReadOnlyList<string> ended;
        int cancelled;
        lock (state.Gate)
        {
            state.EndedTabs.Add(tab);
            if (state.Holders.Values.FirstOrDefault(each => each.Tab == tab) is not { } holding) return [];
            (ended, cancelled) = state.EndHolder(holding);
        }

        _log.Info(StandingConfirmation.RoleLogEvent, ("state", "holder-ended"), ("count", StandingState.Count(ended.Count)));
        if (cancelled > 0) _log.Info("role.cancelled", ("count", StandingState.Count(cancelled)));
        if (ended.Count > 0) owner.Ended?.Invoke(ended);
        return ended;
    }

    private void Said(List<(string Tab, string Line, string? Title)> told)
    {
        foreach (var (tab, line, title) in told) owner.Tell?.Invoke(tab, line, title);
    }

    private static HoldChange Refuse(string why, string message) => new(new StandingChange(false, why, message), []);
}
