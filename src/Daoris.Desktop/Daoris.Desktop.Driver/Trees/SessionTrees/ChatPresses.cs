namespace Daoris.Driver;

/// <summary>Where a chat run's press found its conversation's turns (CHATFLOW1d, the chat-flow design §5.2 point 1).</summary>
public enum ChatHold
{
    /// <summary>No runner of this process holds the conversation, so no turn of it can start here: nothing is reserved.</summary>
    None,

    /// <summary>Reserved for the press (<c>_pressing</c>): a word is kept and waits until the press answers.</summary>
    Reserved,

    /// <summary>A turn is taking, opening or waiting: the press is refused until it ends.</summary>
    Running,

    /// <summary>Another process holds the conversation and this door cannot reach it: the press is refused there.</summary>
    Elsewhere,
}

/// <summary>
/// What a chat run's press asks of the conversation it lands (CHATFLOW1d): its turns reserved and let go, and the person's turn count,
/// read under the reservation. Each door wires its own: the bridge its shell's chat runner, a terminal what it can reach.
/// </summary>
/// <param name="Reserve">Reserve the turns of a record for the press, or say why not.</param>
/// <param name="Release">Let the reservation go, a word that waited under it starting at once.</param>
/// <param name="Turns">The person's turn count over the line's records (§6.1 (b)), read under the reservation.</param>
public sealed record ChatPressDoor(
    Func<string, CancellationToken, Task<ChatHold>> Reserve, Action<string> Release, Func<string, CancellationToken, Task<int>> Turns)
{
    /// <summary>A door that reaches no runner: nothing is reserved, and the count is read from the line's records alone.</summary>
    public static ChatPressDoor Unheld(Func<string, CancellationToken, Task<int>> turns) =>
        new((_, _) => Task.FromResult(ChatHold.None), _ => { }, turns);
}

/// <summary>Why a chat run's press answered as it did (CHATFLOW1d), a code the page words in the person's language.</summary>
public static class ChatPressCodes
{
    /// <summary>A turn is running: press Accept once it ends.</summary>
    public const string TurnRunning = "turn-running";

    /// <summary>Another window holds the conversation: press Accept there.</summary>
    public const string HeldElsewhere = "held-elsewhere";

    /// <summary>The press asked the second opinion: press Accept again once it settles.</summary>
    public const string OpinionAsked = "opinion-asked";

    /// <summary>The work moved while it was judged: press Accept again.</summary>
    public const string Moved = "moved";

    /// <summary>Git could not read the tree's tip, so nothing was judged.</summary>
    public const string Unread = "unread";

    /// <summary><i>Accept without the look…</i> came with no words of the person's.</summary>
    public const string NoWords = "no-words";
}

/// <summary>What a chat run's press came to: the landing or its refusal, the gate it was judged by, and its code where the page words one.</summary>
public sealed record ChatPressed(TreeLanding Landed, LandingGate? Gate)
{
    /// <summary>One of <see cref="ChatPressCodes"/>, where the press itself refused or asked; null where the gate or the landing answered.</summary>
    public string? Code { get; init; }

    /// <summary>The reviewer the press asked, where it asked one.</summary>
    public string? Reviewer { get; init; }
}

/// <summary>
/// The person's Accept on a chat run (CHATFLOW1d, the chat-flow design §5.2; CHATFLOW1d2, §5.3): one driver operation every door calls,
/// the bridge's <c>LAND_SESSION_TREE</c> and the terminal's <c>trees land</c>.
/// </summary>
/// <remarks>
/// <para><b>Reserved, then judged at one tip.</b> The conversation's turns are reserved against another turn (<see cref="ChatHold.Reserved"/>),
/// refused where one runs; the tree's <c>HEAD</c> is captured once; every part of the gate is judged at that tip, keyed by the run; and the
/// landing is pinned to it (<see cref="LandingGate.Tip"/>), so a commit made after the judgement is refused, never landed unseen. The
/// reservation is held through the landing hold and let go when the press answers, landed or refused.</para>
///
/// <para><b>It asks the opinion, once.</b> Where the opinion's part holds for want of a pass, the press writes the due at the
/// <c>landing</c> occasion with its tip and the person's turn count, and is refused saying so; the driver's next look starts the pass.
/// A press while a pass reads, its findings are with the session, or its recheck reads, is refused with that state's words and asks
/// nothing. Nothing is kept to land later: the person presses again.</para>
///
/// <para><b>The person's way past the look</b> (§5.3): <i>Accept without the look…</i> keeps their words at the captured tip on the run's
/// look file before the gate is read, so the skip covers exactly the tip the landing is pinned to.</para>
/// </remarks>
public static class ChatPresses
{
    /// <summary>The refusal while a turn runs (§5.2 point 1).</summary>
    public const string TurnRunningSays = "a turn is running; press Accept once it ends.";

    /// <summary>The refusal where another process holds the conversation (§5.2 point 1).</summary>
    public const string HeldElsewhereSays = "the conversation is held by another window; press Accept there.";

