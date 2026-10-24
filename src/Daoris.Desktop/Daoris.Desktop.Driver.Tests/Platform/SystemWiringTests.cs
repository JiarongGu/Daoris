using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver.Tests.Platform;

/// <summary>
/// What This Daoris reads (WIRING1b, the this-Daoris design §3, §5, §6): one reader in the library, which the window's
/// <c>SYSTEM_WIRING</c> and the terminal's <c>daoris-driver wiring</c> both call, each fact with its source, a fact not read
/// <c>null</c> with its <c>…Why</c>, and no secret anywhere in the answer. Every world here is handed in: no test reads this
/// machine's environment, its account's or its home.
/// </summary>
public sealed class SystemWiringTests : IDisposable
{
    private readonly List<string> _made = [];

    /// <summary>A minted deployment key's shape (`ApiKeyStore`): `dk_` and 48 hex, its audit prefix the 8 after the marker.</summary>
    private const string Minted = "dk_0123abcd" + "4567890123456789abcdef0123456789abcdef01";

    private const string Secret = "not-a-minted-key-but-a-secret";

    public void Dispose()
    {
        foreach (var folder in _made)
        {
            try
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
                // A scratch folder something still holds is left for the OS to clean.
            }
        }
    }

    private string Scratch(string name)
    {
        var folder = Path.Combine(Path.GetTempPath(), "daoris-wiring-tests", Guid.NewGuid().ToString("N")[..8], name);
        Directory.CreateDirectory(folder);
        _made.Add(Path.GetDirectoryName(folder)!);
        return folder;
    }

    /// <summary>An install's shape (D93): the marker at its root, the application under <c>app/</c>, its home <c>data/</c>.</summary>
    private (string Install, string Home, string Driver) Install()
    {
        var install = Scratch("install");
        var driver = Path.Combine(install, "app", "daoris-driver");
        Directory.CreateDirectory(driver);
        File.WriteAllText(Path.Combine(install, DaorisHome.InstallMarker), "# Daoris\n");
        var home = Path.Combine(install, DaorisHome.InstallData);
        Directory.CreateDirectory(home);
        return (install, home, driver);
    }

    private static Func<string, string?> Env(params (string Name, string? Value)[] values)
    {
        var map = values.ToDictionary(v => v.Name, v => v.Value, StringComparer.OrdinalIgnoreCase);
        return name => map.TryGetValue(name, out var value) ? value : null;
    }

    private static WiringWorld Terminal(string? home, string baseDirectory, Func<string, string?>? process = null,
        Func<string, string?>? account = null, WiringSizes? sizes = null) => new()
    {
        Home = home,
        BaseDirectory = baseDirectory,
        Process = process ?? Env(),
        Account = account ?? Env(),
        Machine = Env(),
        Lives = (_, _) => false,
        AskStatus = _ => null,
        Sizes = sizes ?? new WiringSizes(),
    };

    private static WindowWiring Window(string held = WindowWiring.Hosted, string? homeNotice = null, string? hostNotice = null) => new()
    {
        Held = held,
        HomeAccount = "same",
        HomeNotice = homeNotice,
        HostNotice = hostNotice,
        ServiceUrl = "http://localhost:5177",
        Status = new ServiceProbe(true, DateTimeOffset.Parse("2026-10-16T09:00:00Z"), null, "lexical only", "1.2.3+abc"),
        Kit = "0.21.1",
        Ready = true,
        Looked = DateTimeOffset.Parse("2026-10-16T09:01:00Z"),
        ProcessId = Environment.ProcessId,
    };

    private static JsonObject Variable(JsonObject wiring, string name) =>
        wiring["variables"]!.AsArray().Select(row => row!.AsObject()).Single(row => (string?)row["name"] == name);

    private static JsonObject Folder(JsonObject wiring, string id) =>
        wiring["folders"]!.AsArray().Select(row => row!.AsObject()).Single(row => (string?)row["id"] == id);

    private static JsonObject FileRow(JsonObject wiring, string id) =>
        wiring["files"]!.AsArray().Select(row => row!.AsObject()).Single(row => (string?)row["id"] == id);

    [Fact]
    public void Every_section_is_answered_and_what_only_the_window_knows_is_null_with_why_window()
    {
        var (_, home, driver) = Install();

        var wiring = SystemWiring.Read(Terminal(home, driver));

        Assert.Equal(1, (int)wiring["schema"]!);
        Assert.Equal("terminal", (string?)wiring["reader"]);
        foreach (var section in new[] { "folder", "folders", "leftOver", "service", "remotes", "loop", "files", "variables", "versions", "terminal", "knowledge", "logs" })
        {
            Assert.True(wiring.ContainsKey(section), section);
        }

        var service = wiring["service"]!.AsObject();
        Assert.Null(service["held"]);
        Assert.Equal("window", (string?)service["heldWhy"]);
        Assert.Null(wiring["versions"]!["kit"]);
        Assert.Equal("window", (string?)wiring["versions"]!["kitWhy"]);
        Assert.Null(wiring["loop"]!["looked"]);
        Assert.Equal("window", (string?)wiring["loop"]!["lookedWhy"]);
        // A null fact is a field with its why, never an absent one (design §6).
        Assert.True(wiring["folder"]!.AsObject().ContainsKey("notice"));
        Assert.Equal("window", (string?)wiring["folder"]!["noticeWhy"]);
    }

    [Fact]
    public void The_folder_is_an_install_a_development_run_or_named_for_this_start()
    {
        var (install, home, driver) = Install();
        Assert.Equal("install", (string?)SystemWiring.Read(Terminal(home, driver))["folder"]!["kind"]);

        var elsewhere = Scratch("named");
        Assert.Equal("named", (string?)SystemWiring.Read(Terminal(elsewhere, driver))["folder"]!["kind"]);

        var build = Scratch("build");
        var wiring = SystemWiring.Read(Terminal(elsewhere, build));
        Assert.Equal("development", (string?)wiring["folder"]!["kind"]);
        // A development run has no launchers, and says so (§3.7).
        Assert.Empty(wiring["terminal"]!["launchers"]!.AsArray());
        Assert.Equal("development", (string?)wiring["terminal"]!["launchersWhy"]);
        Assert.Null(Folder(wiring, "app")["path"]);
        Assert.Equal("development", (string?)Folder(wiring, "app")["pathWhy"]);

        Assert.Equal(Path.Combine(install, "app"), (string?)Folder(SystemWiring.Read(Terminal(home, driver)), "app")["path"]);
    }

    [Fact]
    public void What_the_accounts_variable_names_is_the_install_its_data_or_nothing()
    {
        var (install, home, driver) = Install();

        string? Names(string? account) =>
            (string?)SystemWiring.Read(Terminal(home, driver, account: Env((DaorisHome.Variable, account))))["folder"]!["accountNames"];

        Assert.Equal("data", Names(home));
        Assert.Equal("root", Names(install));
        Assert.Equal("none", Names(null));

        var same = SystemWiring.Read(Terminal(home, driver, account: Env((DaorisHome.Variable, home))));
        Assert.Equal("same", (string?)same["folder"]!["account"]);
        var other = SystemWiring.Read(Terminal(home, driver, account: Env((DaorisHome.Variable, install))));
        Assert.Equal("this-start", (string?)other["folder"]!["account"]);
    }

    /// <summary>
    /// The layout as it runs (the second opinion's point 4): every entry where it is, an install's sign-ins still in its
    /// <c>data/</c> (a home from before HOMELAYOUT3 moved them), said under <c>profiles</c> with why they stay; never the planned
    /// table as an installed fact.
    /// </summary>
    [Fact]
    public void Each_entry_is_shown_where_it_runs_and_an_old_place_says_why_it_stays()
    {
        var (install, home, driver) = Install();
        File.WriteAllText(DaorisHome.Entry(home, HomeEntry.DriverConfig), "{}");
        File.WriteAllText(DaorisHome.Places(home, HomeEntry.Keys)[^1], "{}");
        Directory.CreateDirectory(DaorisHome.Places(home, HomeEntry.Trees, "a", "b", "c")[^1]);
        Directory.CreateDirectory(DaorisHome.Places(home, HomeEntry.Bin)[^1]);

        var wiring = SystemWiring.Read(Terminal(home, driver));

        var data = Folder(wiring, "data");
        Assert.Equal(home, (string?)data["path"]);
        Assert.True((bool)data["present"]!);
        Assert.Empty(data["old"]!.AsArray());

        var profiles = Folder(wiring, "profiles");
        Assert.Equal(Path.Combine(install, "profiles"), (string?)profiles["path"]);
        Assert.False((bool)profiles["present"]!);
        var keys = profiles["old"]!.AsArray().Single(row => (string?)row!["entry"] == "keys")!;
        Assert.Equal(DaorisHome.Entry(home, HomeEntry.Keys), (string?)keys["path"]);
        Assert.Equal("not-moved", (string?)keys["why"]);

        Assert.Equal("resumes", (string?)Folder(wiring, "work")["old"]!.AsArray().Single(row => (string?)row!["entry"] == "trees")!["why"]);
        Assert.Equal("mcp", (string?)Folder(wiring, "cache")["old"]!.AsArray().Single(row => (string?)row!["entry"] == "bin")!["why"]);
    }

    [Fact]
    public void Sizes_are_measured_never_guessed_and_until_then_null()
    {
        var (_, home, driver) = Install();
        File.WriteAllText(DaorisHome.Entry(home, HomeEntry.DriverConfig), new string('x', 100));
        Directory.CreateDirectory(DaorisHome.Entry(home, HomeEntry.Sessions));
        Directory.CreateDirectory(DaorisHome.Entry(home, HomeEntry.SessionFiles, "s1"));
        File.WriteAllText(DaorisHome.Entry(home, HomeEntry.Sessions, "s1.jsonl"), new string('y', 30));
        File.WriteAllText(DaorisHome.Entry(home, HomeEntry.SessionFiles, "s1", "kept.png"), new string('z', 7));
        File.WriteAllText(DaorisHome.Entry(home, HomeEntry.Keys), new string('k', 11));
        Directory.CreateDirectory(Path.Combine(home, "webview2"));
        File.WriteAllText(Path.Combine(home, "webview2", "old.bin"), new string('w', 5));
        File.WriteAllText(Path.Combine(home, "stranger.txt"), new string('s', 3));
        var sizes = new WiringSizes();

        var before = SystemWiring.Read(Terminal(home, driver, sizes: sizes));
        Assert.Null(Folder(before, "data")["bytes"]);
        Assert.Equal("not-measured", (string?)Folder(before, "data")["bytesWhy"]);
        Assert.Null(before["measured"]);

        var reading = sizes.MeasureNow(home, Path.GetDirectoryName(home), WiringBudget.Default, CancellationToken.None);
        Assert.False(reading.Partial);

        var after = SystemWiring.Read(Terminal(home, driver, sizes: sizes));
        // driver.json and the sessions' records are data; the kept file under a session is work's (D172's table), counted once.
        Assert.Equal(130, (long)Folder(after, "data")["bytes"]!);
        Assert.Equal(7, (long)Folder(after, "work")["bytes"]!);
        Assert.Equal(11, (long)Folder(after, "profiles")["bytes"]!);
        Assert.NotNull(after["measured"]);

        var left = after["leftOver"]!.AsArray().Select(row => row!.AsObject()).ToDictionary(row => (string)row["name"]!);
        Assert.Equal("earlier-build", (string?)left["webview2"]["why"]);
        Assert.Equal(5, (long)left["webview2"]["bytes"]!);
        Assert.Equal("not-daoris", (string?)left["stranger.txt"]["why"]);
    }

    /// <summary>A named workspace's Ask Daoris room (ASKSCOPE1b) is the table's (HOMEROOMS1), so the census does not list it left over.</summary>
    [Theory]
    [InlineData("help", true)]
    [InlineData("help-alpha", true)]
    [InlineData("help-e4_b8_ad", true)]
    [InlineData("helper", false)]
    [InlineData("xhelp-alpha", false)]
    public void A_named_workspace_s_help_room_is_held_by_the_table_and_a_look_alike_is_not(string name, bool held) =>
        Assert.Equal(held, WiringSizes.Held(name));

    [Fact]
    public void A_walk_past_its_budget_or_cancelled_answers_what_it_counted_and_says_so()
    {
        var (_, home, _) = Install();
        for (var at = 0; at < 5; at++) File.WriteAllText(Path.Combine(home, $"usage{at}.json"), "1234");

        var budgeted = new WiringSizes().MeasureNow(home, null, new WiringBudget(Files: 2, Time: TimeSpan.FromMinutes(1)), CancellationToken.None);
        Assert.True(budgeted.Partial);
        Assert.Equal("budget", budgeted.Why);
        Assert.True(budgeted.Counted <= 2);

        using var stop = new CancellationTokenSource();
        stop.Cancel();
        var cancelled = new WiringSizes().MeasureNow(home, null, WiringBudget.Default, stop.Token);
        Assert.True(cancelled.Partial);
        Assert.Equal("cancelled", cancelled.Why);
    }

    [Fact]
    public async Task A_measure_asked_runs_in_the_background_one_at_a_time_and_is_kept_for_the_next_window()
    {
        var (_, home, _) = Install();
        File.WriteAllText(DaorisHome.Entry(home, HomeEntry.DriverConfig), "{}");
        var sizes = new WiringSizes();

        Assert.True(sizes.Start(home, null, WiringBudget.Default));
        await sizes.Finished(home);

        Assert.False(sizes.Measuring(home));
        Assert.NotNull(sizes.Of(home));
        // Another window reads the same reading: the measure is kept across windows, never walked again on an open.
        Assert.Same(sizes.Of(home), sizes.Of(home));
    }

    [Fact]
    public void A_variables_source_is_this_start_inherited_set_by_daoris_or_not_set()
    {
        var (_, home, driver) = Install();
        var wiring = SystemWiring.Read(Terminal(home, driver,
            process: Env(("DAORIS_SERVICE_URL", "http://localhost:5199"), ("DAORIS_HTTP_HOST", "C:/hosts/h.exe")),
            account: Env(("DAORIS_SERVICE_URL", "http://localhost:5199"), ("DAORIS_HTTP_HOST", "C:/other/h.exe"))));

        // The same value as the account's: this process cannot tell the account's from what its launch handed it.
        Assert.Equal("inherited", (string?)Variable(wiring, "DAORIS_SERVICE_URL")["from"]);
        var host = Variable(wiring, "DAORIS_HTTP_HOST");
        Assert.Equal("this-start", (string?)host["from"]);
        Assert.Equal("C:/other/h.exe", (string?)host["account"]);

        var unset = Variable(wiring, "DAORIS_MCP_HOST");
        Assert.False((bool)unset["set"]!);
        Assert.Equal("not-set", (string?)unset["from"]);
        Assert.Null(unset["value"]);

        var windowed = SystemWiring.Read(Terminal(home, driver, process: Env((DaorisHome.Variable, home))) with
        {
            Window = Window() with { HomeSetByDaoris = true },
        });
        Assert.Equal("set-by-daoris", (string?)Variable(windowed, DaorisHome.Variable)["from"]);
        Assert.Equal("http://localhost:5177", (string?)Variable(windowed, "DAORIS_SERVICE_URL")["instead"]);
    }

    /// <summary>
    /// A key is <c>set</c> and at most its audit prefix where it has one (the second opinion's point 1): a key that is not a
    /// minted deployment key shows no part of itself, and no value of a key is anywhere in the answer.
    /// </summary>
    [Fact]
    public void A_key_is_set_and_at_most_a_minted_keys_prefix_and_never_its_value()
    {
        var (_, home, driver) = Install();
        var wiring = SystemWiring.Read(Terminal(home, driver,
            process: Env(("DAORIS_SERVICE_KEY", Secret), ("DAORIS_REMOTE_KEY", Minted), ("DAORIS_REMOTE_URL", "https://team.example")),
            account: Env(("DAORIS_SERVICE_KEY", Secret + "-account"))));

        var service = Variable(wiring, "DAORIS_SERVICE_KEY");
        Assert.True((bool)service["secret"]!);
        Assert.True((bool)service["set"]!);
        Assert.Null(service["value"]);
        Assert.Null(service["prefix"]);
        Assert.Null(service["account"]);
        Assert.True((bool)service["accountSet"]!);
        Assert.Equal("0123abcd", (string?)Variable(wiring, "DAORIS_REMOTE_KEY")["prefix"]);

        var text = SystemWiring.Json(wiring);
        Assert.DoesNotContain(Secret, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Minted, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Minted[11..], text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_address_is_shown_without_its_credentials_or_query_and_one_that_does_not_parse_is_not_shown()
    {
        var (_, home, driver) = Install();
        var wiring = SystemWiring.Read(Terminal(home, driver, process: Env(
            ("DAORIS_SERVICE_URL", "http://someone:pa55word@localhost:5177/base?token=t0ken#frag"),
            ("DAORIS_REMOTE_URL", "not a url, with s3cret inside"))));

        Assert.Equal("http://localhost:5177/base", (string?)Variable(wiring, "DAORIS_SERVICE_URL")["value"]);
        Assert.Equal("http://localhost:5177/base", (string?)wiring["service"]!["url"]);
        var embed = Variable(wiring, "DAORIS_REMOTE_URL");
        Assert.Null(embed["value"]);
        Assert.Equal("malformed", (string?)embed["valueWhy"]);

        var text = SystemWiring.Json(wiring);
        foreach (var leaked in new[] { "pa55word", "someone", "t0ken", "frag", "s3cret" }) Assert.DoesNotContain(leaked, text, StringComparison.Ordinal);
    }

    /// <summary>
    /// An adopted service's environment is not this window's (the second opinion's point 2): what only the service reads is
    /// unknown, said as unknown, and the window's own value is not passed off as the service's.
    /// </summary>
    [Fact]
    public void What_only_the_service_reads_is_unknown_for_an_adopted_service_and_this_windows_for_a_hosted_one()
    {
        var (_, home, driver) = Install();
        var process = Env(("DAORIS_KNOWLEDGE_ROOT", "C:/family"), ("DAORIS_EMBED_MODEL", "nomic"));

        var adopted = SystemWiring.Read(Terminal(home, driver, process: process) with { Window = Window(WindowWiring.Adopted) });
        var root = Variable(adopted, "DAORIS_KNOWLEDGE_ROOT");
        Assert.Null(root["value"]);
        Assert.Equal("unknown", (string?)root["from"]);
        Assert.Equal("adopted", (string?)root["valueWhy"]);
        Assert.Null(adopted["knowledge"]!["familyRoot"]);
        Assert.Equal("adopted", (string?)adopted["knowledge"]!["familyRootWhy"]);
        Assert.Equal("adopted", (string?)adopted["service"]!["held"]);

        var hosted = SystemWiring.Read(Terminal(home, driver, process: process) with { Window = Window() });
        Assert.Equal("C:/family", (string?)Variable(hosted, "DAORIS_KNOWLEDGE_ROOT")["value"]);
        Assert.Equal("C:/family", (string?)hosted["knowledge"]!["familyRoot"]);

        // A terminal knows neither.
        Assert.Equal("window", (string?)Variable(SystemWiring.Read(Terminal(home, driver, process: process)), "DAORIS_EMBED_MODEL")["valueWhy"]);
    }

    [Fact]
    public void A_verbatim_notice_is_passed_through_redacted()
    {
        var (_, home, driver) = Install();
        var wiring = SystemWiring.Read(Terminal(home, driver, process: Env(("DAORIS_SERVICE_KEY", Secret))) with
        {
            Window = Window(
                homeNotice: $"Daoris home: {home} — the key {Secret} was seen.",
                hostNotice: "the service already running at http://u:hunter2@localhost:5177/?key=zz9 serves index-a.js"),
        });

        var notice = (string?)wiring["folder"]!["notice"];
        Assert.Contains(home, notice);
        Assert.DoesNotContain(Secret, notice, StringComparison.Ordinal);
        var hostNotice = (string?)wiring["folder"]!["hostNotice"];
        Assert.Contains("http://localhost:5177/", hostNotice);
        Assert.DoesNotContain("hunter2", hostNotice, StringComparison.Ordinal);
        Assert.DoesNotContain("zz9", hostNotice, StringComparison.Ordinal);
    }

    [Fact]
    public void Each_remote_says_its_address_its_source_and_at_most_a_minted_prefix()
    {
        var (_, home, driver) = Install();
        File.WriteAllText(DaorisHome.Entry(home, HomeEntry.Remotes),
            $$"""{ "team": { "url": "https://team.example/?sig=abc123", "key": "{{Minted}}" }, "lab": { "url": "https://lab.example", "key": "{{Secret}}" } }""");

        var fromFile = SystemWiring.Read(Terminal(home, driver));
        var rows = fromFile["remotes"]!.AsArray().Select(row => row!.AsObject()).ToDictionary(row => (string)row["workspace"]!);
        Assert.Equal("https://team.example/", (string?)rows["team"]["url"]);
        Assert.Equal("0123abcd", (string?)rows["team"]["keyPrefix"]);
        Assert.Null(rows["lab"]["keyPrefix"]);
        Assert.True((bool)rows["lab"]["keySet"]!);
        Assert.Equal("file", (string?)rows["lab"]["from"]);

        var fromEnvironment = SystemWiring.Read(Terminal(home, driver,
            process: Env(("DAORIS_REMOTE_URL", "https://env.example"), ("DAORIS_REMOTE_KEY", Secret))));
        var only = fromEnvironment["remotes"]!.AsArray().Single()!;
        Assert.Equal("environment", (string?)only["from"]);
        Assert.Equal("default", (string?)only["workspace"]);

        foreach (var text in new[] { SystemWiring.Json(fromFile), SystemWiring.Json(fromEnvironment) })
        {
            Assert.DoesNotContain(Secret, text, StringComparison.Ordinal);
            Assert.DoesNotContain("abc123", text, StringComparison.Ordinal);
            Assert.DoesNotContain(Minted, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Who_holds_the_home_is_read_from_its_lease()
    {
        var (_, home, driver) = Install();
        Assert.Null(SystemWiring.Read(Terminal(home, driver))["loop"]!["holder"]);

        using (var held = HomeLease.TryAcquire(home, LeaseKind.Headless, out _))
        {
            Assert.NotNull(held);
            var holder = SystemWiring.Read(Terminal(home, driver))["loop"]!["holder"]!;
            Assert.Equal("headless", (string?)holder["kind"]);
            Assert.True((bool)holder["instance"]!);
            Assert.Equal(Environment.ProcessId, (int)holder["pid"]!);
            Assert.False((bool)holder["thisWindow"]!);
            // A headless loop in this process is no window's, whatever its process.
            Assert.False((bool)SystemWiring.Read(Terminal(home, driver) with { Window = Window() })["loop"]!["holder"]!["thisWindow"]!);
        }

        using (HomeLease.TryAcquire(home, LeaseKind.Desktop, out _))
        {
            Assert.True((bool)SystemWiring.Read(Terminal(home, driver) with { Window = Window() })["loop"]!["holder"]!["thisWindow"]!);
        }

        // A verb holds it for its act, said by its verb alone.
        using (HomeLease.TryAcquire(home, LeaseKind.Verb, out _, what: "trees remove"))
        {
            var verb = SystemWiring.Read(Terminal(home, driver))["loop"]!["holder"]!;
            Assert.Equal("verb", (string?)verb["kind"]);
            Assert.False((bool)verb["instance"]!);
            Assert.Equal("trees remove", (string?)verb["what"]);
        }
    }

    [Fact]
    public void A_settings_file_says_whether_it_reads_and_what_overrides_it_never_its_contents()
    {
        var (_, home, driver) = Install();
        File.WriteAllText(DaorisHome.Entry(home, HomeEntry.DriverConfig), """{ "pollSeconds": 45 }""");
        File.WriteAllText(DaorisHome.Entry(home, HomeEntry.Tools), "{ not json: t0psecret");
        File.WriteAllText(DaorisHome.Entry(home, HomeEntry.Keys), """{ "claude": "sk-very-secret" }""");

        var wiring = SystemWiring.Read(Terminal(home, driver));

        var config = FileRow(wiring, "driver");
        Assert.True((bool)config["present"]!);
        Assert.True((bool)config["reads"]!);
        Assert.Equal("driver", (string?)config["door"]);
        Assert.Equal("data", (string?)config["folder"]);
        Assert.Equal(45, (int)wiring["loop"]!["pollSeconds"]!);

        var tools = FileRow(wiring, "tools");
        Assert.False((bool)tools["reads"]!);
        Assert.Equal("unparsed", (string?)tools["readsWhy"]);

        var keys = FileRow(wiring, "keys");
        Assert.Null(keys["reads"]);
        Assert.Equal("private", (string?)keys["readsWhy"]);

        var text = SystemWiring.Json(wiring);
        Assert.DoesNotContain("t0psecret", text, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-very-secret", text, StringComparison.Ordinal);

        var elsewhere = Path.Combine(Scratch("override"), "mine.json");
        var overridden = SystemWiring.Read(Terminal(home, driver, process: Env(("DAORIS_DRIVER_CONFIG", elsewhere))));
        Assert.Equal("DAORIS_DRIVER_CONFIG", (string?)FileRow(overridden, "driver")["overriddenBy"]);
        Assert.Equal(elsewhere, (string?)FileRow(overridden, "driver")["path"]);
        Assert.False((bool)FileRow(overridden, "driver")["present"]!);
    }

    [Fact]
    public void The_address_file_is_read_as_a_terminal_reads_it()
    {
        var (_, home, driver) = Install();
        string? Why(Func<int, long, bool> lives, Func<string, ServiceProbe?> ask) =>
            (string?)SystemWiring.Read(Terminal(home, driver) with { Lives = lives, AskStatus = ask })["terminal"]!["address"]!["why"];
        // A host answering as this home's (HOMEID1): its status names the home's id.
        var mine = HomeIdentity.Ensure(home);
        ServiceProbe Answering(string _) => new(true, DateTimeOffset.UtcNow, null, "lexical only", null, mine);

        Assert.Equal("absent", Why((_, _) => true, Answering));

        File.WriteAllText(DaorisHome.Entry(home, HomeEntry.Running), """{ "url": "http://localhost:5177", "pid": 42, "started": 1 }""");
        Assert.Equal("ended", Why((_, _) => false, Answering));
        Assert.Equal("not-answering", Why((_, _) => true, _ => new ServiceProbe(false, DateTimeOffset.UtcNow, "no-answer", null, null)));
        // Another home's host, or one naming none, is not taken, as RunningService.Find judges it.
        Assert.Equal("another-home", Why((_, _) => true, _ => new ServiceProbe(true, DateTimeOffset.UtcNow, null, "lexical only", null, "0123456789abcdef0123456789abcdef")));
        Assert.Equal("another-home", Why((_, _) => true, _ => new ServiceProbe(true, DateTimeOffset.UtcNow, null, "lexical only", null)));

        var taken = SystemWiring.Read(Terminal(home, driver) with { Lives = (_, _) => true, AskStatus = Answering });
        Assert.True((bool)taken["terminal"]!["address"]!["taken"]!);
        // With no variable, a terminal an install carries takes the service from the address file (FINDDAORIS1).
        Assert.Equal("address-file", (string?)taken["service"]!["from"]);

        File.WriteAllText(DaorisHome.Entry(home, HomeEntry.Running), """{ "url": "http://192.0.2.7:5177", "pid": 42, "started": 1 }""");
        Assert.Equal("not-loopback", Why((_, _) => true, Answering));
    }

    [Fact]
    public void Whether_a_new_terminal_finds_this_installs_launchers_is_read_from_the_paths_never_shown_whole()
    {
        var (install, home, driver) = Install();
        var bin = Path.Combine(install, "app", "bin");
        Directory.CreateDirectory(bin);
        foreach (var name in new[] { "daoris", "daoris.cmd", "daoris-driver", "daoris-driver.cmd" }) File.WriteAllText(Path.Combine(bin, name), "");
        var other = Scratch("other-bin");
        File.WriteAllText(Path.Combine(other, "daoris-driver.cmd"), "");

        var wiring = SystemWiring.Read(Terminal(home, driver,
            account: Env(("PATH", $"{other}{Path.PathSeparator}{bin}"), ("PATHEXT", ".COM;.EXE;.CMD"))));

        var terminal = wiring["terminal"]!.AsObject();
        Assert.Equal(4, terminal["launchers"]!.AsArray().Count(row => (bool)row!["present"]!));
        Assert.True((bool)terminal["onAccountPath"]!);
        Assert.False((bool)terminal["onMachinePath"]!);
        Assert.Equal("this", (string?)terminal["firstDaoris"]!["is"]);
        Assert.Equal("another", (string?)terminal["firstDriver"]!["is"]);
        Assert.DoesNotContain(other + Path.PathSeparator, SystemWiring.Json(wiring), StringComparison.Ordinal);
    }

    [Fact]
    public void The_versions_name_what_this_install_carries_and_what_only_the_window_or_the_tools_know()
    {
        var (install, home, driver) = Install();
        var package = Path.Combine(install, "app", "cli", "node_modules", "daoris");
        Directory.CreateDirectory(package);
        File.WriteAllText(Path.Combine(package, "package.json"), """{ "name": "daoris", "version": "0.9.1" }""");

        var versions = SystemWiring.Read(Terminal(home, driver) with { Window = Window() })["versions"]!.AsObject();

        Assert.Equal("0.9.1", (string?)versions["cli"]);
        Assert.Equal("1.2.3+abc", (string?)versions["service"]);
        Assert.Equal("0.21.1", (string?)versions["kit"]);
        Assert.Equal(Environment.Version.ToString(), (string?)versions["dotnet"]);
        Assert.Null(versions["node"]);
        Assert.Equal("tools", (string?)versions["nodeWhy"]);
    }

    /// <summary>
    /// The search reads code, never a test's scratch: the CLI package's tests write the rehearsal's stub agent under their
    /// <c>_fixtures/</c> while CI's driver gate runs beside them, and the census once read it as a source (2026-10-24).
    /// </summary>
    [Theory]
    [InlineData("Daoris.Web/src/app/main.ts", true)]
    [InlineData("Daoris.Web/_fixtures/deployment-rehearsal-acp-stub/acp-agent.mjs", false)]
    [InlineData("Daoris.Web/fixtures/stub.mjs", false)]
    public void The_search_reads_code_and_never_a_tests_scratch(string relative, bool searched) =>
        Assert.Equal(searched, WiringVariables.Searched(relative));

    /// <summary>
    /// Every variable Daoris reads is on the list (design §3.5), held against a search of the sources: each <c>DAORIS_</c> name a
    /// source outside the tests spells is shown or set aside with its reason, and each the list names is still read somewhere.
    /// </summary>
    [Fact]
    public void Every_variable_the_sources_name_is_on_the_list_shown_or_set_aside_with_its_reason()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "daoris.json"))) root = root.Parent;
        Assert.NotNull(root);
        var source = Path.Combine(root.FullName, "src");

        var named = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(source, "*.*", SearchOption.AllDirectories))
        {
            if (!WiringVariables.Searched(Path.GetRelativePath(source, file))) continue;
            foreach (Match match in Regex.Matches(File.ReadAllText(file), @"\bDAORIS_[A-Z0-9_]+\b")) named.Add(match.Value);
        }

        var listed = WiringVariables.Shown.Select(v => v.Name).Concat(WiringVariables.SetAside.Keys).ToHashSet(StringComparer.Ordinal);
        Assert.Empty(named.Except(listed).Order(StringComparer.Ordinal));
        Assert.Empty(listed.Except(named).Order(StringComparer.Ordinal));
        Assert.All(WiringVariables.SetAside, aside => Assert.False(string.IsNullOrWhiteSpace(aside.Value)));
    }

    [Fact]
    public void The_terminals_json_is_the_routes_shape_and_its_text_says_the_window_knows()
    {
        var (_, home, driver) = Install();
        var world = Terminal(home, driver, process: Env(("DAORIS_SERVICE_KEY", Secret)));

        Assert.Equal(0, WiringCommand.Run(["--json"], world, out var json));
        Assert.Equal(SystemWiring.Json(SystemWiring.Read(world)), json);
        Assert.Equal(1, (int)JsonNode.Parse(json)!["schema"]!);

        Assert.Equal(0, WiringCommand.Run([], world, out var text));
        Assert.Contains("the window knows", text, StringComparison.Ordinal);
        Assert.Contains("DAORIS_SERVICE_KEY", text, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, text, StringComparison.Ordinal);

        Assert.Equal(2, WiringCommand.Run(["--loud"], world, out var refused));
        Assert.Contains(WiringCommand.Usage, refused, StringComparison.Ordinal);
    }

    [Fact]
    public void The_terminals_measure_walks_the_folder_before_it_answers()
    {
        var (_, home, driver) = Install();
        File.WriteAllText(DaorisHome.Entry(home, HomeEntry.DriverConfig), new string('x', 10));

        Assert.Equal(0, WiringCommand.Run(["--json", "--measure"], Terminal(home, driver), out var json));

        var data = JsonNode.Parse(json)!["folders"]!.AsArray().Single(row => (string?)row!["id"] == "data")!;
        Assert.Equal(10, (long)data["bytes"]!);
    }

    [Fact]
    public void A_status_body_is_read_for_its_tier_and_version_only_from_a_daoris_host()
    {
        var at = DateTimeOffset.Parse("2026-10-16T10:00:00Z");
        var status = ServiceProbe.Of(
            """{ "semantic": false, "tier": "lexical only", "note": "x", "home": "0123456789abcdef0123456789abcdef", "version": "2.0.0+deadbeef" }""", at);
        Assert.True(status.Answers);
        Assert.Equal("2.0.0+deadbeef", status.Version);
        Assert.Equal("lexical only", status.Tier);
        // The home's id read as discovery reads it (RunningService.ReadStatus, HOMEID1).
        Assert.Equal("0123456789abcdef0123456789abcdef", status.Home);

        var foreign = ServiceProbe.Of("<html>not a status</html>", at);
        Assert.False(foreign.Answers);
        Assert.Equal("foreign", foreign.Why);
    }

    [Fact]
    public void This_process_is_named_only_as_a_window_holder_and_its_since_is_a_moment()
    {
        // The holder's since is the lease's moment, written as a moment the page formats (format.ts), never a sentence.
        var (_, home, driver) = Install();
        using var held = HomeLease.TryAcquire(home, LeaseKind.Desktop, out _);
        var since = (string?)SystemWiring.Read(Terminal(home, driver))["loop"]!["holder"]!["since"];
        Assert.True(DateTimeOffset.TryParse(since, out _), since);
        Assert.Equal(Process.GetCurrentProcess().Id, Environment.ProcessId);
    }
}
