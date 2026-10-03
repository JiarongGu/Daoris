using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>languages</c> and <c>workspaceLanguages</c> in <c>driver.json</c> (LANG1c, D142 point 7, the language design §7): the
/// language a session is asked to write to the person in, set for a repository or a workspace, the repository's winning,
/// neither set none, from a closed table. The driver's half of a TWIN with the CLI's <c>driverconfig.ts</c>, whose
/// <c>driverconfig.test.ts</c> holds the same two tables and parses these theories to hold them to its own, cell for cell.
/// </summary>
/// <remarks>🔴 <b>Keep each row on one line, its cells literals</b>: the CLI's test reads them.</remarks>
public sealed class SessionLanguageTests
{
    /// <summary>The closed table: each code, and the name the line gives the agent, in the order both sides list them.</summary>
    [Theory]
    [InlineData("en", "English")]
    [InlineData("zh", "Simplified Chinese (简体中文)")]
    public void The_table_names_each_language_for_the_agent(string code, string name)
    {
        Assert.Equal(name, SessionLanguages.NameOf(code));
        Assert.Contains((code, name), SessionLanguages.Table);
    }

    [Fact]
    public void The_table_holds_exactly_its_rows()
    {
        Assert.Equal(["en", "zh"], SessionLanguages.Table.Select(row => row.Code));
    }

    /// <summary>
    /// The resolution: the repository's own, else its workspace's (a repository in none is in <c>default</c>), else none; a
    /// code read in any case without the spaces around it, and only where the table holds it.
    /// </summary>
    [Theory]
    [InlineData("absent is none", "{}", "app", "work", null, null)]
    [InlineData("a repository's language is its own", """{"languages":{"app":"zh"}}""", "app", "work", "zh", "repository")]
    [InlineData("a workspace's language is each repository's there that sets none", """{"workspaceLanguages":{"work":"zh"}}""", "app", "work", "zh", "workspace")]
    [InlineData("the repository's wins over its workspace's", """{"languages":{"app":"en"},"workspaceLanguages":{"work":"zh"}}""", "app", "work", "en", "repository")]
    [InlineData("another workspace's is not this one's", """{"workspaceLanguages":{"home":"zh"}}""", "app", "work", null, null)]
    [InlineData("another repository's is not this one's", """{"languages":{"api":"zh"}}""", "app", "work", null, null)]
    [InlineData("a repository in no workspace takes the default one's", """{"workspaceLanguages":{"default":"zh"}}""", "app", null, "zh", "workspace")]
    [InlineData("a repository is matched in any case", """{"languages":{"App":"zh"}}""", "app", "work", "zh", "repository")]
    [InlineData("a workspace is matched in any case", """{"workspaceLanguages":{"Work":"zh"}}""", "app", "work", "zh", "workspace")]
    [InlineData("a code is read in any case, without the spaces around it", """{"languages":{"app":" ZH "}}""", "app", "work", "zh", "repository")]
    [InlineData("a code the table does not hold is not read, and the workspace's stands", """{"languages":{"app":"fr"},"workspaceLanguages":{"work":"zh"}}""", "app", "work", "zh", "workspace")]
    [InlineData("a value that is not text is not read", """{"languages":{"app":7}}""", "app", "work", null, null)]
    [InlineData("a repository written twice in any case is read where first written", """{"languages":{"app":"zh","APP":"en"}}""", "app", "work", "zh", "repository")]
    [InlineData("a list is not a map", """{"languages":["app"]}""", "app", "work", null, null)]
    [InlineData("null is absent", """{"languages":null,"workspaceLanguages":null}""", "app", "work", null, null)]
    public void Languages_read_as_the_cli_reads_them(string name, string file, string repository, string? workspace, string? code, string? source)
    {
        var read = SessionLanguages.Resolve(DriverConfig.Parse(file), repository, workspace);

        Assert.True(code == read?.Code, $"{name}: {read?.Code ?? "none"}");
        Assert.True(source == read?.Source, $"{name}: from {read?.Source ?? "nowhere"}");
    }

