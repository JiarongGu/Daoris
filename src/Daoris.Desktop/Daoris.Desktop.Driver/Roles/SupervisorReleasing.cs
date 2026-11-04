using System.Security.Cryptography;

namespace Daoris.Driver;

/// <summary>
/// The look's moves on a held decision (§3.4, §3.6, §6.3): the release that says the word, the settling of a record the owner or the run
/// moved, the owner's overrule and the owner's words superseding. Every move is made under the session's lock in the
/// <see cref="SupervisorBook"/>; this type holds no state of its own.
/// </summary>
internal sealed class SupervisorReleasing(SupervisorBook book, IDecisionAdmission admission, MachineLog? log)
{
    /// <summary>
    /// Release each held decision of <paramref name="session"/> whose <c>goesOnAt</c> has come, in one conditional step: under the
    /// decision's lock, live and held in memory and its generation still effective, the service's say is called with no text and the
    /// receipt as its key; only its kept word's id moves it to <c>released</c>. A lost response keeps it held with its receipt, which the
    /// next look repeats; a refusal moves it to <c>stale</c>, or <c>superseded</c> where the owner's words came first.
    /// </summary>
    /// <returns>The states the session's decisions moved to; a word a refusal leaves claiming the decision is the quarantine's.</returns>
    public async Task<IReadOnlyList<SupervisorState>> ReleaseAsync(
        string session, SupervisorSay say, DateTimeOffset now, CancellationToken ct = default, SupervisorBound? bound = null)
    {
        var moved = new List<SupervisorState>();
        foreach (var id in book.LiveIds(session))
        {
            var state = await book.UnderAsync(session, async () =>
            {
                if (!book.Live.TryGetValue(id, out var decision) || decision.State != SupervisorState.Held) return (SupervisorState?)null;
                if (decision.Receipt is null && now < decision.GoesOnAt) return null;
                if (admission.Admit(decision.Repository) is not { Why: null, Generation: { } generation } || generation != decision.Generation)
                {
                    return book.Move(decision, SupervisorState.Stale, now, SupervisorWhy.Revoked);
                }

                // SUPERVISOR1b (§4.3): the line's count read again here, under the decision's lock, before anything is said; the question's
                // mark is read again by the service's own step, which refuses `owner-question` from the file it reads.
                if (bound is not null && await bound(session, ct).ConfigureAwait(false))
                {
                    return book.Move(decision, SupervisorState.Stale, now, SupervisorWhy.SupervisorBound);
                }

                decision.Receipt ??=Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
                SupervisorSaid said;
                try
                {
                    said = await say(session, decision.Id, decision.Question.Id, decision.Choice, decision.Receipt, ct).ConfigureAwait(false);
                }
                catch (Exception error) when (error is HttpRequestException or IOException or TaskCanceledException)
                {
                    said = new SupervisorSaid(null, null, Lost: true);
                }

                if (said.Lost) return null;
                if (said.Word is { } word)
                {
                    decision.Word = word;
                    return book.Move(decision, SupervisorState.Released, now, null);
                }

                if (said.Refusal == "decision-taken")
                {
                    // A word claiming this decision under another key was never the release's: it is taken back by the look's quarantine.
                    log?.Warn("supervisor.unattributed", ("session", session));
                    return book.Move(decision, SupervisorState.Stale, now, SupervisorWhy.Forged);
                }

                return said.Refusal == SupervisorWhy.OwnerAnswered
                    ? book.Move(decision, SupervisorState.Superseded, now, said.Refusal)
                    : book.Move(decision, SupervisorState.Stale, now, said.Refusal ?? SupervisorWhy.NotParked);
            }, ct).ConfigureAwait(false);
            if (state is { } went) moved.Add(went);
        }

        return moved;
    }

    /// <summary>
    /// The look's reading of <paramref name="session"/>'s record, before it plans (§6.3, §3.6): a held decision with an owner's word
    /// waiting is <c>superseded</c>; a released one whose word is gone is <c>superseded</c>, and one on a record no longer parked is
    /// <c>stale</c> with its word taken back.
    /// </summary>
    public async Task SettleAsync(
        string session, bool parked, bool ownerWaits, IReadOnlyCollection<string> waiting, SupervisorTakeBack takeBack, DateTimeOffset now,
        CancellationToken ct = default)
    {
        foreach (var id in book.LiveIds(session))
        {
            await book.UnderAsync(session, async () =>
            {
                if (!book.Live.TryGetValue(id, out var decision)) return 0;
                if (decision.State == SupervisorState.Held && ownerWaits) book.Move(decision, SupervisorState.Superseded, now, null);
                else if (decision.State == SupervisorState.Released && !waiting.Contains(decision.Word!)) book.Move(decision, SupervisorState.Superseded, now, null);
                else if (decision.State == SupervisorState.Released && !parked)
                {
                    await SupervisorBook.TakeBackWordAsync(session, decision, takeBack, ct).ConfigureAwait(false);
                    book.Move(decision, SupervisorState.Stale, now, SupervisorWhy.NotParked);
                }

                return 0;
            }, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The owner's <b>Overrule</b> (§6.3, <c>SUPERVISOR_OVERRULE</c>), under the decision's lock: while held it moves to <c>overruled</c>
    /// with nothing to take back; while released its word is taken back first. From <c>admitted</c> on it changes nothing and answers
    /// the state. Null where no decision of this session is live by that id.
    /// </summary>
    public Task<SupervisorState?> OverruleAsync(
        string session, string decision, SupervisorTakeBack takeBack, DateTimeOffset now, CancellationToken ct = default) =>
        book.UnderAsync(session, async () =>
        {
            if (!book.Live.TryGetValue(decision, out var live) || live.Session != session) return (SupervisorState?)null;
            if (live.State == SupervisorState.Released && !await SupervisorBook.TakeBackWordAsync(session, live, takeBack, ct).ConfigureAwait(false))
            {
                // The take-back refused: the record runs, so the word is the run's; nothing moved.
                return live.State;
            }

            return live.State is SupervisorState.Held or SupervisorState.Released
                ? book.Move(live, SupervisorState.Overruled, now, null)
                : live.State;
        }, ct);

    /// <summary>
    /// The owner's words named <paramref name="question"/> (§6.3, <c>SESSION_INPUT</c> with its question): every held or released
    /// decision on it moves to <c>superseded</c> at once, since the service's one owner-word operation already took a released word back.
    /// </summary>
    /// <returns>How many moved.</returns>
    public Task<int> SupersedeAsync(string session, string question, DateTimeOffset now, CancellationToken ct = default) =>
        book.UnderAsync(session, () => Task.FromResult(book.Live.Values
            .Where(each => each.Session == session && each.Question.Id == question
                           && each.State is SupervisorState.Held or SupervisorState.Released)
            .ToList()
            .Count(each => book.Move(each, SupervisorState.Superseded, now, null) == SupervisorState.Superseded)), ct);
}
