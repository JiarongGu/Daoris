using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The git access a running session holds: its record begun at its start, held as its repository resolves narrowed by what
/// the person tightened, a tightening that only narrows, the doors' judge reading it for the session's marker, and
/// <c>daoris git access --session</c> in the terminal.
/// </summary>
public sealed class GitSessionAccessTests : IDisposable
{
    private readonly string _root = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "git-session-access", Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "data");

    public GitSessionAccessTests() => Directory.CreateDirectory(Home);

    public void Dispose() => GitSeed.Discard(_root);

    private void Put(string json)
    {
        var path = DaorisHome.Entry(Home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    private static string Show(IEnumerable<GitAccessClass> classes) => string.Join(" ", classes.Select(each => $"{each.Class}={each.Value}@{each.Source}"));

    [Fact]
    public void A_record_begun_at_the_start_holds_what_its_repository_resolves_to_now()
    {
        GitSessionAccess.Begin(Home, "s1a2b3c4", "engine", "Games");
        Put("""{"gitAccess":{"engine":{"write":"ask"}}}""");

        var record = GitSessionAccess.Read(Home, "s1a2b3c4")!;

        Assert.Equal(("engine", "Games"), (record.Repository, record.Workspace));
        Assert.Equal("read=allow@default write=ask@repository dangerous=deny@default", Show(record.Classes));
        Assert.Null(GitSessionAccess.Read(Home, "nobody"));
    }

    [Fact]
    public void Tightening_narrows_a_class_for_the_session_alone_and_a_later_loosening_does_not_lift_it()
    {
        Put("""{"machineGitAccess":{"write":"allow"}}""");
        GitSessionAccess.Begin(Home, "s1", "engine", null);
        GitSessionAccess.Begin(Home, "s2", "engine", null);

        var tightened = GitSessionAccess.Tighten(Home, "s1", new Dictionary<string, string> { ["write"] = "deny" });
        Put("""{"machineGitAccess":{"write":"allow","read":"allow"}}""");

        Assert.Equal("read=allow@default write=deny@session dangerous=deny@default", Show(tightened.Classes));
        Assert.Equal("write=deny@session", Show(GitSessionAccess.Read(Home, "s1")!.Classes.Where(each => each.Source == "session")));
        Assert.Equal("allow", GitSessionAccess.Read(Home, "s2")!.Classes.First(each => each.Class == "write").Value);
        // Begun again, as a resumed session is, it keeps what the person tightened.
        GitSessionAccess.Begin(Home, "s1", "engine", null);
        Assert.Equal("deny", GitSessionAccess.Held(Home, "s1", "engine", null).Of("write").Value);
        // Another session, and a marker that names none, are judged by the repository's.
        Assert.Equal("allow", GitSessionAccess.Held(Home, "s2", "engine", null).Of("write").Value);
        Assert.Equal("allow", GitSessionAccess.Held(Home, null, "engine", null).Of("write").Value);
    }

    [Theory]
    [InlineData("read", "allow", "git access here only tightens")]
    [InlineData("write", "ask", "git access here only tightens")]
    [InlineData("dangerous", "allow", "git access here only tightens")]
    [InlineData("dangerous", "ask", "never ask")]
    [InlineData("push", "deny", "not a git access class")]
    [InlineData("write", "never", "not a value")]
    public void A_tightening_that_does_not_narrow_or_is_not_a_class_or_value_is_refused_and_nothing_is_written(string cls, string value, string said)
    {
        Put("""{"machineGitAccess":{"write":"ask"}}""");
        GitSessionAccess.Begin(Home, "s1", "engine", null);

        var refused = Assert.Throws<DriverException>(() => GitSessionAccess.Tighten(Home, "s1", new Dictionary<string, string> { [cls] = value }));

        Assert.Contains(said, refused.Message);
        Assert.DoesNotContain("session", Show(GitSessionAccess.Read(Home, "s1")!.Classes));
        Assert.Throws<DriverException>(() => GitSessionAccess.Tighten(Home, "nobody", new Dictionary<string, string> { ["write"] = "deny" }));
    }

    [Fact]
    public void The_doors_judge_a_sessions_act_by_what_it_holds()
    {
        Put("""{"machineGitAccess":{"write":"allow"}}""");
        GitSessionAccess.Begin(Home, "s1", "engine", null);
        GitSessionAccess.Tighten(Home, "s1", new Dictionary<string, string> { ["write"] = "deny" });

        var held = GitSessionAccess.Held(Home, "s1", "engine", null).Of(GitAccessKeys.Write).Value;
        var other = GitSessionAccess.Held(Home, "s2", "engine", null).Of(GitAccessKeys.Write).Value;

        Assert.Equal("access-denied", GitAccessRules.Judge(GitAct.Write, held, granted: false).Code);
        Assert.Equal("access", GitAccessRules.Judge(GitAct.Write, other, granted: false).Code);
    }

    // ── The terminal's verb ────────────────────────────────────────────────────

    private (int Code, string Out, string Err) Run(string session, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        GitPress press = (_, _, alone, _) => alone();
        var world = new GitAccessWorld(Home, press, name => name == "DAORIS_SESSION_ID" ? session : null, _ => Task.FromResult<string?>("Games"));
        var code = GitAccessCommand.RunAsync(args, world, output, error).GetAwaiter().GetResult();
        return (code, output.ToString(), error.ToString());
    }

    [Fact]
    public void The_terminal_says_what_a_session_holds_and_tightens_it()
    {
        Put("""{"machineGitAccess":{"write":"ask"}}""");
        GitSessionAccess.Begin(Home, "s1", "engine", null);

        var (said, text, _) = Run("", "access", "--session", "s1");
        Assert.Equal(0, said);
        Assert.Contains("write      ask  (set for the machine)", text);

        var (tightened, changed, _) = Run("", "access", "--session", "s1", "write=deny");
        Assert.Equal(0, tightened);
        Assert.Contains("tightened", changed);
        Assert.Contains("write      deny  (set for the session)", changed);

        var (_, json, _) = Run("", "access", "--session", "s1", "--json");
        Assert.Equal("deny", JsonDocument.Parse(json).RootElement.EnumerateArray().First(each => each.GetProperty("class").GetString() == "write").GetProperty("value").GetString());
    }

    [Fact]
    public void The_terminal_refuses_a_widening_an_unknown_session_and_every_change_from_a_session_s_own_environment()
    {
        Put("""{"machineGitAccess":{"write":"ask"}}""");
        GitSessionAccess.Begin(Home, "s1", "engine", null);

        var (widened, _, widenedError) = Run("", "access", "--session", "s1", "write=allow");
        var (unknown, _, unknownError) = Run("", "access", "--session", "nobody");
        var (agent, _, agentError) = Run("s9", "access", "--session", "s1", "write=deny");
        var (mixed, _, mixedError) = Run("", "access", "engine", "--session", "s1");

        Assert.Equal((1, 1, 1, 1), (widened, unknown, agent, mixed));
        Assert.Contains("only tightens", widenedError);
        Assert.Contains("no running session nobody", unknownError);
        Assert.Contains("this terminal is a session's", agentError);
        Assert.Contains("class words alone", mixedError);
        Assert.Equal("ask", GitSessionAccess.Held(Home, "s1", "engine", null).Of("write").Value);
    }
}
