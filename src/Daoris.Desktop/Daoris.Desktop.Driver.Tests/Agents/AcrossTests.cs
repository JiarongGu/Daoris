using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Reading and writing across repositories (READ1, D107): a checkout is readable by agents outside it
/// unless its repository, or failing that its workspace, says off; a repository's sessions write into
/// another only where the person declared it. The setting lives in `driver.json`.
/// </summary>
/// <remarks>
/// The file's shape is a twin (`.claude/knowledge/twins.md`): the CLI's <c>driverconfig.test.ts</c> holds
/// the same shape cases, answer for answer. Only the driver resolves the setting, so the precedence, the
/// reach and the rules are held here alone.
/// </remarks>
public sealed class AcrossTests
{
    // ——— The file's shape (the CLI's table matches these rows).

    [Fact]
    public void Absent_is_reading_on_and_no_relationship()
    {
        var config = DriverConfig.Parse("{}");

        Assert.Empty(config.ReadAcross);
        Assert.Empty(config.WorkspaceReadAcross);
        Assert.Empty(config.WriteAcross);
        Assert.Equal(new AcrossReading(true, AcrossSource.Default), AcrossRules.Reading(config, "engine", "aurora"));
        Assert.Empty(AcrossRules.WritesTo(config, "engine"));
    }

    [Fact]
    public void Only_a_boolean_is_read_as_reading_and_only_other_names_as_a_relationship()
    {
        var config = DriverConfig.Parse("""
            {
              "readAcross": { "engine": false, "game": true, "odd": "no", "none": null },
              "workspaceReadAcross": { "aurora": false, "forge": 1 },
              "writeAcross": { "plugins": ["engine", "Engine", "plugins", 3, "", "game"], "bad": "engine", "empty": [] }
            }
            """);

        Assert.Equal(new Dictionary<string, bool> { ["engine"] = false, ["game"] = true }, config.ReadAcross);
        Assert.Equal(new Dictionary<string, bool> { ["aurora"] = false }, config.WorkspaceReadAcross);
        Assert.Equal(["plugins"], config.WriteAcross.Keys);
        // One entry per name in any case, never itself, in the order first written.
        Assert.Equal(["engine", "game"], config.WriteAcross["plugins"]);
    }

    [Fact]
    public void Each_key_is_written_only_when_set_and_reads_back_as_written()
    {
        Assert.DoesNotContain("readAcross", DriverConfig.Empty.ToJson());
        Assert.DoesNotContain("workspaceReadAcross", DriverConfig.Empty.ToJson());
        Assert.DoesNotContain("writeAcross", DriverConfig.Empty.ToJson());

        var config = DriverConfig.Empty
            .WithReadAcross("engine", false)
            .WithWorkspaceReadAcross("aurora", false)
            .WithWriteAcross("plugins", "engine", allow: true);
        var again = DriverConfig.Parse(config.ToJson());

        Assert.False(again.ReadAcross["engine"]);
        Assert.False(again.WorkspaceReadAcross["aurora"]);
        Assert.Equal(["engine"], again.WriteAcross["plugins"]);
    }

    [Fact]
    public void Reading_is_set_and_cleared_by_name_in_any_case()
    {
        var config = DriverConfig.Empty.WithReadAcross("Engine", false).WithWorkspaceReadAcross("aurora", true);

        Assert.False(config.ReadAcross["engine"]);
        Assert.Empty(config.WithReadAcross("ENGINE", null).ReadAcross);
        Assert.Empty(config.WithWorkspaceReadAcross("Aurora", null).WorkspaceReadAcross);
    }

