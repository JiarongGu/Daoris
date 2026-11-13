using Daoris.Driver;
using Daoris.Driver.Tests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A harness that names its compactions is read from its event, never from its counter: Claude Code's adapter reports one as a tool call
/// whose <c>_meta.claudeCode.toolName</c> is <c>compact</c>, and its terminal status says it done; a door that names none keeps the counter.
/// The native door's <c>system</c> frame of subtype <c>compact_boundary</c> is the same fact on the other wire. Stand-in streams, no process.
/// </summary>
public sealed class CompactionEventTests
{
    private static string Update(string update) =>
        """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":""" + update + "}}";

    private static string Usage(long used) =>
        Update("""{"sessionUpdate":"usage_update","used":""" + used + ""","size":1000000}""");

    private const string Started =
        """{"sessionUpdate":"tool_call","toolCallId":"c1","title":"Compact conversation","kind":"think","status":"in_progress","_meta":{"claudeCode":{"toolName":"compact"}}}""";

    private const string Finished =
        """{"sessionUpdate":"tool_call_update","toolCallId":"c1","status":"completed","rawOutput":{"trigger":"automatic","preTokens":167000},"_meta":{"claudeCode":{"toolName":"compact"}}}""";

    private const string Failed =
        """{"sessionUpdate":"tool_call_update","toolCallId":"c2","status":"failed","_meta":{"claudeCode":{"toolName":"compact"}}}""";

    private const string OtherTool =
        """{"sessionUpdate":"tool_call_update","toolCallId":"t9","status":"completed","_meta":{"claudeCode":{"toolName":"Bash"}}}""";

    private static async Task<(int Told, AcpUsage? Usage)> RunAsync(bool named, params string[] frames)
    {
        var agent = AcpTransientTests.Scripted((n, frame, self) =>
        {
            foreach (var each in frames) self.Push(each);
            return AcpTransientTests.Answer(frame, "end_turn");
        });
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }) { CompactionNamed = named };
        var told = 0;
        session.Compacted += () => Interlocked.Increment(ref told);
        await session.RunAsync("D:/fam/Game", "serve the report", CancellationToken.None).WaitAsync(Poll.Patience);
        return (told, session.Usage);
    }

    [Fact]
    public async Task A_harness_that_names_its_compactions_is_told_once_when_the_compaction_completes()
    {
        var (told, _) = await RunAsync(true, Update(Started), Update(Finished), Update(Finished));

        Assert.Equal(1, told);
    }

    [Fact]
    public async Task A_compaction_that_failed_and_another_tool_that_completed_are_none()
    {
        var (told, _) = await RunAsync(true, Update(Failed), Update(OtherTool));

        Assert.Equal(0, told);
    }

    [Fact]
    public async Task A_harness_that_names_its_compactions_ignores_its_counter_falling()
    {
        var (told, usage) = await RunAsync(true, Usage(180_000), Usage(22_000), Update(Finished));

        Assert.Equal(1, told);
        Assert.Equal(180_000, usage!.Used);
    }

    [Fact]
    public async Task A_door_that_names_none_keeps_the_counter_and_takes_no_event_for_one()
    {
        var (told, _) = await RunAsync(false, Update(Finished), Usage(180_000), Usage(22_000));

        Assert.Equal(1, told);
    }

    [Fact]
    public void The_native_boundary_frame_is_a_compaction_with_its_trigger_and_size_and_nothing_else_is()
    {
        var boundary = NativeCompaction.Read(NativeFrame.Of(
            """{"type":"system","subtype":"compact_boundary","session_id":"x","compact_metadata":{"trigger":"auto","pre_tokens":167000}}"""));

        Assert.Equal("auto", boundary!.Trigger);
        Assert.Equal(167_000, boundary.PreTokens);
        Assert.NotNull(NativeCompaction.Read(NativeFrame.Of("""{"type":"system","subtype":"compact_boundary"}""")));
        Assert.Null(NativeCompaction.Read(NativeFrame.Of("""{"type":"system","subtype":"init"}""")));
        Assert.Null(NativeCompaction.Read(NativeFrame.Of("""{"type":"assistant","subtype":"compact_boundary"}""")));
        Assert.Null(NativeCompaction.Read(NativeFrame.Of("not json")));
    }
}
