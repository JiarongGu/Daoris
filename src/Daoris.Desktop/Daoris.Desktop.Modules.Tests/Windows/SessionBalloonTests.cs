using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// What a balloon says behind the window (NOTIFY1d; NOTIFY2d, D196, the notification design §3.3, §9): each kind worded from the words
/// the page handed the shell, in the window's language, the event's facts filled in, and what a session wrote shown as written; with
/// no words handed, the English catalogue's. It lived in the application's notifier, which has no test project; here it is tested per kind.
/// </summary>
public sealed partial class SessionBalloonTests
{
    /// <summary>The words a page in <paramref name="language"/> hands the shell (<c>noticeWords</c> on the page): its notices' and its acts'.</summary>
    internal static Dictionary<string, string> Handed(string language) =>
        Catalogue(language)
            .Where(each => each.Key.StartsWith(SessionBalloon.Prefix, StringComparison.Ordinal) || each.Key.StartsWith("person.route.", StringComparison.Ordinal))
            .ToDictionary(each => each.Key, each => each.Value, StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string>? Words(string language) => language == "none" ? null : Handed(language);

    [Theory]
    [InlineData("Parked", "zh", "engine", "它问用哪个分支？", null, "engine：一个会话等你处理", "它问用哪个分支？")]
    [InlineData("Parked", "zh", "engine", null, null, "engine：一个会话等你处理", "打开 Daoris 回答它。")]
    [InlineData("Parked", "en", "engine", null, null, "engine: a session waits on you", "Open Daoris to answer it.")]
    [InlineData("Parked", "none", "engine", "Which branch?", null, "engine: a session waits on you", "Which branch?")]
    [InlineData("QuestParked", "zh", "engine", "You've hit your limit.", 3, "engine：委托 #7a82cc 在 3 个失败的会话后挂起", "You've hit your limit.")]
    [InlineData("QuestParked", "en", "engine", null, 1, "engine: quest #7a82cc parked after 1 failed session", "Open Daoris to try it again.")]
    [InlineData("QuestParked", "en", "engine", null, null, "engine: quest #7a82cc parked after its failed sessions", "Open Daoris to try it again.")]
    [InlineData("QuestParked", "zh", "engine", null, null, "engine：委托 #7a82cc 因失败的会话而挂起", "打开 Daoris 再试一次。")]
    [InlineData("Ended", "zh", "tools", null, null, "tools：一个会话失败了", "打开 Daoris 查看它做了什么。")]
    [InlineData("Ended", "en", "tools", "Exited before it concluded.", null, "tools: a session failed", "Exited before it concluded.")]
    [InlineData("Waiting", "zh", "engine, game", "Every account claude-code may use is cooling until 15:00.", null, "engine, game：一次启动在等待账户", "打开 Daoris 查看它在等哪些账户。")]
    [InlineData("Waiting", "none", "engine", "Every account claude-code may use is cooling until 15:00.", null, "engine: a start waits for an account", "Open Daoris to see which accounts it waits for.")]
    [InlineData("Answered", "zh", "engine", null, null, "engine：一个对话回复了你", "打开 Daoris 阅读。")]
    [InlineData("Answered", "en", "daoris:help", null, null, "Ask Daoris answered you", "Open Daoris to read it.")]
    [InlineData("Answered", "zh", "daoris:help", null, null, "问道衍回复了你", "打开 Daoris 阅读。")]
    [InlineData("Holding", "zh", "engine", "npm test", null, "engine：一个会话的工具调用在等你处理", "npm test")]
    [InlineData("Holding", "zh", "engine", null, null, "engine：一个会话的工具调用在等你处理", "在它被自动拒绝前，打开 Daoris 回答它。")]
    [InlineData("Holding", "en", "engine", null, null, "engine: a session holds a tool call for you", "Open Daoris to answer it before it is refused by itself.")]
    [InlineData("Holding", "en", "daoris:help", "Edit notes.md", null, "Ask Daoris holds a tool call for you", "Edit notes.md")]
    public void Each_kind_is_worded_from_the_words_handed_in_either_language_and_in_english_with_none(
        string kind, string language, string repository, string? detail, int? strikes, string title, string body)
    {
        var facts = new BalloonFacts(kind, Detail: detail)
        {
            Repository = repository,
            State = kind == "Ended" ? "failed" : null,
            Quest = kind == "QuestParked" ? "7a82cc" : null,
            Strikes = strikes,
        };

        var said = SessionBalloon.Words(facts, Words(language));

        Assert.Equal((title, body), (said.Title, said.Body));
    }

    /// <summary>
    /// A terminal's press behind the window names its act in the window's language and no value, and never its route's constant
    /// (NOTIFY2a, §3.3): the act's words are the page's, by the route's catalogue key; a route the page handed no act for says the
    /// press's door alone. The event's own English (<c>Sent by …</c>) is no part of it.
    /// </summary>
    [Theory]
    [InlineData("zh", "DAORIS.DRIVER", "SESSION_INPUT", "一个终端在等你确认", "它请求以你的名义对会话说话。打开 Daoris 确认或拒绝。")]
    [InlineData("en", "DAORIS.DRIVER", "SESSION_INPUT", "A terminal waits for your confirmation", "It asks to say words to a session as yours. Open Daoris to confirm or refuse it.")]
    [InlineData("zh", "DAORIS.REMOTES", "SET", "一个终端在等你确认", "它请求将一个工作区接到远端。打开 Daoris 确认或拒绝。")]
    [InlineData("zh", "DAORIS.DRIVER", "NO_SUCH_ROUTE", "一个终端在等你确认", "打开 Daoris 确认或拒绝。")]
    [InlineData("none", "DAORIS.DRIVER", "SESSION_INPUT", "A terminal waits for your confirmation", "Open Daoris to confirm or refuse it.")]
    public void A_press_names_its_act_in_the_windows_language_and_no_route(string language, string module, string route, string title, string body)
    {
        var facts = new BalloonFacts("Confirming", "A terminal's press waits for you in Daoris's window", "Sent by daoris-driver (4242). Open Daoris to confirm or refuse it.")
        {
            Module = module,
            Route = route,
        };

        var said = SessionBalloon.Words(facts, Words(language));

        Assert.Equal((title, body, BalloonIcon.Warning), (said.Title, said.Body, said.Icon));
        Assert.DoesNotContain(route, said.Title + said.Body, StringComparison.Ordinal);
    }

    /// <summary>An end is said by its state's own words (§6: a state by a key per state), and a state no words name is an ending, never its code.</summary>
    [Theory]
    [InlineData("completed")]
    [InlineData("failed")]
    [InlineData("stopped")]
    [InlineData("declined")]
    [InlineData("stood-down")]
    [InlineData("vanished")]
    [InlineData("")]
    public void An_end_state_is_said_by_its_own_words_and_never_raw_in_a_sentence(string state)
    {
        var en = Catalogue("en");
        var zh = Catalogue("zh");
        var key = SessionBalloon.English.ContainsKey($"{SessionBalloon.Prefix}ended.{state}") ? $"{SessionBalloon.Prefix}ended.{state}" : $"{SessionBalloon.Prefix}ended.other";
        var facts = new BalloonFacts("Ended") { Repository = "tools", State = state };

        Assert.Equal(en[key].Replace("{{repository}}", "tools", StringComparison.Ordinal), SessionBalloon.Words(facts, Handed("en")).Title);
        Assert.Equal(en[key].Replace("{{repository}}", "tools", StringComparison.Ordinal), SessionBalloon.Words(facts, null).Title);
        var chinese = SessionBalloon.Words(facts, Handed("zh")).Title;
        Assert.Equal(zh[key].Replace("{{repository}}", "tools", StringComparison.Ordinal), chinese);
        if (state.Length > 0) Assert.DoesNotContain(state, chinese, StringComparison.Ordinal);
    }

    /// <summary>
    /// With no words handed the balloon says English, and that English is the English catalogue's, word for word: a twin of the page's
    /// catalogue held here, so the two never drift. Every key it reads is in the 中文 catalogue too, with the same placeholders.
    /// </summary>
    [Fact]
    public void The_english_said_with_no_words_handed_is_the_english_catalogues_and_each_key_is_in_both()
    {
        var en = Catalogue("en");
        var zh = Catalogue("zh");

        Assert.NotEmpty(SessionBalloon.English);
        foreach (var (key, english) in SessionBalloon.English)
        {
            Assert.Equal((key, english), (key, en.GetValueOrDefault(key)));
            Assert.True(zh.ContainsKey(key), $"{key} is not in the 中文 catalogue");
            Assert.Equal((key, Placeholders(english)), (key, Placeholders(zh[key])));
        }

        // And every key the catalogues hold for the balloon is one it reads: a word the page hands that nothing says is a word lost.
        Assert.Equal(
            en.Keys.Where(key => key.StartsWith(SessionBalloon.Prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal),
            SessionBalloon.English.Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>What the event carries is filled in once: a repository named like a placeholder is said as it is named, never filled again.</summary>
    [Fact]
    public void A_value_is_filled_in_once_and_said_as_written()
    {
        var said = SessionBalloon.Words(new BalloonFacts("QuestParked") { Repository = "{{quest}}", Quest = "{{count}}", Strikes = 2 }, Handed("en"));

        Assert.Equal("{{quest}}: quest #{{count}} parked after 2 failed sessions", said.Title);
    }

    /// <summary>A kind this build does not know says the event's own headline and detail, as before NOTIFY2d.</summary>
    [Fact]
    public void A_kind_it_does_not_know_says_the_events_own_words()
    {
        var said = SessionBalloon.Words(new BalloonFacts("Later", "engine — something new", "As it said."), Handed("zh"));

        Assert.Equal(("engine — something new", "As it said.", BalloonIcon.Info), (said.Title, said.Body, said.Icon));
        Assert.Equal("打开 Daoris 查看它做了什么。", SessionBalloon.Words(new BalloonFacts("Later", "engine — something new"), Handed("zh")).Body);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_detail_gets_the_kinds_line_not_an_empty_body(string detail)
    {
        Assert.Equal("Open Daoris to answer it.", SessionBalloon.Words(new BalloonFacts("Parked", Detail: detail) { Repository = "engine" }, null).Body);
    }

    /// <summary>Warn for what WAITS on somebody: a park, a quest's park, a terminal's press and a held tool call. An ending, an answer or a wait for an account is news.</summary>
    [Theory]
    [InlineData("Parked", BalloonIcon.Warning)]
    [InlineData("QuestParked", BalloonIcon.Warning)]
    [InlineData("Confirming", BalloonIcon.Warning)]
    [InlineData("Holding", BalloonIcon.Warning)]
    [InlineData("Answered", BalloonIcon.Info)]
    [InlineData("Ended", BalloonIcon.Info)]
    [InlineData("Waiting", BalloonIcon.Info)]
    [InlineData(null, BalloonIcon.Info)]
    public void Each_kind_has_its_icon(string? kind, BalloonIcon icon)
    {
        Assert.Equal(icon, SessionBalloon.Icon(kind));
        Assert.Equal(icon, SessionBalloon.Words(new BalloonFacts(kind, "headline") { Repository = "engine" }, null).Icon);
    }

    private static string Placeholders(string text) =>
        string.Join(",", Placeholder().Matches(text).Select(match => match.Groups[1].Value).Distinct().Order(StringComparer.Ordinal));

    [GeneratedRegex(@"\{\{\s*(\w+)\s*\}\}")]
    private static partial Regex Placeholder();

    /// <summary>A language's whole catalogue, its area files merged as the page loads them.</summary>
    private static Dictionary<string, string> Catalogue(string language)
    {
        var folder = Path.Combine(SourceFiles.WorkspaceFolder(), "src", "Daoris.Web", "src", "locales", language);
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            foreach (var (key, value) in JsonNode.Parse(File.ReadAllText(file))!.AsObject())
            {
                if (value?.GetValue<string>() is { } text) merged[key] = text;
            }
        }

        Assert.NotEmpty(merged);
        return merged;
    }
}