    [Fact]
    public void A_relationship_is_declared_once_in_any_case_and_the_last_one_cleared_leaves_no_entry()
    {
        var config = DriverConfig.Empty
            .WithWriteAcross("plugins", "engine", allow: true)
            .WithWriteAcross("Plugins", "ENGINE", allow: true)
            .WithWriteAcross("plugins", "game", allow: true);

        Assert.Equal(["engine", "game"], config.WriteAcross["plugins"]);
        var fewer = config.WithWriteAcross("plugins", "Engine", allow: false);
        Assert.Equal(["game"], fewer.WriteAcross["plugins"]);
        Assert.Empty(fewer.WithWriteAcross("plugins", "game", allow: false).WriteAcross);
        // Clearing what was never declared changes nothing.
        Assert.Equal(config.WriteAcross["plugins"], config.WithWriteAcross("plugins", "tools", allow: false).WriteAcross["plugins"]);
    }

    /// <summary>The same sentences as the CLI's, for the same two mistakes.</summary>
    [Fact]
    public void A_repository_writing_into_itself_or_into_no_name_is_refused_in_a_sentence()
    {
        var self = Assert.Throws<DriverException>(() => DriverConfig.Empty.WithWriteAcross("plugins", "Plugins", allow: true));
        Assert.Equal("`plugins` writes in its own tree already — a relationship names another repository.", self.Message);
        var nothing = Assert.Throws<DriverException>(() => DriverConfig.Empty.WithWriteAcross("plugins", " ", allow: true));
        Assert.Equal("a relationship names the repository it may write into.", nothing.Message);
    }

    /// <summary>
    /// CASEFOLD1d: a relationship's names compare by <c>OrdinalIgnoreCase</c>, each letter to its one capital, as the CLI's
    /// <c>writeAcrossProblem</c> and its reader compare them through <c>casefold.ts</c> (<c>driverconfig.test.ts</c> names this
    /// behaviour): a name full case mapping would lower to the same letters is another repository, so it is neither refused
    /// as the repository's own, nor read as a repeat, nor cleared with the other.
    /// </summary>
    [Fact]
    public void A_relationship_names_another_repository_only_as_OrdinalIgnoreCase_parts_them()
    {
        var dotted = $"i{(char)0x0307}zmir";
        Assert.Null(AcrossRules.Problem("İzmir", dotted));
        Assert.Null(AcrossRules.Problem("straße", "STRASSE"));

        var config = DriverConfig.Parse($$"""{ "writeAcross": { "plugins": ["İzmir", "{{dotted}}", "straße", "STRASSE"] } }""");
        Assert.Equal(["İzmir", dotted, "straße", "STRASSE"], config.WriteAcross["plugins"]);

        Assert.Equal(["İzmir", "straße", "STRASSE"], config.WithWriteAcross("plugins", dotted, allow: false).WriteAcross["plugins"]);
    }

    [Fact]
    public void The_setting_survives_every_other_edit_of_the_file()
    {
        var config = DriverConfig.Empty
            .WithReadAcross("engine", false)
            .WithWorkspaceReadAcross("aurora", false)
            .WithWriteAcross("plugins", "engine", allow: true)
            .WithDrivable("engine", true)
            .WithLine("engine", "develop")
            .WithNotify(false);

        var again = DriverConfig.Parse(config.ToJson());
        Assert.False(again.ReadAcross["engine"]);
        Assert.False(again.WorkspaceReadAcross["aurora"]);
        Assert.Equal(["engine"], again.WriteAcross["plugins"]);
    }

    // ——— Precedence (the driver's alone).

    [Fact]
    public void The_repositorys_reading_wins_then_its_workspaces_then_on()
    {
        var config = DriverConfig.Empty
            .WithWorkspaceReadAcross("aurora", false)
            .WithReadAcross("engine", true)
            .WithReadAcross("secret", false);

        Assert.Equal(new AcrossReading(true, AcrossSource.Repository), AcrossRules.Reading(config, "engine", "aurora"));
        Assert.Equal(new AcrossReading(false, AcrossSource.Workspace), AcrossRules.Reading(config, "game", "aurora"));
        Assert.Equal(new AcrossReading(false, AcrossSource.Repository), AcrossRules.Reading(config, "secret", "forge"));
        Assert.Equal(new AcrossReading(true, AcrossSource.Default), AcrossRules.Reading(config, "tools", "forge"));
        // A repository in no workspace is in `default`'s, as its line and its landing are.
        Assert.Equal(AcrossSource.Workspace,
            AcrossRules.Reading(DriverConfig.Empty.WithWorkspaceReadAcross("default", false), "tools", null).Source);
    }

