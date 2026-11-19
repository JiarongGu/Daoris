using Daoris.Driver;
using Daoris.Ports.Browser;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A driven session that meets an expired sign-in asks the person through <c>question_ask</c> with the site: its park waits on their
/// sign-in, and a sign-in Daoris's browser sees there (a page through a sign-in address, then on the site) settles it with Daoris's
/// word under one key, so the next look takes the park up and its next turn goes on.
/// </summary>
public sealed class SignInWaitTests : IDisposable
{
    private const string Site = "https://portal.example.com/settings";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-signin-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new(Poll.Patience);

    public SignInWaitTests()
    {
        Directory.CreateDirectory(_home);
        _ledger.Register("engine", Path.Combine(_home, "engine"));
        _ledger.Publish("q1", "engine");
        _ledger.Park("s1", "q1", DaorisHome.Entry(_home, HomeEntry.Trees, "s-1"));
    }

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The browser's readings, one per look, the last one again once they run out; null where it does not run.</summary>
    private sealed class Pages(params IReadOnlyList<BrowserPage>?[] readings) : IInAppBrowser
    {
        private int _next;

        public Task<string?> EnsureAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);

        public void Show()
        {
        }

        public void Open(string address)
        {
        }

        public Task<IReadOnlyList<BrowserPage>?> PagesAsync(CancellationToken ct = default) =>
            Task.FromResult(readings[Math.Min(_next++, readings.Length - 1)]);
    }

    private void Asked(string session, string? site = Site, bool conversation = false)
    {
        var file = new System.Text.Json.Nodes.JsonObject
        {
            ["id"] = "q-sign", ["session"] = session, ["question"] = "The production identity (ops@portal) expired: sign in so I can restore the settings.",
            ["choices"] = new System.Text.Json.Nodes.JsonArray(), ["recommend"] = null, ["why"] = null, ["by"] = null, ["owner"] = "sign-in",
            ["at"] = "2026-11-19T09:00:00.0000000+00:00",
        };
        if (site is not null) file["site"] = site;
        if (conversation) file["conversation"] = true;
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Sessions, session + QuestionFiles.Suffix), file.ToJsonString());
    }

    private Daoris.Driver.Driver Driver(IInAppBrowser browser, List<string> resumed)
    {
        var adapters = ProviderRegistry.Built();
        var config = DriverConfig.Empty with { Drivable = ["engine"], Actors = StaffedRoles.On("stub"), Cap = 3, PollSeconds = 1 };
        return new Daoris.Driver.Driver(
            _ledger.Client(), config, adapters, _home, harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)),
            browser: browser)
        {
            Runner = (start, opened, ct) =>
            {
                lock (resumed) resumed.Add(start.Resumes?.Session ?? "");
                return Task.FromResult(new StartRun("stand-in", false));
            },
        };
    }

    private static BrowserPage Page(string target, string address) => new(target, address);

    /// <summary>The words kept on the park, as the next look takes them up.</summary>
    private IReadOnlyList<(string Text, string? From, string? Key)> Said() =>
        [.. (_ledger.Session("s1")["said"]?.AsArray() ?? []).Select(word =>
            (word!["text"]!.GetValue<string>(), word["from"]?.GetValue<string>(), word["key"]?.GetValue<string>()))];

    /// <summary>
    /// 🔴 The row's case: the agent asked for the sign-in with its site and parked. The person's page goes through the sign-in and
    /// comes back to the site; that look keeps Daoris's word on the park, once whatever later looks see, and the look after takes the
    /// park up to go on.
    /// </summary>
    [Fact]
    public async Task A_sign_in_the_browser_sees_on_the_site_settles_the_park_and_its_next_turn_goes_on()
    {
        Asked("s1");
        var resumed = new List<string>();
        var browser = new Pages(
            [Page("t1", "https://portal.example.com/settings")],
            [Page("t1", "https://login.example.net/oauth2/authorize?client=portal")],
            [Page("t1", "https://portal.example.com/settings")]);
        var driver = Driver(browser, resumed);

        await driver.TickAsync(_closing.Token);
        await driver.TickAsync(_closing.Token);
        Assert.Empty(Said());
        await driver.TickAsync(_closing.Token);
        await driver.TickAsync(_closing.Token);

        var (text, from, key) = Assert.Single(Said());
        Assert.Equal((DaorisWords.From, "sign-in:s1:q-sign"), (from, key));
        Assert.Contains("portal.example.com", text, StringComparison.Ordinal);
        Assert.Contains("s1", resumed);
    }

    /// <summary>
    /// A page that reaches the site before any sign-in (the address the person opened, before its redirect) settles nothing, nor
    /// does another site's page, nor a park whose question names no site, nor a browser that is not running.
    /// </summary>
    [Fact]
    public void Only_a_page_through_a_sign_in_and_back_on_the_site_settles_a_sign_in_question()
    {
        var sightings = new SignInSightings();
        var question = new ParkQuestion("q-sign", "s1", "Sign in.", [], null, null, DateTimeOffset.UnixEpoch, Owner: "sign-in", Site: Site);
        var plain = question with { Id = "q-plain", Session = "s2", Site = null };

        Assert.Empty(sightings.Signed([question, plain], [Page("t1", "https://portal.example.com/settings")]));
        Assert.Empty(sightings.Signed([question, plain], [Page("t1", "https://portal.example.com/login"), Page("t2", "https://other.example.org/")]));
        Assert.Empty(sightings.Signed([question, plain], [Page("t1", "https://portal.example.com/login"), Page("t2", "https://other.example.org/home")]));
        var signed = sightings.Signed([question, plain], [Page("t1", "https://PORTAL.example.com/settings?tab=2")]);

        Assert.Equal(["q-sign"], signed.Select(each => each.Id));
        Assert.Empty(new SignInSightings().Signed([question], []));
    }

    [Theory]
    [InlineData("https://login.microsoftonline.com/common/oauth2/v2.0/authorize", true)]
    [InlineData("https://portal.example.com/sign-in?next=/settings", true)]
    [InlineData("https://portal.example.com/account/log_in", true)]
    [InlineData("https://idp.example.com/adfs/ls/", true)]
    [InlineData("https://portal.example.com/settings", false)]
    [InlineData("https://portal.example.com/authors/7", false)]
    [InlineData("not an address", false)]
    public void A_sign_in_address_is_one_whose_host_or_path_names_a_sign_in(string address, bool signIn) =>
        Assert.Equal(signIn, SignInSightings.SignInAddress(address));

    /// <summary>🔴 A conversation's sign-in is said to it once and its question goes.</summary>
    [Fact]
    public async Task A_conversations_sign_in_is_said_to_it_once_and_its_question_goes()
    {
        Asked("c1", conversation: true);
        var browser = new Pages(
            [Page("t1", "https://portal.example.com/settings")],
            [Page("t1", "https://login.example.net/oauth2/authorize?client=portal")],
            [Page("t1", "https://portal.example.com/settings")]);
        var said = new List<(string Session, string Key)>();
        Task<SayAnswer> Say(string session, string text, string key, CancellationToken ct)
        {
            said.Add((session, key));
            return Task.FromResult(new SayAnswer(true, "kept."));
        }

        for (var look = 0; look < 5; look++) await SignInAnswers.SayAsync(_home, browser, Say, _closing.Token);

        Assert.Equal([("c1", "sign-in:c1:q-sign")], said);
        Assert.Null(new QuestionFiles(_home).Read("c1"));
    }

    /// <summary>The question's file keeps its site, read back as a web page's address alone.</summary>
    [Fact]
    public void A_sign_in_question_reads_back_with_its_site_and_one_with_no_web_address_reads_none()
    {
        Asked("s1");
        Asked("s2", site: "file:///c:/secrets");

        var files = new QuestionFiles(_home);

        Assert.Equal(Site, files.Read("s1")!.Site);
        Assert.Null(files.Read("s2")!.Site);
        Assert.Equal(["s1"], files.SignIns().Select(each => each.Session));
    }
}
