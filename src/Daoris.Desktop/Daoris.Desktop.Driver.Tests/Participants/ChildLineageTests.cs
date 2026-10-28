using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-C1a: the session's lineage file under the home, which the connector, the hook and the driver write and the driver
/// reads (<c>&lt;home&gt;/sessions/&lt;id&gt;/lineage.jsonl</c>, the connector's <c>LineageFile</c> its twin); the hook handed to
/// Claude Code beside the rules; and the part's end written when its run lets go.
/// </summary>
/// <remarks>Files only: the suite's fast half. The hook as node runs it is <see cref="LineageHookProcessTests"/>.</remarks>
public sealed class ChildLineageTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-lineage-" + Guid.NewGuid().ToString("N")[..8]);

    public ChildLineageTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    [Fact]
    public void The_file_is_the_session_s_own_in_its_folder_named_as_the_connector_names_it()
    {
        Assert.Equal("lineage.jsonl", ChildLineage.FileName);
        Assert.Equal(DaorisHome.Entry(_home, HomeEntry.Sessions, "s1", "lineage.jsonl"), ChildLineage.PathOf(_home, "s1"));
        // The sessions' records are data (D172), under `data/` in a new home (HOMELAYOUT3), where they were in an install's.
        Assert.Equal(["data", "sessions", "s1", "lineage.jsonl"], Path.GetRelativePath(_home, ChildLineage.PathOf(_home, "s1")).Split(Path.DirectorySeparatorChar));
    }

    [Fact]
    public void Lines_are_appended_in_utf8_with_no_bom_and_read_back_as_the_part_s_bindings()
    {
        ChildLineage.Append(_home, "s1", """{"by":"hook","event":"started","agent":"a1","type":"general-purpose"}""");
        ChildLineage.Append(_home, "s1", """{"by":"stream","event":"started","agent":"a1","depth":1}""");

        var bytes = File.ReadAllBytes(ChildLineage.PathOf(_home, "s1"));
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.DoesNotContain("\r", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        var child = Assert.Single(ChildLineage.Read(_home, "s1").Children);
        Assert.Equal(("a1", 1), (child.Native, child.Depth));
    }

    [Fact]
    public void A_session_with_no_file_has_no_children_and_its_end_writes_none()
    {
        ChildLineage.End(_home, "s1", clean: true, DateTimeOffset.UnixEpoch);

        Assert.False(File.Exists(ChildLineage.PathOf(_home, "s1")));
        Assert.Empty(ChildLineage.Read(_home, "s1").Children);
    }

    [Fact]
    public void The_end_is_appended_where_the_session_has_lineage_and_a_killed_root_loses_its_children()
    {
        ChildLineage.Append(_home, "s1", """{"by":"hook","event":"started","agent":"a1"}""");

        ChildLineage.End(_home, "s1", clean: false, DateTimeOffset.UnixEpoch);

        var bound = ChildLineage.Read(_home, "s1");
        Assert.True(bound.PartEnded);
        Assert.Equal(ChildBindings.Lost, Assert.Single(bound.Children).State);
    }

    [Fact]
    public void An_id_that_is_not_a_session_s_writes_nothing_and_reads_nothing()
    {
        ChildLineage.Append(_home, "..", """{"by":"hook","event":"started","agent":"a1"}""");
        ChildLineage.End(_home, "a/b", clean: true, DateTimeOffset.UnixEpoch);

        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.Sessions)));
        Assert.Empty(ChildLineage.Read(_home, "..").Children);
    }

    [Fact]
    public void The_stream_watch_writes_each_subagent_s_start_and_end_off_claude_code_s_stream()
    {
        var watch = ChildLineage.Watch(_home, "s1");

        watch("""{"type":"system","subtype":"init","session_id":"c1"}""");
        watch("""{"type":"system","subtype":"task_started","task_id":"a1","tool_use_id":"toolu_1","subagent_type":"general-purpose","spawn_depth":1,"task_type":"local_agent"}""");
        watch("""{"type":"system","subtype":"task_started","task_id":"a2","tool_use_id":"toolu_2","subagent_type":"general-purpose","spawn_depth":2,"parent_task_id":"a1","task_type":"local_agent"}""");
        watch("""{"type":"system","subtype":"task_notification","task_id":"a2","status":"completed"}""");

        var bound = ChildLineage.Read(_home, "s1");
        Assert.Equal(
            [("a1", (string?)null, (int?)1, ChildBindings.Active), ("a2", "a1", 2, ChildBindings.Finished)],
            bound.Children.Select(child => (child.Native, child.Parent, child.Depth, child.State)));
    }

    [Fact]
    public void The_hook_is_written_under_the_home_as_shipped_and_handed_with_the_session_s_file()
    {
        var hook = ChildLineage.HookFor(_home, "s1");

        Assert.NotNull(hook);
        Assert.Equal(DaorisHome.Entry(_home, HomeEntry.Hooks, "lineage-hook.mjs"), hook.Script);
        Assert.Equal(ChildLineage.Source, File.ReadAllText(hook.Script));
        Assert.Equal(ChildLineage.PathOf(_home, "s1"), hook.File);
        Assert.DoesNotContain("\r", ChildLineage.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_spawn_file_hands_the_lineage_hook_on_the_connector_s_calls_and_each_subagent_s_start_and_stop()
    {
        var lineage = ChildLineage.HookFor(_home, "s1")!;

        var path = SpawnSettings.Write(_home, "s1", RuleLists.Empty, [], lineage: lineage);

        Assert.NotNull(path);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var hooks = document.RootElement.GetProperty("hooks");
        var call = Assert.Single(hooks.GetProperty("PreToolUse").EnumerateArray());
        Assert.Equal("mcp__daoris-knowledge__.*", call.GetProperty("matcher").GetString());
        foreach (var entry in new[] { call, Assert.Single(hooks.GetProperty("SubagentStart").EnumerateArray()), Assert.Single(hooks.GetProperty("SubagentStop").EnumerateArray()) })
        {
            var hook = Assert.Single(entry.GetProperty("hooks").EnumerateArray());
            Assert.Equal("command", hook.GetProperty("type").GetString());
            Assert.Equal(lineage.Node, hook.GetProperty("command").GetString());
            Assert.Equal([lineage.Script, lineage.File], hook.GetProperty("args").EnumerateArray().Select(arg => arg.GetString()));
        }
    }

    [Fact]
    public void Beside_the_tree_guard_each_keeps_its_own_entry_and_the_trace_says_the_guard_only_where_it_is()
    {
        var lineage = ChildLineage.HookFor(_home, "s1")!;
        var guard = TreeGuard.For(_home, _home);

        using var both = JsonDocument.Parse(File.ReadAllText(SpawnSettings.Write(_home, "s1", RuleLists.Empty, [], guard, lineage)!));
        using var alone = JsonDocument.Parse(File.ReadAllText(SpawnSettings.Write(_home, "s2", RuleLists.Empty, [], lineage: lineage)!));

        Assert.Equal(
            [TreeGuard.Matcher, "mcp__daoris-knowledge__.*"],
            both.RootElement.GetProperty("hooks").GetProperty("PreToolUse").EnumerateArray().Select(entry => entry.GetProperty("matcher").GetString()));
        Assert.True(TraceRules.HandsTreeGuard(both.RootElement));
        Assert.False(TraceRules.HandsTreeGuard(alone.RootElement));
    }
}