    // ——— What a session may reach.

    private static readonly RepoView[] Registry =
    [
        new("app", Adopted: true, Root: "/work/app", Workspace: "aurora"),
        new("engine", Adopted: true, Root: "/work/engine", Workspace: "aurora"),
        new("plugins", Adopted: true, Root: "/work/plugins", Workspace: "aurora"),
        new("secret", Adopted: true, Root: "/work/secret", Workspace: "aurora"),
        new("remote-only", Adopted: true, Root: null, Workspace: "aurora"),
        new("personal", Adopted: true, Root: "/srv/personal", Workspace: "home"),
    ];

    [Fact]
    public void By_default_a_session_reads_its_workspaces_other_checkouts_and_writes_none()
    {
        var reach = AcrossRules.Reach(DriverConfig.Empty, Registry, "app", "aurora");

        Assert.Equal(["engine", "plugins", "secret"], reach.Reads.Select(c => c.Repository));
        Assert.Equal("/work/engine", reach.Reads[0].Path);
        Assert.Empty(reach.Writes);
        // Another workspace's checkout is not read across (D48); one with no checkout here is nowhere.
        Assert.Equal(["personal"], reach.Unread.Select(c => c.Repository));
    }

    [Fact]
    public void A_checkout_switched_off_is_read_by_no_other_session_and_a_declared_target_is_written_and_read()
    {
        var config = DriverConfig.Empty
            .WithReadAcross("secret", false)
            .WithReadAcross("engine", false)
            .WithWriteAcross("plugins", "engine", allow: true);

        var fromPlugins = AcrossRules.Reach(config, Registry, "plugins", "aurora");
        Assert.Equal(["app", "engine"], fromPlugins.Reads.Select(c => c.Repository));
        Assert.Equal(["engine"], fromPlugins.Writes.Select(c => c.Repository));
        Assert.Equal(["personal", "secret"], fromPlugins.Unread.Select(c => c.Repository).Order(StringComparer.Ordinal));

        // The relationship has a direction: `engine`'s sessions write nothing into `plugins`.
        var fromEngine = AcrossRules.Reach(config, Registry, "engine", "aurora");
        Assert.Empty(fromEngine.Writes);
        Assert.Equal(["app", "plugins"], fromEngine.Reads.Select(c => c.Repository));
    }

    [Fact]
    public void A_relationship_into_another_workspace_or_a_repository_with_no_checkout_here_reaches_nothing()
    {
        var config = DriverConfig.Empty
            .WithWriteAcross("app", "personal", allow: true)
            .WithWriteAcross("app", "remote-only", allow: true);

        var reach = AcrossRules.Reach(config, Registry, "app", "aurora");

        Assert.Empty(reach.Writes);
        Assert.Contains("personal", reach.Unread.Select(c => c.Repository));
    }

    [Fact]
    public void Ask_Daoris_reads_every_readable_checkout_in_every_workspace()
    {
        var config = DriverConfig.Empty.WithReadAcross("secret", false).WithWorkspaceReadAcross("home", false);

        Assert.Equal(["app", "engine", "plugins"], AcrossRules.Readable(config, Registry).Select(c => c.Repository));
        Assert.Equal(
            ["app", "engine", "personal", "plugins", "secret"],
            AcrossRules.Readable(DriverConfig.Empty, Registry).Select(c => c.Repository));
    }

    // ——— The rules a session is handed.

    [Fact]
    public void A_readable_checkout_is_a_read_and_two_read_only_git_commands_and_an_edit_refused()
    {
        var reach = new AcrossReach([new("engine", "/work/engine")], [], []);

        var rules = AcrossRules.Rules(reach, own: ["/work/app"]);

        Assert.Equal(
            ["Read(//work/engine/**)", "Bash(git -C /work/engine status:*)", "Bash(git -C /work/engine branch --list:*)"],
            rules.Allow);
        Assert.Empty(rules.Ask);
        Assert.Equal(["Edit(//work/engine/**)"], rules.Deny);
    }

