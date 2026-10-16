using System.Text;
using System.Text.Json.Nodes;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// FABRIC-C1a: where the connector keeps what each call's harness wrote of its lineage, for the driver to bind to the part
/// (<c>&lt;home&gt;/sessions/&lt;session&gt;/lineage.jsonl</c>, one JSON object a line, the driver's <c>ChildLineage</c> its
/// reader). Only for a session the driver named, under the home it named; appended by several processes at once (Codex starts a
/// connector per thread, and Claude Code's hook writes the same file), so a line is never lost to another's.
/// </summary>
public sealed class LineageFileTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-lineage-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Written(string session) => DaorisHome.Entry(_home, HomeEntry.Sessions, session, LineageFile.FileName);

    [Fact]
    public void A_call_is_appended_under_the_named_session_in_utf8_with_no_bom_and_lf()
    {
        var file = new LineageFile(_home, "s1a2b3c4");
        file.Call("knowledge_search", new CallLineage(CallLineage.Claude) { ToolUse = "toolu_1" }, DateTimeOffset.UnixEpoch);
        file.Call("knowledge_get", null, DateTimeOffset.UnixEpoch);

        Assert.Equal(Written("s1a2b3c4"), file.Path);
        var bytes = File.ReadAllBytes(Written("s1a2b3c4"));
        Assert.NotEqual(0xEF, bytes[0]);
        var text = Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("\r", text, StringComparison.Ordinal);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["knowledge_search", "knowledge_get"], lines.Select(line => (string)JsonNode.Parse(line)!["tool"]!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("c:x")]
    public void With_no_session_or_one_that_is_not_an_id_nothing_is_written(string? session)
    {
        var file = new LineageFile(_home, session);
        file.Call("knowledge_search", null, DateTimeOffset.UnixEpoch);

        Assert.Null(file.Path);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.Sessions)));
    }

    [Fact]
    public void With_no_home_nothing_is_written()
    {
        var file = new LineageFile(null, "s1");
        file.Call("knowledge_search", null, DateTimeOffset.UnixEpoch);

        Assert.Null(file.Path);
    }

    [Fact]
    public void The_home_is_the_rules_home_the_driver_named_else_the_daoris_home_and_the_session_is_the_connectors()
    {
        var rules = Path.Combine(_home, "rules");
        var named = LineageFile.FromEnvironment(name => name switch
        {
            "DAORIS_RULES_HOME" => rules, "DAORIS_HOME" => _home, "DAORIS_SESSION_ID" => " s1 ", _ => null,
        });
        var home = LineageFile.FromEnvironment(name => name switch { "DAORIS_HOME" => _home, "DAORIS_SESSION_ID" => "s1", _ => null });
        var none = LineageFile.FromEnvironment(name => name switch { "DAORIS_HOME" => _home, _ => null });

        Assert.Equal(DaorisHome.Entry(rules, HomeEntry.Sessions, "s1", LineageFile.FileName), named.Path);
        Assert.Equal(Written("s1"), home.Path);
        Assert.Null(none.Path);
    }

    [Fact]
    public void Writers_at_once_lose_no_line()
    {
        const int Writers = 8, Each = 25;
        Parallel.For(0, Writers, writer =>
        {
            var file = new LineageFile(_home, "s1");
            for (var call = 0; call < Each; call++) file.Call($"tool_{writer}_{call}", null, DateTimeOffset.UnixEpoch);
        });

        var lines = File.ReadAllLines(Written("s1"));
        Assert.Equal(Writers * Each, lines.Length);
        Assert.All(lines, line => Assert.NotNull(JsonNode.Parse(line)!["tool"]));
    }

    [Fact]
    public void A_file_that_cannot_be_written_never_fails_the_call()
    {
        // A folder where the file would be: the append fails, and the call it records goes on.
        Directory.CreateDirectory(Written("s1"));
        var file = new LineageFile(_home, "s1");

        file.Call("knowledge_search", null, DateTimeOffset.UnixEpoch);
    }
}
