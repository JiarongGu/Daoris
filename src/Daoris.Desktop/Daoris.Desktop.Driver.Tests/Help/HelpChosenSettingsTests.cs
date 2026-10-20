using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HELPMODE1c3 (D183's note; the Ask Daoris modes design §4.1): Ask Daoris's chosen settings, the five ordinary ones. The person's
/// record of what is chosen, beside housekeeping's; each start composing it again onto the allow-list; and the driver's judgement of
/// each tool's request at every call, as housekeeping's is: the record is Ask Daoris's, the setting is chosen now and was handed at
/// this start, and what it names is the conversation's workspace's. Then the setting's own checks, the proposal's, and the writer
/// every door uses: the driver's file, edited where it is, so what it holds besides stays as written.
/// </summary>
public sealed class HelpChosenSettingsTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-chosen-settings-" + Guid.NewGuid().ToString("N")[..8]);

    public HelpChosenSettingsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string ConfigPath => DriverConfig.ResolvePath(_home);

    private DriverConfig Config => DriverConfig.Load(ConfigPath);

    private void Choose(string json) => File.WriteAllText(ConfiguredAccess.PathOf(_home), json);

    /// <summary>What this start was handed, as its receipt keeps it.</summary>
    private void Handed(string session, params string[] settings) =>
        new HelpWired(_home).Keep(session, new HelpReceipt
        {
            At = DateTimeOffset.UtcNow, Harness = "codex-acp", Folder = HelpRoom.PathOf(_home, "aurora"), Settings = settings,
        });

    /// <summary>Ask Daoris's <c>h1</c> in workspace <c>aurora</c>, a driven <c>s1</c>; <c>engine</c> is aurora's, <c>rival</c> borealis's.</summary>
    private static HousekeepingLedger Ledger() => new()
    {
        Records = [("h1", HelpRoom.Repository, "aurora", null), ("s1", "engine", "aurora", null)],
        Registry = [("engine", "aurora", "X:/daoris/engine"), ("rival", "borealis", "X:/daoris/rival")],
    };

    private static SessionRequest Request(string setting, string value, string? repository = null, string? workspace = null) =>
        new("h1", SessionMove.Set, DateTimeOffset.UtcNow)
        {
            By = RequestDoor.Help, Key = "0123456789ab", Operation = setting, Value = value, Repository = repository, Workspace = workspace,
        };

    private Task<SettingAnswer> AnswerAsync(HousekeepingLedger ledger, SessionRequest request) =>
        new HelpSettings(_home, ledger.Client).AnswerAsync(request, CancellationToken.None);

    /// <summary>
    /// The record keeps each grant apart: the settings chosen read in the grant's order, once each, whatever housekeeping holds,
    /// and a code no setting of the five (the mode, a grant) grants nothing.
    /// </summary>
    [Fact]
    public void The_record_reads_the_settings_chosen_apart_from_housekeeping()
    {
        Assert.Empty(ConfiguredAccess.Read(_home).Settings);

        Choose("""{ "settings": ["cap", "notify", "mode", "cap", 3] }""");
        var read = ConfiguredAccess.Read(_home);
        Assert.Equal([ConfiguredAccess.Notify, ConfiguredAccess.Cap], read.Settings);
        Assert.Empty(read.Housekeeping);
        Assert.Equal(["settings_notify", "settings_cap"], read.SettingsTools);

        Choose("""{ "housekeeping": "inspect", "settings": ["timeout"] }""");
        Assert.Equal([ConfiguredAccess.Timeout], ConfiguredAccess.Read(_home).Settings);
    }

    /// <summary>A choice of one setting edits the settings' list alone, the housekeeping chosen kept as written; one not of the five is refused.</summary>
    [Fact]
    public void A_choice_edits_the_settings_alone_and_refuses_another()
    {
        Choose("""{ "housekeeping": ["inspect"] }""");

        ConfiguredAccess.Choose(_home, ConfiguredAccess.SettingsGrant, ConfiguredAccess.CoolOff, true);
        var chosen = ConfiguredAccess.Choose(_home, ConfiguredAccess.SettingsGrant, ConfiguredAccess.Language, true);

        Assert.Equal([ConfiguredAccess.Language, ConfiguredAccess.CoolOff], chosen.Settings);
        Assert.Equal([ConfiguredAccess.Inspect], chosen.Housekeeping);
        using var file = JsonDocument.Parse(File.ReadAllText(ConfiguredAccess.PathOf(_home)));
        Assert.Equal(["language", "cooloff"], file.RootElement.GetProperty("settings").EnumerateArray().Select(each => each.GetString()));
        Assert.Contains("`mode`", Assert.Throws<DriverException>(() => ConfiguredAccess.Choose(_home, "settings", "mode", true)).Message);
        Assert.Contains("not built", Assert.Throws<DriverException>(() => ConfiguredAccess.Choose(_home, "checkouts", "read", true)).Message);
    }

    /// <summary>
    /// 🔴 Each start composes the grant again (design §4.1's trap): the place a start is handed reads the record then, and its
    /// allow-list names the chosen settings' tools beside the room's own and no other.
    /// </summary>
    [Fact]
    public void Each_start_hands_the_settings_chosen_then_on_its_allow_list()
    {
        var file = PermissionRules.Load(_home);
        Assert.DoesNotContain(HelpRoom.Allowed, rule => rule.Contains("__settings_", StringComparison.Ordinal));
        Assert.DoesNotContain(ChatRunner.HelpPlaceIn(_home, HelpRoom.PathOf(_home), []).Rules(file, "h1").Allow,
            rule => rule.Contains("__settings_", StringComparison.Ordinal));

        Choose("""{ "settings": ["timeout", "notify"] }""");
        var place = ChatRunner.HelpPlaceIn(_home, HelpRoom.PathOf(_home), []);

        Assert.Equal([ConfiguredAccess.Notify, ConfiguredAccess.Timeout], place.Settings);
        var allow = place.Rules(file, "h1").Allow;
        Assert.Contains($"mcp__{KnowledgeConnector.ServerName}__settings_notify", allow);
        Assert.Contains($"mcp__{KnowledgeConnector.ServerName}__settings_timeout", allow);
        Assert.DoesNotContain($"mcp__{KnowledgeConnector.ServerName}__settings_cap", allow);
    }

    /// <summary>
    /// 🔴 A grant off refuses each setting, whatever the agent (Codex takes no allow-list, so the call's judgement is what holds it),
    /// and the driver's file is not touched.
    /// </summary>
    [Theory]
    [InlineData(ConfiguredAccess.Notify, "off", null)]
    [InlineData(ConfiguredAccess.Language, "zh", "engine")]
    [InlineData(ConfiguredAccess.Timeout, "45", null)]
    [InlineData(ConfiguredAccess.Cap, "3", null)]
    [InlineData(ConfiguredAccess.CoolOff, "90", null)]
    public async Task A_grant_off_refuses_each_setting_and_writes_nothing(string setting, string value, string? repository)
    {
        Handed("h1");

        var answer = await AnswerAsync(Ledger(), Request(setting, value, repository));

        Assert.Equal((setting, false, SettingAnswer.NotGranted), (answer.Setting, answer.Done, answer.Code));
        Assert.Contains("configured access", answer.Said);
        Assert.Contains("setting_propose", answer.Said);
        Assert.Contains("Nothing was changed", answer.Said);
        Assert.False(File.Exists(ConfigPath));
    }

    /// <summary>
    /// 🔴 Granted and handed, each setting is changed through its own door's edit on the driver's file, and the answer says what it
    /// is now, with the terminal command that does the same; the rest of the file stays as it was.
    /// </summary>
    [Fact]
    public async Task A_granted_setting_is_changed_through_its_own_doors_edit()
    {
        File.WriteAllText(ConfigPath, """{ "drivable": ["engine"], "notify": true }""");
        Choose("""{ "settings": ["notify", "language", "timeout", "cap", "cooloff"] }""");
        Handed("h1", "notify", "language", "timeout", "cap", "cooloff");
        var ledger = Ledger();

        var notify = await AnswerAsync(ledger, Request(ConfiguredAccess.Notify, "off"));
        Assert.Equal((true, (string?)null), (notify.Done, notify.Code));
        Assert.Contains("`daoris driver notify off`", notify.Said);
        Assert.True((await AnswerAsync(ledger, Request(ConfiguredAccess.Timeout, "45"))).Done);
        Assert.True((await AnswerAsync(ledger, Request(ConfiguredAccess.Cap, "3"))).Done);
        Assert.True((await AnswerAsync(ledger, Request(ConfiguredAccess.CoolOff, "90"))).Done);
        var language = await AnswerAsync(ledger, Request(ConfiguredAccess.Language, "zh", repository: "engine"));
        Assert.True(language.Done);
        Assert.True((await AnswerAsync(ledger, Request(ConfiguredAccess.Language, "en", workspace: "aurora"))).Done);

        var config = Config;
        Assert.False(config.Notify);
        Assert.Equal((45, 3, 90), (config.TimeoutMinutes, config.Cap, config.CoolOffMinutes));
        Assert.Equal("zh", config.Languages["engine"]);
        Assert.Equal("en", config.WorkspaceLanguages["aurora"]);
        Assert.Equal(["engine"], config.Drivable);

        Assert.True((await AnswerAsync(ledger, Request(ConfiguredAccess.Language, "clear", repository: "engine"))).Done);
        Assert.False(Config.Languages.ContainsKey("engine"));
    }

    /// <summary>
    /// A value the setting's own door refuses is refused in its words, and nothing is written: a timeout or a cap below one, a
    /// cool-off below a minute, notify that is neither on nor off, a language this build does not write in.
    /// </summary>
    [Theory]
    [InlineData(ConfiguredAccess.Timeout, "0", null)]
    [InlineData(ConfiguredAccess.Cap, "none", null)]
    [InlineData(ConfiguredAccess.CoolOff, "0", null)]
    [InlineData(ConfiguredAccess.Notify, "maybe", null)]
    [InlineData(ConfiguredAccess.Language, "fr", "engine")]
    public async Task A_value_its_own_door_refuses_is_refused_and_nothing_is_written(string setting, string value, string? repository)
    {
        Choose("""{ "settings": ["notify", "language", "timeout", "cap", "cooloff"] }""");
        Handed("h1", "notify", "language", "timeout", "cap", "cooloff");

        var answer = await AnswerAsync(Ledger(), Request(setting, value, repository));

        Assert.Equal((false, SettingAnswer.Refused), (answer.Done, answer.Code));
        Assert.Contains("Nothing was changed", answer.Said);
        Assert.False(File.Exists(ConfigPath));
    }

    /// <summary>
    /// 🔴 A revoke refuses the next call, since each call reads the record; and a grant chosen after this start began waits for its
    /// next turn, as its allow-list does (ACCESSSWAP1).
    /// </summary>
    [Fact]
    public async Task A_revoke_refuses_the_next_call_and_a_grant_since_takes_the_next_turn()
    {
        var ledger = Ledger();
        Choose("""{ "settings": ["cap"] }""");
        Handed("h1");
        Assert.Equal(SettingAnswer.NotHanded, (await AnswerAsync(ledger, Request(ConfiguredAccess.Cap, "2"))).Code);

        Handed("h1", ConfiguredAccess.Cap);
        Choose("""{ "settings": [] }""");
        Assert.Equal(SettingAnswer.NotGranted, (await AnswerAsync(ledger, Request(ConfiguredAccess.Cap, "2"))).Code);
        Assert.False(File.Exists(ConfigPath));
    }

    /// <summary>
    /// 🔴 A request for a session that is not an Ask Daoris conversation is refused (a driven session shares the connector), and so
    /// is a language for a repository or a workspace outside the conversation's, read from its record and never from the request.
    /// </summary>
    [Fact]
    public async Task Another_session_and_a_language_outside_the_workspace_are_refused()
    {
        Choose("""{ "settings": ["language", "notify"] }""");
        Handed("h1", ConfiguredAccess.Language, ConfiguredAccess.Notify);
        Handed("s1", ConfiguredAccess.Notify);
        var ledger = Ledger();

        var driven = await AnswerAsync(ledger, Request(ConfiguredAccess.Notify, "on") with { Session = "s1" });
        Assert.Equal(SettingAnswer.NotHelp, driven.Code);
        Assert.Contains("`s1`", driven.Said);

        var rival = await AnswerAsync(ledger, Request(ConfiguredAccess.Language, "zh", repository: "rival"));
        Assert.Equal(SettingAnswer.Outside, rival.Code);
        Assert.Contains("`aurora`", rival.Said);
        var borealis = await AnswerAsync(ledger, Request(ConfiguredAccess.Language, "zh", workspace: "borealis"));
        Assert.Equal(SettingAnswer.Outside, borealis.Code);
        Assert.False(File.Exists(ConfigPath));
    }
}