    [Fact]
    public void A_declared_target_is_also_an_edit_and_a_commit_there_and_is_refused_nothing()
    {
        var engine = new AcrossCheckout("engine", "C:\\work\\engine");
        var reach = new AcrossReach([engine], [engine], []);

        var rules = AcrossRules.Rules(reach, own: ["C:\\work\\plugins"]);

        Assert.Equal(
            [
                "Read(//c/work/engine/**)", "Bash(git -C C:/work/engine status:*)", "Bash(git -C C:/work/engine branch --list:*)",
                "Edit(//c/work/engine/**)", "Bash(git -C C:/work/engine add:*)", "Bash(git -C C:/work/engine commit:*)",
            ],
            rules.Allow);
        Assert.Empty(rules.Deny);
    }

    [Fact]
    public void A_checkout_it_may_not_read_is_refused_both_the_read_and_the_edit()
    {
        var reach = new AcrossReach([], [], [new("secret", "/work/secret")]);

        var rules = AcrossRules.Rules(reach, own: ["/work/app"]);

        Assert.Empty(rules.Allow);
        Assert.Equal(["Edit(//work/secret/**)", "Read(//work/secret/**)"], rules.Deny);
    }

    /// <summary>
    /// 🔴 Deny beats allow, so a refusal on a checkout that holds the session's own tree, its kept files, or
    /// a checkout it may use would refuse the session exactly what it was given.
    /// </summary>
    [Fact]
    public void No_refusal_lands_on_a_checkout_that_holds_the_sessions_own_or_what_it_may_use()
    {
        var reach = new AcrossReach(
            Reads: [new("outer", "/work"), new("plugins", "/work/app/plugins")],
            Writes: [new("plugins", "/work/app/plugins")],
            Unread: [new("everything", "/"), new("mono", "/work/mono")]);

        var rules = AcrossRules.Rules(reach, own: ["/work/app", "/data/quests/q1/attachments"]);

        // `outer` and `everything` hold the session's own tree, so neither is refused; `mono` is refused whole.
        Assert.Equal(["Edit(//work/mono/**)", "Read(//work/mono/**)"], rules.Deny);
        Assert.Contains("Read(//work/**)", rules.Allow);
        Assert.Contains("Edit(//work/app/plugins/**)", rules.Allow);
    }

    [Fact]
    public void Nothing_across_is_no_rule_at_all()
    {
        Assert.True(AcrossRules.Rules(AcrossReach.None, own: ["/work/app"]).IsEmpty);
    }

    // ——— The instruction (every harness gets it, where only Claude Code takes the rules).

    private static SessionTarget Target() => new(
        QuestId: "abc123", Title: "Add the note field", Body: "The report needs a note column.", Asker: "ask #9f45",
        Repository: "plugins", Root: "C:/work/plugins", ServiceUrl: "http://localhost:5177");

    [Fact]
    public void With_nothing_across_the_instruction_reads_as_it_always_has()
    {
        var prompt = TargetPrompt.Compose(Target());

        Assert.Contains("do not read into it and do not guess: ask it.", prompt);
        Assert.Contains("Never write outside this repository. Work another repository needs", prompt);
        Assert.DoesNotContain("other repositories' checkouts", prompt);
    }

