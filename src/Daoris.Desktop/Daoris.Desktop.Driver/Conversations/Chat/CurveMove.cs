using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A conversation over its account's usage curve moves on its own as its turn ends (SWITCH1c-ii; D165 point 3; the switch design
/// §1.2, §2 and §4): to another account of the same agent in its scope that has room, by the hand-over's box (MIXAGENT1a, D163)
/// with its agent unchanged, why <c>curve</c>, asked by <c>daoris</c>. <see cref="SwitchDecision"/> decides; this class asks it,
/// reads the others where it says so, asks again, and asks the box, whose take (<see cref="ChatHandOver.TakeAsync"/>) ends the old
/// conversation and starts the new one, its record continuing the old. Over the runner's context (PART10g, D166).
/// </summary>
/// <remarks>
/// <para><b>Never mid-turn, never during a take.</b> A conversation is judged once per turn's end, as its turns tell it with
/// nothing running, opening or queued (<see cref="TurnJustEnded"/>); never while a hand-over waits or is taken, whose box is kept
/// until its take is done; and the take holds the turns (<see cref="ChatTurns.Hold"/>), so a word said as it begins either runs
/// its turn first, the box then waiting for that turn's end, or takes nothing.</para>
/// <para><b>Never across agents, never outside the scope's list.</b> The candidates are the other accounts of the scope's list of
/// the conversation's own agent; another agent is the <c>handOver</c> rule's (MIXAGENT1c). Where none has room it stays and says
/// nothing: the person's offer (MIXAGENT1b) is the fallback. <c>switch: off</c> moves nothing.</para>
/// </remarks>
/// <param name="handOver">The hand-over a move asks and takes: a move by the curve is a hand-over with its agent unchanged.</param>
internal sealed class CurveMove(ChatContext context, ChatHandOver handOver)
{
    private readonly ChatContext _context = context;

    private readonly ChatHandOver _handOver = handOver;

    /// <summary>What each judged turn's end came to, by session: a test hears it; no person reads it.</summary>
    public event Action<string, SwitchChoice>? Judged;

    /// <summary>
    /// Whether the turns, as just told, are a turn's end not yet judged (SWITCH1c-ii): nothing running, the door not opening, none
    /// of the person's words waiting, and a turn ended since <paramref name="judged"/>. A change for any other reason judges nothing.
    /// </summary>
    /// <param name="judged">The turn's end the conversation was last judged at, or null before any.</param>
    public static bool TurnJustEnded(ChatQueue queue, DateTimeOffset? judged) =>
        !queue.Taking && !queue.Opening && queue.Queued.Count == 0 && queue.LastTurnEnded is { } ended && ended != judged;

    /// <summary>A turn's end, told as the turns move: judged on a task of its own, as <see cref="ChatHandOver.Due"/>'s take is.</summary>
    public void Due(string sessionId, DriverConfig config, Func<string, string, Task>? onEnded)
    {
        if (_context.Closing) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await MoveAsync(sessionId, config, onEnded).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A judgement that failed moves nothing, and the conversation runs on where it is: the next turn's end judges again.
            }
        });
    }

    /// <summary>
    /// Judge a conversation by its account's usage curve and, where the decision says so, move it (SWITCH1c-ii). Its account, agent
    /// and workspace are its record's; the scope's switch and curve, the scope's list and what each account said are this machine's.
    /// </summary>
    /// <returns>What the decision said; null where nothing was judged (a hand-over waits or is taken, or it is no conversation of
    /// an account of an agent whose accounts the roster holds).</returns>
    public async Task<SwitchChoice?> MoveAsync(
        string sessionId, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct = default)
    {
        if (_context.Closing || _context.GoingOn.ContainsKey(sessionId) || new HandOverBox(_context.Home).Read(sessionId) is not null)
        {
            return null;
        }

        // Ask Daoris's own conversations and intakes never go on elsewhere (D137 §2.2), and one on the tool's own home has no account.
        var prior = await _context.Service.RecordAsync(sessionId, ct).ConfigureAwait(false);
        if (prior is not { Kind: "chat", Teammate: false, Ask: null, Profile: { Length: > 0 } account, Adapter: { Length: > 0 } adapter }
            || string.Equals(prior.Repository, HelpRoom.Repository, StringComparison.Ordinal))
        {
            return null;
        }

        string owner;
        string agent;
        RotationScope scope;
        try
        {
            var resolved = _context.Harnesses.Adapters.Resolve(adapter);
            if (resolved.Toolchain is not { } toolchain) return null;
            (agent, owner) = (resolved.Name, toolchain.Owner(resolved.Name));
            scope = _context.Harnesses.Settings.Set(owner);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }

        // Its last move by the curve is when its record was made, where it continues another by the curve (§2's hold).
        var lastMove = prior.FromWhy == HandOverWhy.Curve ? prior.Created : null;
        var choice = Decide(othersRead: false);
        if (choice is SwitchChoice.ReadOthers { Accounts: var stale })
        {
            await _context.Harnesses.ReadUsageAsync(agent, owner, stale, config, ct).ConfigureAwait(false);
            choice = Decide(othersRead: true);
        }

        Judged?.Invoke(sessionId, choice);
        if (choice is not SwitchChoice.MoveTo move) return choice;

        _context.CurveMoves[sessionId] = move;
        var asked = await HandOver.AskAsync(
            _context.Service, _context.Home, _handOver.AccountsOf, sessionId, agent, move.Account, HandOverWhy.Curve, HandOverBy.Daoris,
            DateTimeOffset.UtcNow, ct)
            .ConfigureAwait(false);
        if (!asked.Kept)
        {
            _context.CurveMoves.TryRemove(sessionId, out _);
            return choice;
        }

        // Taken now, the turns held while it is; where a word started a turn meanwhile, the box waits for that turn's end.
        await _handOver.TakeAsync(sessionId, config, onEnded, ct).ConfigureAwait(false);
        return choice;

        SwitchChoice Decide(bool othersRead)
        {
            var now = _context.Harnesses.Clock();
            return SwitchDecision.Decide(
                scope.Use, account, SaidOf(owner, account, now), _context.Harnesses.SwitchCandidates(owner, scope.List), now, lastMove,
                othersRead);
        }
    }

    /// <summary>What the conversation's own account last said of its windows: its frames, or a read; null for nothing.</summary>
    private AccountSaid? SaidOf(string owner, string account, DateTimeOffset now)
    {
        try
        {
            return AccountWindows.SaidOf(_context.Harnesses.Home, owner, account, now);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
