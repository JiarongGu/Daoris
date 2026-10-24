namespace Daoris.Driver;

/// <summary>
/// The runner that holds a conversation, reserving it for the person's press on its work (CHATFLOW1d, the chat-flow design §5.2 point 1): the
/// shell's <see cref="ChatRunner"/>. A seam, so the press's operation is the same on every door and its tests stand in.
/// </summary>
public interface IChatPressing
{
    /// <summary>
    /// Reserve the conversation against another turn: <see cref="PressHold.Held"/> where nothing is taking, opening or waiting, and no turn
    /// starts until <see cref="Release"/>; <see cref="PressHold.NotHere"/> where no runner in this process holds it.
    /// </summary>
    Task<PressHold> HoldAsync(string sessionId, CancellationToken ct = default);

    /// <summary>Let the conversation go once the press answered, landed or refused; a word that waited for it starts at once.</summary>
    void Release(string sessionId);
}

/// <summary>What the person's press on a chat run's work asks the operation (CHATFLOW1d): the record pressed and its run.</summary>
/// <param name="Session">The record the door lands, the line's newest in its tree.</param>
/// <param name="Live">Whether that record is still a live conversation, which some runner holds; an ended one needs no reservation.</param>
public sealed record ChatPressAsk(string Session, string Tree, string Run, string Repository, string Workspace, bool Live)
{
    /// <summary>
    /// <i>Accept without the look…</i> (CHATFLOW1d2, the chat-flow design §5.3): the person's way past a look no set-up step can show, kept with
    /// their words at the tip this press captures; null for a plain <i>Accept…</i>.
    /// </summary>
    public ChatLookPress? WithoutLook { get; init; }
}

/// <summary>The person's way past the look as their press says it (CHATFLOW1d2): their words, and the door they pressed it at.</summary>
public sealed record ChatLookPress(string? Words, string Door);

/// <summary>What the press came to: the landing, or its refusal, and the gate it was judged by at the captured tip; and the tip.</summary>
public sealed record ChatPressed(TreeLanding Landed, LandingGate? Gate, string? Tip)
{
    /// <summary>
    /// What refused it, where the page words it in the reader's language (CHATFLOW1d): <c>busy</c>, <c>elsewhere</c>, <c>moved</c> or <c>asked</c>;
    /// null where the landing's own sentence says it.
    /// </summary>
    public string? Code { get; init; }

    /// <summary>Whether this press asked the run's second opinion (§5.2 point 3).</summary>
    public bool Asked { get; init; }
}

/// <summary>
/// The person's press on a chat run's work (CHATFLOW1d, the chat-flow design §5.2, Codex's findings 1 and 11, the Opus check's finding 5): one
/// driver operation the bridge's <c>LAND_SESSION_TREE</c>, the connected terminal and the standalone terminal each call. It reserves the
/// conversation against another turn, captures the tree's <c>HEAD</c> once, reads every part of the gate at that tip, asks the second opinion
/// where its part holds as <c>not-asked</c>, and lands that tip by the pinned path (<see cref="LandingGate.Tip"/>), refusing a moved candidate.
/// </summary>
/// <remarks>
/// <para><b>The reservation</b> is the runner's <c>_pressing</c>: a word the person says meanwhile is kept and waits, and starts once the press
/// answers. It is held through the landing hold too, since the landing reads the tree itself, and let go however the press ends. A live
/// conversation no runner here holds is held by another window, and the press is refused there, never landed beside a turn it cannot see.</para>
///
/// <para><b>Only the press asks</b> (§5.2 point 5): the plan reads the gate and writes nothing. A press while the pass is asked, reading,
/// with the conversation or read again asks nothing and is refused with that state's words (point 4). No press is kept: the person presses
/// again once it settles, and the work lands at that press (point 7). <i>Accept automatically</i> never lands a chat's work.</para>
/// </remarks>
public static class ChatPresses
{
    /// <summary>The codes a refusal of the press carries for the page (<see cref="ChatPressed.Code"/>).</summary>
    public static class Codes
    {
        public const string Busy = "busy";
        public const string Elsewhere = "elsewhere";
        public const string Moved = "moved";
        public const string Asked = "asked";
    }

    /// <summary>A turn runs or waits: the press waits for its end, never lands beside it.</summary>
    public const string Busy = "a turn is running in this conversation; press Accept once it ends.";

    /// <summary>A live conversation another process holds: its window is the door.</summary>
    public const string Elsewhere = "the conversation is held by another window; press Accept there.";

    /// <summary>A commit made after the judgement: nothing lands under the judgement of an earlier tip.</summary>
    public const string Moved = "the work moved while it was judged; press Accept again.";

