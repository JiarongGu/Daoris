using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A cool-off scoped to a model: an account at a model family's weekly limit cools as account plus
/// model, so a start asking that family is passed and a start asking another runs; an account-wide limit still cools every start.
/// </summary>
/// <remarks>Nothing here starts a process: the roster's harness is a PATH look for a command no machine has.</remarks>
public sealed class ModelCoolingTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");
    private static readonly DateTimeOffset Seen = new(2026, 10, 1, 14, 0, 0, TimeSpan.FromMinutes(345));
    private static readonly DateTimeOffset Until = new(2026, 10, 3, 16, 2, 0, TimeSpan.FromMinutes(345));
    private const string Nowhere = "daoris-modelrole1k-no-such-command";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-model-cooling-" + Guid.NewGuid().ToString("N")[..8]);
    private DateTimeOffset _now = Seen;

    public ModelCoolingTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Settings => DaorisHome.Entry(_home, HomeEntry.Harnesses);

    private static CoolingEntry Entry(string? model = "opus", DateTimeOffset? until = null, string? window = "weekly") =>
        new("fake", "account-1", until ?? Until, true, window, Seen, "3f9c2a71", Model: model);

    private static TestAgent Present() => new(
        Binary: [Nowhere], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true,
        Limits: ClaudeLimits.Words, Windows: ClaudeWindows.Words, RunsInOwnHome: true);

    private HarnessRoster Roster()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", "account-1"));
        new HarnessSettings().WithDefault("fake", "account-1").Save(Settings);
        // The families are the agent's own: the models its open listed.
        AgentOffers.Offered(_home, "fake", "1", "account-1", ["opus", "sonnet", "claude-opus-4-5", "claude-sonnet-4-5"], null, _now);
        return new(TestDoors.Registry(TestDoors.Door("fake", Present())), Settings) { Clock = () => _now, Zone = Zone };
    }

    private static DriverConfig Config => DriverConfig.Empty with { Actors = StaffedRoles.On("fake") };

    private JsonObject File() => JsonNode.Parse(System.IO.File.ReadAllText(AccountCooling.PathOf(_home)))!.AsObject();

    // ——— The walk.

    [Fact]
    public async Task An_account_at_its_opus_week_is_passed_for_an_opus_start_and_taken_for_a_sonnet_start()
    {
        var roster = Roster();
        AccountCooling.Cool(_home, Entry(), _now);

        var opus = await roster.SelectAsync("fake", Config, null, null, model: "opus");
        var named = await roster.SelectAsync("fake", Config, null, null, model: "claude-opus-4-5");
        var sonnet = await roster.SelectAsync("fake", Config, null, null, model: "sonnet");

        Assert.Equal(Entry(), opus.Cooling);
        Assert.Equal(Entry(), named.Cooling);
        // Taken: past the cool-off, the probe finds nothing on this PATH.
        Assert.Null(sonnet.Cooling);
        Assert.Contains("is not installed on this machine", sonnet.Refusal);
    }

    [Fact]
    public async Task A_start_at_account_default_is_held_by_a_model_s_cool_off_since_the_model_it_runs_is_the_agent_s()
    {
        var roster = Roster();
        AccountCooling.Cool(_home, Entry(), _now);

        Assert.Equal(Entry(), (await roster.SelectAsync("fake", Config, null, null)).Cooling);
    }

    [Fact]
    public async Task An_account_wide_limit_cools_both()
    {
        var roster = Roster();
        AccountCooling.Cool(_home, Entry(model: null), _now);

        Assert.Equal(Entry(model: null), (await roster.SelectAsync("fake", Config, null, null, model: "opus")).Cooling);
        Assert.Equal(Entry(model: null), (await roster.SelectAsync("fake", Config, null, null, model: "sonnet")).Cooling);
    }

    [Fact]
    public async Task A_family_the_agent_offers_cools_by_its_own_scope_though_no_list_of_Daoris_named_it()
    {
        var roster = Roster();
        AgentOffers.Offered(_home, "fake", "1", "account-2", ["claude-haiku-5-5", "claude-fable-5-2", "claude-sonnet-5-5"], null, _now);
        var reset = Seen.AddDays(3);
        roster.History("fake", "account-1", "fake", [Week("seven_day_fable", "fable", 1.0, reset)], ClaudeUsage.Source, ClaudeUsage.HistoryReader, _now, session: null);

        Assert.NotNull((await roster.SelectAsync("fake", Config, null, null, model: "claude-fable-5-2")).Cooling);
        Assert.Null((await roster.SelectAsync("fake", Config, null, null, model: "claude-haiku-5-5")).Cooling);

        var limited = roster.Limited("fake", "account-1", $"You've hit your Haiku limit · resets Oct 3, 4pm ({Zone.Id})", "s1");
        Assert.Equal("haiku", limited!.Value.Entry.Model);
    }

    [Fact]
    public async Task A_model_s_cool_off_ends_at_its_window_s_reset()
    {
        var roster = Roster();
        var reset = Seen.AddDays(3);
        roster.History("fake", "account-1", "fake", [Week("seven_day_opus", "opus", 1.0, reset)], ClaudeUsage.Source, ClaudeUsage.HistoryReader, _now, session: null);

        Assert.NotNull((await roster.SelectAsync("fake", Config, null, null, model: "opus")).Cooling);

        _now = reset.AddSeconds(1);
        Assert.Null((await roster.SelectAsync("fake", Config, null, null, model: "opus")).Cooling);
    }

    [Fact]
    public async Task The_walk_s_sentence_names_the_model_s_limit()
    {
        var roster = Roster();
        AccountCooling.Cool(_home, Entry(), _now);

        var held = await roster.SelectAsync("fake", Config, null, null, model: "opus");

        Assert.Equal(
            $"the `fake` account `account-1` reached its `opus` limit and is cooling for `opus` until Oct 3, 16:02 ({Zone.Id}), as the "
            + "agent said. Daoris starts nothing on it that asks for `opus` until then.",
            held.Refusal);
        Assert.Equal(held.Refusal, held.RefusalNote!.Note);
        Assert.Equal("held.cooling-model", held.RefusalNote.Parts[0].Code);
        Assert.Equal(("opus", "account-1", "fake"), (held.RefusalNote.Parts[0].Value("model"), held.RefusalNote.Parts[0].Value("account"), held.RefusalNote.Parts[0].Value("owner")));
    }

    [Fact]
    public void A_record_s_and_a_conversation_s_note_say_the_family_is_cooling_not_the_whole_account_and_by_a_code_of_its_own()
    {
        var note = CoolingWords.NoteOf(Entry(), Zone);
        var conversation = CoolingWords.ConversationOf(Entry(), Zone);
        var when = $"Oct 3, 16:02 ({Zone.Id}), as the agent said";

        Assert.Equal(
            $"The `fake` account it ran on reached its `opus` limit and is cooling for `opus` until {when}, and nothing that asks for `opus` starts on it until then.",
            note.Note);
        Assert.Equal(
            $"The `fake` account this conversation runs on reached its `opus` limit and is cooling for `opus` until {when}, and nothing new that asks for `opus` starts on it until then.",
            conversation.Note);
        Assert.Equal(note.Note, CoolingWords.Note(Entry(), Zone));
        Assert.Equal(conversation.Note, CoolingWords.Conversation(Entry(), Zone));
        foreach (var line in new[] { note, conversation })
        {
            Assert.Equal("account.cooling-model", line.Parts[0].Code);
            Assert.Equal(["until", "why", "owner", "model"], line.Parts[0].Values.Select(value => value.Key));
            Assert.Equal(("fake", "opus"), ((string?)line.Parts[0].Value("owner"), (string?)line.Parts[0].Value("model")));
            Assert.DoesNotContain("account-1", line.Note);
        }

        // An account-wide limit keeps its sentences and codes.
        Assert.Equal("account.cooling-window", CoolingWords.NoteOf(Entry(model: null), Zone).Parts[0].Code);
        Assert.Contains("nothing starts on it", CoolingWords.Note(Entry(model: null), Zone));
    }

    [Fact]
    public void The_tool_s_own_sign_in_cooling_for_a_family_holds_only_that_family_and_says_so_by_its_own_code()
    {
        var held = CoolingWords.HoldOf(Entry() with { Account = null }, Zone);

        Assert.Equal("held.cooling-model-own", held.Parts[0].Code);
        Assert.Equal(["owner", "model", "until", "why"], held.Parts[0].Values.Select(value => value.Key));
        Assert.StartsWith(
            $"`fake`'s own sign-in reached its `opus` limit and is cooling for `opus` until Oct 3, 16:02 ({Zone.Id}), as the agent said. "
            + "Daoris starts nothing on it that asks for `opus` until then.",
            held.Note);
        Assert.Equal("waits.read-again", held.Parts[^1].Code);
    }

    // ——— What cools a model.

    [Fact]
    public void A_reading_at_a_model_family_s_week_cools_that_model_until_the_week_s_reset()
    {
        var roster = Roster();
        var reset = Seen.AddDays(3);
        roster.History(
            "fake", "account-1", "fake",
            [
                Week("seven_day_opus", "opus", 1.0, reset),
                Week("seven_day_sonnet", "sonnet", 0.4, reset),
                // A scope that is no model family Claude's windows declare cools nothing.
                Week("seven_day_oauth_apps", "oauth_apps", 1.0, reset),
            ],
            ClaudeUsage.Source, ClaudeUsage.HistoryReader, _now, session: null);

        var cooling = Assert.Single(AccountCooling.Read(_home, _now));
        Assert.Equal(("fake", "account-1", "opus", reset, true, "weekly"), (cooling.Agent, cooling.Account, cooling.Model, cooling.Until, cooling.Stated, cooling.Window));
    }

    [Fact]
    public void A_limit_that_names_a_model_family_cools_that_model_and_one_that_names_none_the_account()
    {
        var roster = Roster();

        var opus = roster.Limited("fake", "account-1", $"You've hit your Opus limit · resets Oct 3, 4pm ({Zone.Id})", "s1");
        Assert.Equal(("opus", Until), (opus!.Value.Entry.Model, opus.Value.Entry.Until));

        var weekly = roster.Limited("fake", "account-1", $"You've hit your weekly limit · resets Oct 3, 4pm ({Zone.Id})", "s2");
        Assert.Null(weekly!.Value.Entry.Model);
        Assert.Equal(2, AccountCooling.Read(_home, _now).Count);
    }

    // ——— The file.

    [Fact]
    public void An_account_s_whole_cool_off_and_its_model_s_are_kept_side_by_side_and_a_try_now_ends_both()
    {
        AccountCooling.Cool(_home, Entry(), _now);
        AccountCooling.Cool(_home, Entry(model: null, until: Until.AddDays(-1), window: "session"), _now);

        var account = File()["fake"]!["account-1"]!.AsObject();
        Assert.Equal("2026-10-02T10:17:00Z", account["until"]!.GetValue<string>());
        Assert.Equal("2026-10-03T10:17:00Z", account["models"]!["opus"]!["until"]!.GetValue<string>());
        Assert.Equal(2, AccountCooling.Read(_home, _now).Count);
        // The later of the two holds an Opus start; a Sonnet start meets only the account's own.
        Assert.Equal(Entry(), AccountCooling.Of(_home, "fake", "account-1", _now, "opus"));
        Assert.Equal("session", AccountCooling.Of(_home, "fake", "account-1", _now, "sonnet")!.Window);

        Assert.True(AccountCooling.End(_home, "fake", "account-1", _now));
        Assert.Empty(AccountCooling.Read(_home, _now));
    }

    [Fact]
    public void A_passed_whole_cool_off_is_dropped_and_its_model_s_kept()
    {
        AccountCooling.Cool(_home, Entry(), _now);
        AccountCooling.Cool(_home, Entry(model: null, until: Seen.AddHours(1), window: "session"), _now);

        _now = Seen.AddHours(2);
        AccountCooling.Cool(_home, new CoolingEntry("codex", null, Until, false, null, _now, null), _now);

        var account = File()["fake"]!["account-1"]!.AsObject();
        Assert.Equal(["models"], account.Select(field => field.Key));
        Assert.Equal(Entry(), Assert.Single(AccountCooling.Read(_home, _now), entry => entry.Agent == "fake"));
    }

    // ——— A model's family.

    [Theory]
    [InlineData("opus", "opus", true)]
    [InlineData("claude-opus-4-5", "opus", true)]
    [InlineData("Opus[1m]", "opus", true)]
    [InlineData("sonnet", "opus", false)]
    [InlineData("claude-sonnet-4-5", "sonnet", true)]
    [InlineData("opusplan", "opus", false)]
    public void A_model_names_a_family_as_a_word_of_its_id(string model, string family, bool names) =>
        Assert.Equal(names, ModelLimits.Names(model, family));

    private static WindowSeen Week(string key, string scope, double used, DateTimeOffset reset) =>
        new(key, null, scope, 10080, used, 0.01, reset, UsageHistory.ResetSaid);
}
