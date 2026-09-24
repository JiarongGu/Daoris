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
        // And Daoris's own door onto the same grant (D73), which is still the person's to give.
        Assert.Contains("daoris agent trust claude-code", said);
    }

    /// <summary>
    /// 🔴 The command it names grants the account the hold read. A session under a named profile is
    /// held on that profile's file, so the command names the profile, or it would grant the machine's
    /// default account and the same hold would come back.
    /// </summary>
    [Fact]
    public void The_refusal_names_the_profile_the_hold_read()
    {
        Assert.Contains("--profile work", ClaudeTrust.Refusal(@"D:\fam\engine", "work"));
        Assert.DoesNotContain("--profile", ClaudeTrust.Refusal(@"D:\fam\engine"));
    }

    /// <summary>
    /// The twin of the CLI's `trustFile` rows (D73): the same two entries on both sides, because both
    /// doors onto Claude Code ignore an untrusted folder's allow-list — measured, not assumed.
    /// </summary>
    [Fact]
    public void The_harnesses_that_keep_a_trust_record_are_the_two_doors_onto_Claude_Code()
    {
        var adapters = AdapterSet.Built();

        var trusting = adapters.Names
            .Where(name => adapters.Resolve(name).Toolchain is { TrustFile: { Length: > 0 } })
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["claude-code", "claude-code-acp"], trusting);
        Assert.Equal(".claude.json", adapters.Resolve("claude-code").Toolchain!.TrustFile);
    }

    // ——— The grant (D73): asked, then written — never silently.

    /// <summary>
    /// A folder the harness never recorded gets its own entry, in the form Claude Code writes today —
    /// forward slashes — and the hold's own question then answers yes.
    /// </summary>
    [Fact]
    public void A_grant_records_a_new_folder_in_the_harness_own_form()
    {
        var config = Config("""{"numStartups":3,"projects":{"D:/fam/other":{"hasTrustDialogAccepted":true}}}""");

        var grant = ClaudeTrust.Grant(config, @"D:\fam\engine");

        Assert.True(grant.Changed);
        Assert.True(grant.Verified);
        Assert.Equal("D:/fam/engine", grant.Key);
        Assert.True(ClaudeTrust.Accepted(config, @"D:\fam\engine"));
    }

    /// <summary>
    /// 🔴 <b>Only the one flag moves.</b> The file is the harness's own, and every other field in it —
    /// its counters, its other projects, the entry's own history — means what it meant before.
    /// </summary>
    [Fact]
    public void A_grant_changes_the_one_flag_and_nothing_else_the_file_says()
    {
        var config = Config("""
            {
              "numStartups": 42,
              "userID": "abc",
              "projects": {
                "d:\\fam\\engine": { "allowedTools": [], "lastCost": 0.1234, "hasTrustDialogAccepted": false, "lastSessionId": "s1" },
                "D:/fam/other": { "hasTrustDialogAccepted": true, "mcpServers": {} }
              },
              "oauthAccount": { "emailAddress": "someone@example.invalid" }
            }
            """);

        var grant = ClaudeTrust.Grant(config, @"D:\fam\engine\");

        using var after = System.Text.Json.JsonDocument.Parse(File.ReadAllText(config));
        var root = after.RootElement;
        Assert.Equal(42, root.GetProperty("numStartups").GetInt32());
        Assert.Equal("abc", root.GetProperty("userID").GetString());
        Assert.Equal("someone@example.invalid", root.GetProperty("oauthAccount").GetProperty("emailAddress").GetString());
        var projects = root.GetProperty("projects");
        // The existing entry is updated IN PLACE, under the key the harness wrote — not duplicated
        // under a second spelling of the same folder.
        Assert.Equal(2, projects.EnumerateObject().Count());
        Assert.Equal(@"d:\fam\engine", grant.Key);
        var engine = projects.GetProperty(@"d:\fam\engine");
        Assert.True(engine.GetProperty("hasTrustDialogAccepted").GetBoolean());
        Assert.Equal(0.1234m, engine.GetProperty("lastCost").GetDecimal());
        Assert.Equal("s1", engine.GetProperty("lastSessionId").GetString());
        Assert.True(projects.GetProperty("D:/fam/other").GetProperty("hasTrustDialogAccepted").GetBoolean());
    }

    [Fact]
    public void Granting_what_is_already_granted_writes_nothing()
    {
        var config = Config("""{"projects":{"D:/fam/engine":{"hasTrustDialogAccepted":true}}}""");
        var before = File.GetLastWriteTimeUtc(config);
        var bytes = File.ReadAllBytes(config);

        var grant = ClaudeTrust.Grant(config, @"D:\fam\engine");

        Assert.False(grant.Changed);
        Assert.True(grant.Verified);
        Assert.Equal(bytes, File.ReadAllBytes(config));
        Assert.Equal(before, File.GetLastWriteTimeUtc(config));
    }

    /// <summary>
    /// A configuration home the harness has never run in has no file yet. The grant makes one holding
    /// only the grant; the harness fills in the rest itself when it first starts there.
    /// </summary>
    [Fact]
    public void A_grant_into_a_home_with_no_file_yet_makes_one_holding_only_the_grant()
    {
        var config = Path.Combine(_home, "profile", ".claude.json");

        var grant = ClaudeTrust.Grant(config, @"D:\fam\engine");

        Assert.True(grant.Changed);
        Assert.True(ClaudeTrust.Accepted(config, @"D:\fam\engine"));
    }

    /// <summary>
    /// 🔴 <b>A file this build cannot read is never overwritten.</b> Unknown is permissive for the
    /// QUESTION; for a WRITE it is a refusal, because replacing somebody else's file with our guess of
    /// it would destroy everything in it we did not understand.
    /// </summary>
    [Theory]
    [InlineData("not json at all")]
    [InlineData("""{"projects":"a string where an object was"}""")]
    [InlineData("""[1,2,3]""")]
    public void A_file_this_build_cannot_read_is_refused_and_left_as_it_was(string json)
    {
        var config = Config(json);

        var error = Assert.Throws<DriverException>(() => ClaudeTrust.Grant(config, @"D:\fam\engine"));

        Assert.Contains(config, error.Message);
        Assert.Equal(json, File.ReadAllText(config));
    }

    /// <summary>The write is atomic: beside, then rename — nothing of ours is left in the home.</summary>
    [Fact]
    public void A_grant_leaves_nothing_beside_the_file()
    {
        var config = Config("""{"projects":{}}""");

        ClaudeTrust.Grant(config, @"D:\fam\engine");

        Assert.Equal([".claude.json"], Directory.GetFiles(_home).Select(Path.GetFileName));
    }
}
