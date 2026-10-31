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
    public void A_process_s_start_makes_the_file_and_its_end_names_the_same_process()
    {
        ChildLineage.Start(_home, "s1", "0123456789abcdef", "4242 1", DateTimeOffset.UnixEpoch);
        ChildLineage.End(_home, "s1", clean: true, DateTimeOffset.UnixEpoch, "0123456789abcdef");
        ChildLineage.Start(_home, "..", "0123456789abcdef", "4242 1", DateTimeOffset.UnixEpoch);

        var lines = File.ReadAllLines(ChildLineage.PathOf(_home, "s1")).Select(line => JsonDocument.Parse(line).RootElement).ToList();
        Assert.Equal(
            [("start", "0123456789abcdef"), ("end", "0123456789abcdef")],
            lines.Select(line => (line.GetProperty("by").GetString(), line.GetProperty("proc").GetString())));
        Assert.Equal("4242 1", lines[0].GetProperty("issuer").GetString());
    }

    [Fact]
    public void The_end_is_appended_where_the_session_has_lineage_and_a_killed_process_loses_its_children()
    {
        ChildLineage.Start(_home, "s1", "0123456789abcdef", "4242 1", DateTimeOffset.UnixEpoch);
        ChildLineage.Append(_home, "s1", """{"by":"hook","event":"started","agent":"a1","proc":"0123456789abcdef"}""");

        ChildLineage.End(_home, "s1", clean: false, DateTimeOffset.UnixEpoch, "0123456789abcdef");

        Assert.Equal(ChildBindings.Lost, Assert.Single(ChildLineage.Read(_home, "s1").Children).State);
    }

    /// <summary>COWORKVIEW1a's rule 9: no file, read, or unreadable with its kind; the length read is up to the last full line.</summary>
    [Fact]
    public void A_read_finds_no_file_reads_it_or_cannot_and_an_unreadable_file_is_never_an_empty_fold()
    {
        Assert.Equal(LineageReading.Absent, ChildLineage.Reading(_home, "s1").Outcome);

        ChildLineage.Start(_home, "s1", "0123456789abcdef", "4242 1", DateTimeOffset.UnixEpoch);
        ChildLineage.Append(_home, "s1", """{"by":"hook","event":"started","agent":"a1","proc":"0123456789abcdef"}""");
        var path = ChildLineage.PathOf(_home, "s1");
        var whole = new FileInfo(path).Length;
        File.AppendAllText(path, """{"by":"hook","event":"sto""");

        var read = ChildLineage.Reading(_home, "s1");
        Assert.Equal((LineageReading.Found, whole), (read.Outcome, read.Length));
        Assert.Single(read.Bindings.Children);

        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var unreadable = ChildLineage.Reading(_home, "s1");
            Assert.Equal((LineageReading.Unreadable, "IOException"), (unreadable.Outcome, unreadable.Error));
        }
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
    public void The_writer_writes_each_subagent_s_start_and_end_off_claude_code_s_stream()
    {
        var writer = ChildLineage.Writer(_home, "s1");

        writer.Stream("""{"type":"system","subtype":"init","session_id":"c1"}""");
        writer.Stream("""{"type":"system","subtype":"task_started","task_id":"a1","tool_use_id":"toolu_1","subagent_type":"general-purpose","spawn_depth":1,"task_type":"local_agent"}""");
        writer.Stream("""{"type":"system","subtype":"task_started","task_id":"a2","tool_use_id":"toolu_2","subagent_type":"general-purpose","spawn_depth":2,"parent_task_id":"a1","task_type":"local_agent"}""");
        writer.Stream("""{"type":"system","subtype":"task_notification","task_id":"a2","status":"completed"}""");

        var bound = ChildLineage.Read(_home, "s1");
        Assert.Equal(
            [("a1", (string?)null, (int?)1, ChildBindings.Uncertain), ("a2", "a1", 2, ChildBindings.Completed)],
            bound.Children.Select(child => (child.Native, child.Parent, child.Depth, child.State)));
    }

    [Fact]
    public void The_writer_appends_the_protocol_door_s_announcements_and_ends_naming_its_process()
    {
        var writer = ChildLineage.Writer(_home, "s1", "0123456789abcdef");

        writer.Spawned(new LineageAnnouncement("a1", "Reader", "Read it.") { ByRoot = true });
        writer.Spawned(new LineageAnnouncement("../a2", "Bad", null) { ByRoot = true });
        writer.Ended("a1", "completed");

        var lines = File.ReadAllLines(ChildLineage.PathOf(_home, "s1")).Select(line => JsonDocument.Parse(line).RootElement).ToList();
        Assert.Equal(
            [("acp", "started", "a1", "0123456789abcdef"), ("acp", "ended", "a1", "0123456789abcdef")],
            lines.Select(line => (line.GetProperty("by").GetString(), line.GetProperty("event").GetString(), line.GetProperty("agent").GetString(), line.GetProperty("proc").GetString())));
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

    /// <summary>A session still running from before the guard judged a shell's writes (TREESHELL1) was handed the edit tools' matcher: still the guard.</summary>
    [Fact]
    public void The_trace_still_reads_the_guard_a_session_started_before_the_shells_were_judged_was_handed()
    {
        using var before = JsonDocument.Parse(
            "{\"hooks\":{\"PreToolUse\":[{\"matcher\":\"" + TreeGuardHook.EditMatcher + "\",\"hooks\":[]}]}}");

        Assert.True(TraceRules.HandsTreeGuard(before.RootElement));
    }
}