    [Fact]
    public void A_session_that_may_read_across_is_told_which_checkouts_where_and_how_and_that_a_change_is_still_asked()
    {
        var prompt = TargetPrompt.Compose(Target() with
        {
            ReadsAcross = [new("app", "C:\\work\\app"), new("docs", "D:\\my work\\docs")],
        });

        Assert.Contains("You may read these other repositories' checkouts on this machine, and change nothing in them:", prompt);
        Assert.Contains("- `app` — `C:/work/app`", prompt);
        Assert.Contains("- `docs` — `\"D:/my work/docs\"`", prompt);
        Assert.Contains("`git -C <path> status`", prompt);
        Assert.Contains("`git -C <path> branch --list`", prompt);
        // Reading is allowed, so the instruction no longer forbids it; a change and the why are still asked.
        Assert.DoesNotContain("do not read into it", prompt);
        Assert.Contains("do not guess: ask it.", prompt);
        Assert.Contains("respond to `#abc123` with `wait`", prompt);
        Assert.Contains("Never write outside this repository. Work another repository needs", prompt);
        // Said as the canon speaks — it travels to repositories that know nothing of this numbering.
        Assert.DoesNotContain("D107", prompt);
    }

    [Fact]
    public void A_declared_target_is_the_one_exception_the_boundary_names_with_how_to_commit_there()
    {
        var engine = new AcrossCheckout("engine", "C:\\work\\engine");
        var prompt = TargetPrompt.Compose(Target() with { ReadsAcross = [engine], WritesAcross = [engine] });

        Assert.Contains("Never write outside this repository, except in these, which the person has declared this one may change:", prompt);
        Assert.Contains("- `engine` — `C:/work/engine`", prompt);
        Assert.Contains("`git -C <path> add` and `git -C <path> commit`", prompt);
        Assert.Contains("Any other work another repository needs is a quest published to it, never an edit", prompt);
        // A target is read as it is written, so it is not listed twice.
        Assert.DoesNotContain("You may read these other repositories' checkouts", prompt);
    }

    [Fact]
    public void A_resumed_and_a_carried_on_session_are_told_the_same()
    {
        var across = Target() with { ReadsAcross = [new("app", "C:/work/app")], WritesAcross = [new("engine", "C:/work/engine")] };
        var resumed = TargetPrompt.Compose(across with
        {
            Answered = new QuestView("q2", "plugins", "app", "What does it take?", "", "Done") { Note = "A string." },
        });
        var carried = TargetPrompt.Compose(across with { CutOff = "it timed out." });

        foreach (var prompt in new[] { resumed, carried })
        {
            Assert.Contains("- `app` — `C:/work/app`", prompt);
            Assert.Contains("except in these, which the person has declared this one may change:", prompt);
        }
    }

    [Theory]
    [InlineData("C:\\work\\engine", "C:/work/engine")]
    [InlineData("C:\\work\\engine\\", "C:/work/engine")]
    [InlineData("/srv/engine", "/srv/engine")]
    [InlineData("D:\\my work\\engine", "\"D:/my work/engine\"")]
    [InlineData("/srv/a&b", "\"/srv/a&b\"")]
    [InlineData("D:\\工作\\engine", "D:/工作/engine")]
    [InlineData("/srv/v1.2_x-y+z@home,=~%", "/srv/v1.2_x-y+z@home,=~%")]
    public void A_checkouts_path_is_written_for_git_with_forward_slashes_and_quoted_where_a_shell_would_split_it(string path, string written) =>
        Assert.Equal(written, AcrossRules.GitPath(path));

    [Theory]
    [InlineData("/work", "/work/app", true)]
    [InlineData("/work/app", "/work/app", true)]
    [InlineData("/work/app", "/work/app-old", false)]
    [InlineData("/work/app/plugins", "/work/app", false)]
    [InlineData("C:\\Work\\App", "c:/work/app/src", true)]
    [InlineData("C:\\work", "D:\\work", false)]
    [InlineData("/", "/work/app", true)]
    public void Whether_one_path_holds_another(string outer, string inner, bool holds) =>
        Assert.Equal(holds, AcrossRules.Holds(outer, inner, windows: outer.Contains(':')));

    [Fact]
    public void A_posix_path_is_held_only_in_its_own_case()
    {
        Assert.False(AcrossRules.Holds("/srv/Engine", "/srv/engine/a", windows: false));
        Assert.True(AcrossRules.Holds("/srv/Engine", "/srv/engine/a", windows: true));
    }
}