    [Fact]
    public void A_language_survives_the_file_written_only_when_set()
    {
        var kept = DriverConfig.Empty.WithLanguage("app", " ZH ").WithWorkspaceLanguage("work", "en");

        var read = DriverConfig.Parse(kept.ToJson());
        Assert.Equal("zh", read.Languages["APP"]);
        Assert.Equal("en", read.WorkspaceLanguages["Work"]);
        Assert.Contains("\"app\": \"zh\"", kept.ToJson(), StringComparison.Ordinal);
        Assert.DoesNotContain("anguages", DriverConfig.Empty.ToJson(), StringComparison.Ordinal);
    }

    /// <summary>One language per repository or workspace: a later one replaces the earlier under the spelling first written; null clears it.</summary>
    [Fact]
    public void A_later_language_replaces_the_earlier_and_null_clears_it()
    {
        var config = DriverConfig.Parse("""{"languages":{"App":"en","api":"zh"},"workspaceLanguages":{"Work":"en"}}""")
            .WithLanguage("app", "zh")
            .WithWorkspaceLanguage("work", "zh");

        Assert.Equal("zh", config.Languages["app"]);
        Assert.Equal(["App", "api"], config.Languages.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["Work"], config.WorkspaceLanguages.Keys);

        var cleared = config.WithLanguage("APP", null).WithWorkspaceLanguage("WORK", null);
        Assert.False(cleared.Languages.ContainsKey("app"));
        Assert.Equal("zh", cleared.Languages["api"]);
        Assert.Empty(cleared.WorkspaceLanguages);
        Assert.DoesNotContain("workspaceLanguages", cleared.ToJson(), StringComparison.Ordinal);
    }

    /// <summary>A door refuses a code the table does not hold, naming the table, and no repository or workspace.</summary>
    [Fact]
    public void A_door_refuses_a_code_the_table_does_not_hold_and_a_blank_name()
    {
        var french = Assert.Throws<DriverException>(() => DriverConfig.Empty.WithLanguage("app", "fr"));
        Assert.Equal("`fr` is not a language a session can be asked to write in here — one of `en`, `zh`.", french.Message);
        Assert.Equal(SessionLanguages.Refusal("fr"), Assert.Throws<DriverException>(() => DriverConfig.Empty.WithWorkspaceLanguage("work", "fr")).Message);
        Assert.Throws<DriverException>(() => DriverConfig.Empty.WithLanguage("app", "  "));
        Assert.Throws<DriverException>(() => DriverConfig.Empty.WithLanguage(" ", "zh"));
        Assert.Throws<DriverException>(() => DriverConfig.Empty.WithWorkspaceLanguage(" ", "zh"));
    }

    /// <summary>🔴 Each writer keeps the other's sections: an edit of anything else keeps the languages the CLI wrote.</summary>
    [Fact]
    public void Another_edit_keeps_the_languages()
    {
        var edited = DriverConfig.Parse("""{"languages":{"app":"zh"},"workspaceLanguages":{"work":"en"},"cooloff":45}""")
            .WithDrivable("app", true).WithStrikes(2).WithStanding("app", "dev only", DateTimeOffset.UnixEpoch);

        var read = DriverConfig.Parse(edited.ToJson());
        Assert.Equal("zh", read.Languages["app"]);
        Assert.Equal("en", read.WorkspaceLanguages["work"]);
    }

    /// <summary>An intake answers a workspace's ask in no repository, so it takes the workspace's alone.</summary>
    [Fact]
    public void An_intake_takes_its_workspaces_language()
    {
        var config = DriverConfig.Empty.WithLanguage("app", "en").WithWorkspaceLanguage("work", "zh");

        Assert.Equal(new SessionLanguage("zh", "Simplified Chinese (简体中文)", LanguageSource.Workspace), SessionLanguages.OfWorkspace(config, "Work"));
        Assert.Null(SessionLanguages.OfWorkspace(config, "home"));
        Assert.Null(SessionLanguages.OfWorkspace(config, null));
    }
}
