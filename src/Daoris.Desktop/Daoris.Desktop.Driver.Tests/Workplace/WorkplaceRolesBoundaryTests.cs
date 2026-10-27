using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

public sealed class WorkplaceRolesBoundaryTests : IDisposable
{
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "workplace-roles", Guid.NewGuid().ToString("N"));
    public void Dispose() => GitSeed.Remove(_home);

    [Fact]
    public void Git_and_tool_hands_strip_the_literal_holder_key_and_keep_unrelated_environment()
    {
        Assert.Equal(["SSH_ASKPASS", "SSH_ASKPASS_REQUIRE", "GIT_TRACE_CURL", "GIT_CURL_VERBOSE", "GCM_TRACE", "GCM_TRACE_SECRETS", "DAORIS_ROLE_KEY"], GitWiring.Removed);
        foreach (var tool in new[] { false, true })
        {
            var start = new ProcessStartInfo("unused") { UseShellExecute = false };
            start.Environment["DAORIS_ROLE_KEY"] = new string('1', 64);
            start.Environment["WORKPLACE_TEST_VALUE"] = "retained";
            if (tool) Tools.Hand(start, _home);
            else GitWiring.Hand(start);
            Assert.False(start.Environment.ContainsKey("DAORIS_ROLE_KEY"));
            Assert.Equal("retained", start.Environment["WORKPLACE_TEST_VALUE"]);
        }
    }

    [Theory]
    [InlineData("none")]
    [InlineData("directory")]
    [InlineData("pointer")]
    [InlineData("partial-directory")]
    public void Literal_historical_paths_keep_inventory_order_mark_names_actual_bytes_and_refusal_continuation(string refusal)
    {
        var sessions = DaorisHome.Entry(_home, HomeEntry.Sessions);
        var decisions = Path.Combine(sessions, "s1.decisions");
        var pointer = Path.Combine(sessions, "s1.supervisor.json");
        Directory.CreateDirectory(decisions);
        var decision = Path.Combine(decisions, "decision.json");
        var states = Path.Combine(decisions, "decision.states");
        var conversation = Path.Combine(sessions, "s1.events.jsonl");
        var transcript = Path.Combine(sessions, "s1.log");
        File.WriteAllText(decision, "12345");
        File.WriteAllText(states, "12");
        File.WriteAllText(pointer, "1234567");
        File.WriteAllText(conversation, "abc");
        File.WriteAllText(transcript, "tx");
        var calls = new List<(string Path, bool Folder)>();
        var files = new SessionHomeFiles(_home, (path, folder) =>
        {
            calls.Add((path, folder));
            if (path == decisions && refusal is "directory" or "partial-directory")
            {
                if (refusal == "partial-directory") File.Delete(decision);
                throw new IOException("held decisions");
            }
            if (path == pointer && refusal == "pointer") throw new UnauthorizedAccessException("held pointer");
            SessionHomeFiles.FromDisk(path, folder);
        });
        Assert.Equal((3L, 2L, 14L), (files.Size("s1").Conversations, files.Size("s1").Transcripts, files.Size("s1").Other));
        var result = files.Remove(["s1"], null);
        Assert.Equal([(conversation, false), (transcript, false), (decisions, true), (pointer, false)], calls);
        Assert.Equal(["conversation", "transcript", "mark"], result.Went["s1"]);
        Assert.Equal(refusal == "none" ? Array.Empty<string>() : ["mark"], result.Stayed["s1"]);
        Assert.Equal(refusal switch { "none" => Array.Empty<string>(), "pointer" => [pointer], _ => [decisions] }, result.Failed);
        var other = refusal switch { "directory" or "pointer" => 7, "partial-directory" => 12, _ => 14 };
        Assert.Equal((3L, 2L, (long)other), (result.Bytes["s1"].Conversations, result.Bytes["s1"].Transcripts, result.Bytes["s1"].Other));
        Assert.Equal(refusal is "directory" or "partial-directory", Directory.Exists(decisions));
        Assert.Equal(refusal == "pointer", File.Exists(pointer));
        Assert.Contains(".supervisor.json", SessionHomeFiles.SessionSuffixes);
        Assert.DoesNotContain(".decisions", SessionHomeFiles.SessionSuffixes);
    }
}
