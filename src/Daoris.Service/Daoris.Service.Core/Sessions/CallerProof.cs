namespace Daoris.Knowledge;

/// <summary>Who made a call: proven the session's own agent, a native child of it, or neither readable.</summary>
public enum CallerKind
{
    /// <summary>Proven the session's own agent.</summary>
    Root,

    /// <summary>A thread or subagent the harness says it spawned.</summary>
    Child,

    /// <summary>Lineage missing, malformed, conflicting or unjoined: not proven a root, and not shown a child.</summary>
    Unreadable,
}

/// <summary>A caller, and what its lineage showed, in a phrase for a person.</summary>
public sealed record Caller(CallerKind Kind, string Reason);

/// <summary>
/// Tells a root's call from a child's by the lineage its harness wrote on it, failing closed (FABRIC-C2a, D173's FABRIC-C2a note,
/// point 1): only a caller proven a root is one, and missing, malformed, conflicting or unjoined lineage never is.
/// </summary>
/// <remarks>
/// <para><b>Codex.</b> A root's call names a thread that is its session's root (<c>thread_id</c> equal to <c>session_id</c>) and no
/// child signal: no parent, no spawn kind, a source of <c>user</c> or none. Codex's protocol door writes no source on a root's calls
/// (D173's 2026-10-31 note), so a missing one is not asked for; a spawned thread has its own id, so its calls never name the root's
/// thread as theirs. A source other than <c>user</c> or <c>subagent</c> is unreadable.</para>
///
/// <para><b>Claude Code</b> writes only the call's id, so a root's call is one whose id joins a hook line naming no agent
/// (<see cref="HookJoin"/>). The driver's <c>ChildBindings</c> fold reads the same lines the same way.</para>
/// </remarks>
public static class CallerProof
{
    /// <summary>The caller <paramref name="lineage"/> shows; <paramref name="join"/> reads the hook's line for a Claude call's id.</summary>
    public static async Task<Caller> OfAsync(CallLineage? lineage, Func<string, Task<HookJoinOutcome>> join)
    {
        if (lineage is null) return Unread("the call carried no lineage from its harness");
        if (lineage.Malformed is not null) return Unread($"its harness sent a lineage value that is not an id ({lineage.Malformed})");
        if (lineage.Harness != CallLineage.Claude) return Codex(lineage);
        if (lineage.ToolUse is not { } toolUse) return Unread("the call named no call id");

        return await join(toolUse).ConfigureAwait(false) switch
        {
            HookJoinOutcome.Root => new Caller(CallerKind.Root, "the call joins a hook line naming no agent"),
            HookJoinOutcome.Child => new Caller(CallerKind.Child, "the call joins a hook line naming a subagent"),
            HookJoinOutcome.Conflict => Unread("the hook's lines for this call disagree"),
            HookJoinOutcome.Unreadable => Unread("the hook could not read which agent made this call"),
            HookJoinOutcome.TimedOut => Unread("the hook's record of this call was not read in time"),
            _ => Unread("no hook line was found for this call"),
        };
    }

    private static Caller Codex(CallLineage lineage)
    {
        if (lineage.Parent is not null || lineage.Kind is not null || lineage.Source == "subagent")
        {
            return new Caller(CallerKind.Child, "its harness marks the call as a spawned thread's");
        }

        if (lineage.Thread is null) return Unread("the call named no thread");
        if (lineage.Root is null) return Unread("the call named no root thread");
        if (lineage.Thread != lineage.Root) return new Caller(CallerKind.Child, "the call's thread is not its session's root thread");
        if (lineage.Source is not (null or "user")) return Unread("the call's thread source is one this connector does not read");
        return new Caller(CallerKind.Root, "the call's thread is its session's root thread");
    }

    private static Caller Unread(string reason) => new(CallerKind.Unreadable, reason);
}