    /// <summary>
    /// The press (§5.2 points 1 to 7): reserved, captured, judged at the tip, the opinion asked where it is not, and landed pinned.
    /// </summary>
    /// <param name="holder">The runner that may hold the conversation in this process; null where none does (a terminal alone).</param>
    /// <param name="gate">The whole gate read at a tip, keyed by the run, every part in its fixed order, and a press's answer to what it showed.</param>
    /// <param name="land">The landing, pinned to the gate's tip.</param>
    /// <param name="turns">The person's turn count, read once the conversation is reserved (§6.1 (b)).</param>
    /// <param name="head">The tree's <c>HEAD</c>, read once to capture and again where the landing was refused; git's where null.</param>
    public static async Task<ChatPressed> PressAsync(
        string home, ChatPressAsk ask, IChatPressing? holder, Func<string, CancellationToken, Task<LandingGate>> gate,
        Func<LandingGate, CancellationToken, Task<TreeLanding>> land, Func<int> turns, CancellationToken ct = default,
        Func<CancellationToken, Task<string?>>? head = null)
    {
        var hold = holder is null ? PressHold.NotHere : await holder.HoldAsync(ask.Session, ct).ConfigureAwait(false);
        if (hold is PressHold.Busy or PressHold.Ending) return Refused(Busy) with { Code = Codes.Busy };
        if (hold == PressHold.NotHere && ask.Live) return Refused(Elsewhere) with { Code = Codes.Elsewhere };

        try
        {
            head ??= token => ReviewGit.HeadAsync(ask.Tree, token);
            if (await head(ct).ConfigureAwait(false) is not { } tip)
            {
                return Refused("git could not read the commit that would land, so nothing was judged or landed.");
            }

            var judged = await gate(tip, ct).ConfigureAwait(false) with { Tip = tip };
            // CHATFLOW1d2 (§5.3): the person's way past a look no set-up step can show is kept at the tip captured, and the gate read again
            // there; the landing is pinned to that tip, so a tree that moved past it is refused, never landed under the skip.
            if (ask.WithoutLook is { } without)
            {
                if (judged.Review is not { State: ReviewStates.CannotStart, Decision.ChatRun: not null })
                {
                    return new(Refusal($"no look waits for this work, so there is nothing to accept it without: {judged.Review.Says}", AutoLandingCode.Refused),
                        judged, tip);
                }

                new ChatLooks(home).Skip(ask.Run, new ChatLookSkip(tip, DateTimeOffset.UtcNow, without.Words, without.Door));
                judged = await gate(tip, ct).ConfigureAwait(false) with { Tip = tip };
            }

            // The opinion's part holds for want of a pass, every part before it letting go: the press asks it, at the landing occasion, with the
            // tip it captured and the person's turn count, and is refused saying so. One a press asked already, the look not started yet, asks
            // nothing again.
            if (judged.Workflow is not { LetsGo: false } && judged.Opinion is { State: OpinionGateStates.NotAsked, Run: not null } opinion)
            {
                if (opinion.Asked) return new(Refusal(opinion.Says, AutoLandingCode.Opinion), judged, tip) { Code = Codes.Asked };
                new OpinionDues(home).Due(new OpinionDue(ask.Session, "", ask.Repository, ask.Workspace, ask.Tree, OpinionRules.Landing, DateTimeOffset.UtcNow)
                {
                    Run = ask.Run, Tip = tip, Turns = turns(),
                });
                return new(Refusal(OpinionGate.AskedAtPress(opinion), AutoLandingCode.Opinion), judged, tip) { Asked = true, Code = Codes.Asked };
            }

            var landed = await land(judged, ct).ConfigureAwait(false);
            // The pinned path refused a tree that moved past the tip judged: said as the press says it, so the person presses again.
            if (!landed.Landed && landed.Refusal == AutoLandingCode.Refused
                && await head(ct).ConfigureAwait(false) is { } now && !string.Equals(now, tip, StringComparison.OrdinalIgnoreCase))
            {
                landed = landed with { Message = Moved };
            }

            return new(landed, judged, tip) { Code = landed.Message == Moved ? Codes.Moved : null };
        }
        finally
        {
            if (hold == PressHold.Held) holder!.Release(ask.Session);
        }

        static ChatPressed Refused(string why) => new(Refusal(why, AutoLandingCode.Refused), null, null);
    }

    private static TreeLanding Refusal(string message, string code) => new(false, message) { Refusal = code };

    /// <summary>
    /// The person's turn count over a line (§6.1 (b), the Opus check's finding 4): their own words in the line's conversation history, each
    /// kept word once by its id, plus <paramref name="waiting"/>, the words waiting or being kept. It only rises: a word leaves the queue for
    /// the history once its turn is on the wire.
    /// </summary>
    public static int TurnsOf(SessionEvents events, IEnumerable<string> line, int waiting = 0)
    {
        var count = waiting;
        foreach (var record in line)
        {
            if (!File.Exists(events.PathOf(record))) continue;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var said in events.After(record, 0).Events)
            {
                if (said.Kind != SessionEventKind.User || said.Origin != "person" || said.From is not null) continue;
                if (said.Id is null || seen.Add(said.Id)) count++;
            }
        }

        return count;
    }
}