    /// <summary>The refusal of a candidate that moved under its judgement (§5.2 point 1).</summary>
    public const string MovedSays = "the work moved while it was judged; press Accept again.";

    /// <summary>
    /// Press Accept on a chat run's work: reserve, capture, judge, ask or land. <paramref name="land"/> is the door's own landing (its
    /// subject, its plugins, its tidy), handed the pinned gate.
    /// </summary>
    /// <param name="withoutLook">The person's words for <i>Accept without the look…</i>; null for a plain Accept.</param>
    /// <param name="answers">The gate's token the page drew beside Accept, which answers a dispute or commits unread where it still stands.</param>
    /// <param name="door"><c>screen</c> or <c>terminal</c> (<see cref="ReviewDoors"/>), kept with the person's answers.</param>
    public static async Task<ChatPressed> PressAsync(
        SessionTrees trees, string session, string run, string tree, IOpinionWorld world, ChatPressDoor reach,
        Func<LandingGate, CancellationToken, Task<TreeLanding>> land, string? withoutLook = null, string? answers = null,
        string door = ReviewDoors.Screen, CancellationToken ct = default, Func<DateTimeOffset>? now = null)
    {
        var clock = now ?? (() => DateTimeOffset.UtcNow);
        if (withoutLook is not null && withoutLook.Trim().Length == 0)
        {
            return Refused(ChatPressCodes.NoWords, "Accept without the look takes your words: say why it lands without one.");
        }

        var hold = await reach.Reserve(session, ct).ConfigureAwait(false);
        if (hold == ChatHold.Running) return Refused(ChatPressCodes.TurnRunning, TurnRunningSays);
        if (hold == ChatHold.Elsewhere) return Refused(ChatPressCodes.HeldElsewhere, HeldElsewhereSays);
        try
        {
            if (await ReviewGit.HeadAsync(tree, ct).ConfigureAwait(false) is not { } tip)
            {
                return Refused(ChatPressCodes.Unread, "git could not read the tree's tip, so nothing was judged or landed.");
            }

            var home = trees.Home;
            var (workspace, repository) = trees.Owner(tree);
            if (withoutLook is not null)
            {
                new ChatLooks(home).Skip(run, new ChatLookSkip(tip, withoutLook.Trim(), clock()) { Session = session, Door = door });
            }

            var gate = await trees.GateAsync(tree, null, world, session, ct, tip: tip, run: run).ConfigureAwait(false);
            // The press answers what it showed beside Accept (the second-agent design §8.2–§8.3): a dispute or commits nobody read, kept by
            // the run's key at the tip it was judged at.
            if (gate.Answers(answers))
            {
                new OpinionGates(home).Said(run, repository, new OpinionPersonWord(OpinionPersonSaid.Press, tip, clock()) { Door = door });
                gate = await trees.GateAsync(tree, null, world, session, ct, tip: tip, run: run).ConfigureAwait(false);
            }

            gate = gate with { Tip = tip };
            if (gate.Refusal is { Refusal: AutoLandingCode.Opinion } && gate.Opinion.State == OpinionGateStates.NotAsked)
            {
                var turns = await reach.Turns(session, ct).ConfigureAwait(false);
                new OpinionDues(home).Due(new OpinionDue(session, null, repository, workspace, Path.GetFullPath(tree), OpinionRules.Landing, clock())
                {
                    Run = run, Tip = tip, Turns = turns,
                });
                return new ChatPressed(new TreeLanding(false, OpinionGate.Asked(gate.Opinion)) { Refusal = AutoLandingCode.Opinion }, gate)
                {
                    Code = ChatPressCodes.OpinionAsked,
                    Reviewer = gate.Opinion.Rule?.Rule.Reviewers is { Count: > 0 } reviewers ? reviewers[0] : null,
                };
            }

            var landed = await land(gate, ct).ConfigureAwait(false);
            if (!landed.Landed && gate.LetsGo && await ReviewGit.HeadAsync(tree, ct).ConfigureAwait(false) is { } now2
                && !string.Equals(now2, tip, StringComparison.OrdinalIgnoreCase))
            {
                return new ChatPressed(landed with { Message = MovedSays }, gate) { Code = ChatPressCodes.Moved };
            }

            return new ChatPressed(landed, gate);
        }
        finally
        {
            if (hold == ChatHold.Reserved) reach.Release(session);
        }
    }

    /// <summary>
    /// The person's turn count over a chat run's line (§6.1 (b)): their own words each record of the line took, plus those waiting.
    /// It only rises.
    /// </summary>
    public static int TurnsOf(SessionEvents events, IEnumerable<string> line, int waiting = 0) =>
        line.Distinct(StringComparer.OrdinalIgnoreCase).Sum(events.PersonSaid) + waiting;

    private static ChatPressed Refused(string code, string says) =>
        new(new TreeLanding(false, says) { Refusal = AutoLandingCode.Refused }, null) { Code = code };
}
