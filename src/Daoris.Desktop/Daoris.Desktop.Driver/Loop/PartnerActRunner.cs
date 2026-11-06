using System.ComponentModel;

namespace Daoris.Driver;

/// <summary>
/// A piece's later acts, done by the driver's look (COAGENT1b2, D199; the co-agent design §2.3 step 10, §2.4, §6.1): the person's
/// <i>Discard…</i> and <i>Hand over what it left</i> taken from the act's file, and whether a handed piece was taken in. The look is the one
/// writer of a piece's state; each side effect is made before the state that names it, so a look that is cut redoes the act from what stands.
/// </summary>
/// <param name="keep">Keep and import a piece whose state is <see cref="PartnerStates.Ending"/>, then answer and hand it.</param>
internal sealed class PartnerActRunner(
    LoopContext context, Func<string, CancellationToken, Task<string?>> rootOf, Func<PartnerAsk, CancellationToken, Task<LookLine>> keep)
{
    private static LookLine Line(LookClass @class, string line) => new(line, new LookClassed(@class, "partner"));

    /// <summary>Whether an act waits on the piece, for the look's drain hold.</summary>
    public bool Waits(string piece) => new PartnerActFiles(context.Home).Waits(piece);

    /// <summary>The act on file, done or refused; null where none waits.</summary>
    public async Task<LookLine?> RunAsync(PartnerAsk ask, PartnerState? state, PartnerFiles files, CancellationToken ct)
    {
        var acts = new PartnerActFiles(context.Home);
        if (!acts.Waits(ask.Id)) return null;
        if (acts.Read(ask.Id) is not { } act || state is null || !PartnerPersonActs.Allows(act.Act, state.State))
        {
            acts.Clear(ask.Id);
            return Line(LookClass.Progress, $"partner  the act on piece `{ask.Id}` no longer fits where it stands, and is dropped.");
        }

        var line = act.Act == PartnerPersonActs.Discard
            ? await DiscardAsync(ask, state, files, ct).ConfigureAwait(false)
            : await HandOverAsync(ask, state, files, ct).ConfigureAwait(false);
        if (line.Look is not { Class: LookClass.Retry }) acts.Clear(ask.Id);
        return line;
    }

    /// <summary>
    /// <i>Discard…</i>: the partner's job ended where a restart left it unknown, the ref removed at the commit the piece froze (one
    /// <c>update-ref -d</c> naming it, which git refuses if the ref moved), the copy removed, and only then <see cref="PartnerStates.Discarded"/>.
    /// </summary>
    private async Task<LookLine> DiscardAsync(PartnerAsk ask, PartnerState state, PartnerFiles files, CancellationToken ct)
    {
        if (await EndJobAsync(ask, state).ConfigureAwait(false) is { } cannot) return cannot;

        var reference = PieceRefs.Of(ask.Id);
        var lead = await context.Service.RecordAsync(ask.Asker, ct).ConfigureAwait(false);
        var root = lead?.Tree is { Length: > 0 } tree ? await rootOf(tree, ct).ConfigureAwait(false) : null;
        if (root is null)
        {
            return Line(LookClass.Retry, $"partner  piece `{ask.Id}` cannot be discarded yet: its repository's checkout could not be read. It is tried again at the next look.");
        }

        var (readCode, read, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "-q", reference], ct).ConfigureAwait(false);
        if (readCode == 0)
        {
            var standing = read.Trim();
            if (state.Commit is { Length: > 0 } frozen && !string.Equals(frozen, standing, StringComparison.OrdinalIgnoreCase))
            {
                return Line(LookClass.Progress, $"partner  piece `{ask.Id}`'s ref stands at {TextBounds.ShortCommit(standing, 7)}, not the commit it froze: it is kept, and nothing was discarded.");
            }

            var (deleted, _, error) = await WorkingTree.GitAsync(root, ["update-ref", "-d", reference, standing], ct).ConfigureAwait(false);
            if (deleted != 0)
            {
                return Line(LookClass.Retry, $"partner  piece `{ask.Id}`'s ref could not be removed, and is tried again at the next look: {error.Trim()}");
            }
        }

        if (state.Copy is { Length: > 0 } copy && Directory.Exists(copy)
            && await OpinionTree.RemoveAsync(context.Home, copy, PartnerPass.Prefix).ConfigureAwait(false) is { } left)
        {
            return Line(LookClass.Retry, $"partner  piece `{ask.Id}`'s copy could not be removed, and is tried again at the next look: {left}");
        }

        files.WriteState(state with { State = PartnerStates.Discarded, Code = null, CopyRemoved = true, At = DateTimeOffset.UtcNow });
        return Line(LookClass.Progress, $"partner  piece `{ask.Id}` is discarded: its ref and its copy are removed.");
    }

    /// <summary>
    /// <i>Hand over what it left</i>: an uncertain piece's job ended and waited empty, the keeper's moment written with the end
    /// <see cref="PartnerEnds.Stopped"/>, and the work kept, imported and handed as any other end's is.
    /// </summary>
    private async Task<LookLine> HandOverAsync(PartnerAsk ask, PartnerState state, PartnerFiles files, CancellationToken ct)
    {
        if (state.Copy is not { Length: > 0 } copy || !Directory.Exists(copy))
        {
            return Line(LookClass.Progress, $"partner  piece `{ask.Id}` has no copy left to hand over.");
        }

        if (await EndJobAsync(ask, state).ConfigureAwait(false) is { } cannot) return cannot;
        var now = DateTimeOffset.UtcNow;
        files.WriteState(state with { State = PartnerStates.Ending, Code = null, End = PartnerEnds.Stopped, Kept = now, At = now });
        return await keep(ask, ct).ConfigureAwait(false);
    }

    /// <summary>Whether the partner's job is gone, ending what a restart left running; a line where it could not be told.</summary>
    private async Task<LookLine?> EndJobAsync(PartnerAsk ask, PartnerState state)
    {
        if (state is not { State: PartnerStates.Uncertain, Job: { Length: > 0 } job }) return null;
        try
        {
            await ProcessJob.EndNamedAsync(job).ConfigureAwait(false);
            return null;
        }
        catch (Win32Exception error)
        {
            return Line(LookClass.Retry, $"partner  piece `{ask.Id}`'s job could not be ended, and the act is tried again at the next look: {error.Message}");
        }
    }

    /// <summary>
    /// Whether a handed piece was taken in (the design §2.3 step 10): its frozen commit read as an ancestor of the lead's branch by
    /// <c>merge-base --is-ancestor</c>, recorded with the lead's commit and never unset. A lead whose record ended <c>completed</c>
    /// and whose tree's branch does not hold the commit leaves the piece <see cref="PartnerStates.NotTakenIn"/>. Null where nothing moved.
    /// </summary>
    public async Task<LookLine?> TakenInAsync(PartnerAsk ask, PartnerState? state, PartnerFiles files, CancellationToken ct)
    {
        if (state is not { Commit: { Length: > 0 } commit } || !PartnerTakenIn.Reads(state.State)) return null;
        var lead = await context.Service.RecordAsync(ask.Asker, ct).ConfigureAwait(false);
        if (lead?.Tree is not { Length: > 0 } tree || !Directory.Exists(tree)) return null;

        var (headCode, head, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--verify", "-q", "HEAD"], ct).ConfigureAwait(false);
        var tip = headCode == 0 ? head.Trim() : null;
        var ancestor = false;
        if (tip is not null)
        {
            var (code, _, _) = await WorkingTree.GitAsync(tree, ["merge-base", "--is-ancestor", commit, tip], ct).ConfigureAwait(false);
            ancestor = code == 0;
        }

        var landed = string.Equals(lead.State, "completed", StringComparison.Ordinal);
        if (PartnerTakenIn.Next(state, tip, ancestor, landed, DateTimeOffset.UtcNow) is not { } next) return null;
        files.WriteState(next);
        return Line(LookClass.Progress, next.State == PartnerStates.TakenIn
            ? $"partner  piece `{ask.Id}` is taken in by session {ask.Asker}'s branch at {TextBounds.ShortCommit(next.TakenBy ?? "", 7)}."
            : $"partner  piece `{ask.Id}` was not taken in when session {ask.Asker}'s work landed: its ref stays until you discard it.");
    }
}
