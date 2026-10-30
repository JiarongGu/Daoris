using System.Text;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// FABRIC-C2a (D173's FABRIC-C2a note, point 5): a Claude Code call's id joined to the hook line naming the agent behind it, read
/// from the session's lineage file's end, bounded in bytes and time, cancellable, refusing on a conflict.
/// </summary>
public sealed class HookJoinTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "daoris-hook-join-" + Guid.NewGuid().ToString("N")[..8], "lineage.jsonl");

    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(50);

    public HookJoinTests() => Directory.CreateDirectory(Path.GetDirectoryName(_file)!);

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(_file)!, recursive: true); } catch (IOException) { }
    }

    private static string Hook(string toolUse, string? agent, bool unreadable = false) =>
        "{\"by\":\"hook\",\"event\":\"call\"" + (agent is null ? "" : $",\"agent\":\"{agent}\"")
        + (unreadable ? ",\"agentUnreadable\":true" : "") + $",\"toolUse\":\"{toolUse}\",\"session\":\"c1\"}}";

    private static string Call(string toolUse) => $"{{\"by\":\"call\",\"tool\":\"helper_ask\",\"harness\":\"claude-code\",\"toolUse\":\"{toolUse}\"}}";

    private void Write(params string[] lines) => File.AppendAllText(_file, string.Concat(lines.Select(line => line + "\n")), new UTF8Encoding(false));

    private Task<HookJoinOutcome> Find(string toolUse, TimeSpan? budget = null, int maxBytes = HookJoin.MaxBytes) =>
        HookJoin.FindAsync(_file, toolUse, budget ?? Short, CancellationToken.None, maxBytes);

    [Fact]
    public async Task The_hook_line_says_root_or_child_and_the_connector_s_own_line_is_no_join()
    {
        Write(Hook("toolu_r", null), Call("toolu_r"), Hook("toolu_c", "a6aa3d41bcf080f4e"), Call("toolu_c"), Call("toolu_alone"));

        Assert.Equal(HookJoinOutcome.Root, await Find("toolu_r"));
        Assert.Equal(HookJoinOutcome.Child, await Find("toolu_c"));
        Assert.Equal(HookJoinOutcome.NotFound, await Find("toolu_alone"));
        // The exact id: one that only begins another is not it.
        Assert.Equal(HookJoinOutcome.NotFound, await Find("toolu"));
    }

    [Fact]
    public async Task Hook_lines_that_disagree_are_a_conflict_and_an_unreadable_agent_is_unreadable()
    {
        Write(Hook("toolu_x", null), Hook("toolu_x", "a6aa3d41bcf080f4e"), Hook("toolu_u", null, unreadable: true));

        Assert.Equal(HookJoinOutcome.Conflict, await Find("toolu_x"));
        Assert.Equal(HookJoinOutcome.Unreadable, await Find("toolu_u"));
    }

    [Fact]
    public async Task A_hook_line_written_while_the_call_waits_is_joined_within_the_budget()
    {
        Write(Call("toolu_late"));
        var finding = Find("toolu_late", TimeSpan.FromSeconds(10));
        await Task.Delay(100);
        Write(Hook("toolu_late", null));

        Assert.Equal(HookJoinOutcome.Root, await finding);
    }

    [Fact]
    public async Task A_cancelled_wait_is_timed_out_and_a_file_not_there_is_not_found()
    {
        using var cancel = new CancellationTokenSource();
        var finding = HookJoin.FindAsync(_file, "toolu_never", TimeSpan.FromSeconds(10), cancel.Token);
        cancel.Cancel();

        Assert.Equal(HookJoinOutcome.TimedOut, await finding);
        Assert.Equal(HookJoinOutcome.NotFound, await HookJoin.FindAsync(_file + ".none", "toolu_r", Short, CancellationToken.None));
        Assert.Equal(HookJoinOutcome.NotFound, await HookJoin.FindAsync(null, "toolu_r", Short, CancellationToken.None));
    }

    [Fact]
    public async Task Only_the_file_s_end_is_read()
    {
        Write(Hook("toolu_old", null));
        Write([.. Enumerable.Range(0, 50).Select(i => Call($"toolu_{i}"))]);

        Assert.Equal(HookJoinOutcome.NotFound, await Find("toolu_old", maxBytes: 1024));
        Assert.Equal(HookJoinOutcome.Root, await Find("toolu_old"));
    }
}
