using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// **Can this harness actually use what the repository allows it?** (DEPLOY1.)
/// </summary>
/// <remarks>
/// <para>🔴 <b>Written from a measured failure, not from documentation.</b> Three real driven runs
/// did the work and none could take its quest, because Claude Code <b>ignores</b> a repository's
/// `permissions.allow` until a person has accepted that path in the harness's own config:
/// <c>Ignoring 9 permissions.allow entries from .claude/settings.json: this workspace has not been
/// trusted.</c> Nine minutes and a real login each time, for a run that could never have closed its
/// quest.</para>
///
/// <para><b>Read, never written.</b> The flag is the person's grant — a tool that wrote it would have
/// removed the one step in the chain that was theirs (SES3). So this answers a question and the
/// driver refuses on it, which turns a nine-minute silent failure into an instant sentence naming the
/// one command that fixes it.</para>
///
/// <para><b>Only a definite NO refuses</b>, the same rule the login question follows: no config file,
/// unreadable JSON, or a harness with no notion of trust are all <c>null</c> — unknown, and
/// permissive.</para>
/// </remarks>
public sealed class WorkspaceTrustTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-trust-" + Guid.NewGuid().ToString("N")[..8]);

    public WorkspaceTrustTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private string Config(string json)
    {
        var path = Path.Combine(_home, ".claude.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void An_accepted_workspace_is_trusted()
    {
        var config = Config("""{"projects":{"D:\\fam\\engine":{"hasTrustDialogAccepted":true}}}""");

        Assert.True(ClaudeTrust.Accepted(config, @"D:\fam\engine"));
    }

    /// <summary>
    /// 🔴 <b>Absent is NOT unknown here — it is untrusted.</b> The harness prompts on first visit, so
    /// a path it has never recorded has never been accepted, and the allow-list is ignored exactly as
    /// if the flag were false. This is the case the deployment actually hit.
    /// </summary>
    [Fact]
    public void A_workspace_the_harness_has_never_recorded_is_not_trusted()
    {
        var config = Config("""{"projects":{"D:\\fam\\other":{"hasTrustDialogAccepted":true}}}""");

        Assert.False(ClaudeTrust.Accepted(config, @"D:\fam\engine"));
    }

    [Fact]
    public void A_workspace_recorded_as_declined_is_not_trusted()
    {
        var config = Config("""{"projects":{"D:\\fam\\engine":{"hasTrustDialogAccepted":false}}}""");

        Assert.False(ClaudeTrust.Accepted(config, @"D:\fam\engine"));
    }

    /// <summary>
    /// Windows paths differ by case and by separator and name the same directory. A check that said
    /// "never trusted" about a path the person had accepted would be worse than no check: it would
    /// refuse runs that work.
    /// </summary>
    [Theory]
    [InlineData(@"d:\fam\engine")]
    [InlineData(@"D:/fam/engine")]
    [InlineData(@"D:\fam\engine\")]
    public void The_same_directory_spelled_differently_is_the_same_workspace(string spelling)
    {
        var config = Config("""{"projects":{"D:\\fam\\engine":{"hasTrustDialogAccepted":true}}}""");

        Assert.True(ClaudeTrust.Accepted(config, spelling));
    }

    /// <summary>
    /// 🔴 Unknown is permissive, and each of these is unknown. A machine whose harness has never run,
    /// a config this build cannot parse, or a shape that changed under us must not stop a loop that
    /// would otherwise work — the cost of a wrong refusal is higher than the cost of the run.
    /// </summary>
    [Fact]
    public void No_config_is_unknown_rather_than_untrusted()
    {
        Assert.Null(ClaudeTrust.Accepted(Path.Combine(_home, "absent.json"), @"D:\fam\engine"));
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("""{"projects":"a string where an object was"}""")]
    [InlineData("""{}""")]
    public void A_config_this_build_cannot_read_is_unknown(string json)
    {
        Assert.Null(ClaudeTrust.Accepted(Config(json), @"D:\fam\engine"));
    }

    /// <summary>
    /// The sentence a person acts on. It names the path, the one command, and why it is being asked —
    /// a refusal that says only "not trusted" sends somebody to search for what that means.
    /// </summary>
    [Fact]
    public void The_refusal_names_the_path_and_the_fix()
    {
        var said = ClaudeTrust.Refusal(@"D:\fam\engine");

        Assert.Contains(@"D:\fam\engine", said);
        Assert.Contains("claude", said, StringComparison.OrdinalIgnoreCase);
        // What it costs if nobody does it — the reason this is a refusal and not a warning.
        Assert.Contains("quest", said, StringComparison.OrdinalIgnoreCase);
    }
}
