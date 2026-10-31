using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// FABRIC-C2a (D173's FABRIC-C2a note, points 1 and 2; its 2026-10-31 note): who made a call, by the lineage its harness wrote,
/// failing closed. A Codex call is a root's where its thread is its session's root and nothing says a child; a Claude Code call
/// where its id joins a hook line naming no agent. Anything else is a child, or unreadable, and the second never claims the first.
/// </summary>
/// <remarks>The Codex shapes are the install's: the protocol door's root (thread equal to root, no source), the CLI's (source
/// <c>user</c>), and the C0 audit's spawned thread.</remarks>
public sealed class CallerProofTests
{
    private static CallLineage CodexCall(string thread, string? root = "t-root", string? parent = null, string? kind = null, string? source = null) =>
        new(CallLineage.Codex) { Thread = thread, Root = root, Parent = parent, Kind = kind, Source = source };

    private static Task<Caller> Of(CallLineage? lineage, HookJoinOutcome joined = HookJoinOutcome.NotFound) =>
        CallerProof.OfAsync(lineage, _ => Task.FromResult(joined));

    /// <summary>
    /// The Codex reading (D173's 2026-10-31 note): codex-acp's 128 calls each carried `thread` equal to `root` and no source, so
    /// a call whose thread is its session's root reads as the root, as the CLI's with source <c>user</c> does.
    /// </summary>
    [Fact]
    public async Task A_codex_call_whose_thread_is_its_root_is_the_root_s_with_or_without_a_user_source()
    {
        Assert.Equal(CallerKind.Root, (await Of(CodexCall("t-root"))).Kind);
        Assert.Equal(CallerKind.Root, (await Of(CodexCall("t-root", source: "user"))).Kind);
    }

    /// <summary>A Codex child's call carries a thread unequal to its root, or a parent, a spawn kind or a subagent source.</summary>
    [Fact]
    public async Task A_codex_call_with_any_child_signal_is_a_child_s()
    {
        CallLineage[] children =
        [
            CodexCall("t-a"),
            CodexCall("t-a", source: "user"),
            CodexCall("t-a", parent: "t-root", kind: "thread_spawn", source: "subagent"),
            CodexCall("t-root", parent: "t-root"),
            CodexCall("t-root", kind: "thread_spawn"),
            CodexCall("t-root", source: "subagent"),
        ];

        foreach (var lineage in children) Assert.Equal(CallerKind.Child, (await Of(lineage)).Kind);
    }

    [Fact]
    public async Task Missing_malformed_or_unknown_codex_lineage_is_unreadable_and_never_said_to_be_a_subagent_s()
    {
        CallLineage?[] unread =
        [
            null,
            new CallLineage(CallLineage.Codex) { Malformed = "turn" },
            CodexCall("t-root") with { Malformed = "parent" },
            CodexCall("t-root", root: null),
            new CallLineage(CallLineage.Codex) { Root = "t-root" },
            CodexCall("t-root", source: "exec"),
        ];

        foreach (var lineage in unread)
        {
            var caller = await Of(lineage);
            Assert.Equal(CallerKind.Unreadable, caller.Kind);
            Assert.DoesNotContain("subagent", caller.Reason, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task A_claude_call_is_the_root_s_only_where_its_id_joins_a_hook_line_naming_no_agent()
    {
        var call = new CallLineage(CallLineage.Claude) { ToolUse = "toolu_1" };

        Assert.Equal(CallerKind.Root, (await Of(call, HookJoinOutcome.Root)).Kind);
        Assert.Equal(CallerKind.Child, (await Of(call, HookJoinOutcome.Child)).Kind);
        foreach (var outcome in new[] { HookJoinOutcome.NotFound, HookJoinOutcome.Conflict, HookJoinOutcome.Unreadable, HookJoinOutcome.TimedOut })
        {
            Assert.Equal(CallerKind.Unreadable, (await Of(call, outcome)).Kind);
        }

        // An id that could not be read is never joined.
        var looked = 0;
        var malformed = await CallerProof.OfAsync(
            new CallLineage(CallLineage.Claude) { Malformed = "toolUse" },
            _ => { looked++; return Task.FromResult(HookJoinOutcome.Root); });
        Assert.Equal((CallerKind.Unreadable, 0), (malformed.Kind, looked));
    }
}
