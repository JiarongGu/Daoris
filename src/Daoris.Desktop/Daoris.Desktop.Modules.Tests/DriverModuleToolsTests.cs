using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Settings → Tools over the bridge (TOOLS7, D121 §4.1; <c>DriverModule.Tools.cs</c>): each tool's way, what is
/// downloaded and what the lists offer, a download started as an action the page follows and the person stops, a
/// delete, the resource locations listed, added, removed and looked at, and what a switch of git changes.
/// </summary>
/// <remarks>
/// Nothing here starts a program. A list is a location's copy written by the test, a download is served from a host on
/// this machine (<see cref="LoopbackHost"/>), and a managed version is laid out by hand as a download leaves one. The
/// version a program answers and git's own configuration are asked of real programs, so those cases are
/// <see cref="DriverModuleToolProgramsTests"/>', in the suite's Process half (MOD8).
/// </remarks>
public sealed class DriverModuleToolsTests : DriverModuleBridge
{
    private static string Platform => ToolResources.Current ?? "win-x64";

    private static readonly byte[] Archive = LoopbackHost.Zip("bin/gh.exe", "gh");

    /// <summary>A resource list naming one version of one tool at one address.</summary>
    private static string List(string tool, string version, string url, byte[] bytes, string? sha256 = null) => new JsonObject
    {
        ["schema"] = 1,
        ["tools"] = new JsonObject
        {
            [tool] = new JsonObject
            {
                ["source"] = "https://maker.example/releases",
                ["licence"] = new JsonObject { ["id"] = "MIT", ["url"] = "https://maker.example/licence" },
                ["versions"] = new JsonObject
                {
                    [version] = new JsonObject
                    {
                        ["files"] = new JsonObject
                        {
                            [Platform] = new JsonObject
                            {
                                ["url"] = url,
                                ["sha256"] = sha256 ?? LoopbackHost.Sha256(bytes),
                                ["size"] = bytes.Length,
                                ["archive"] = "zip",
                                ["exe"] = "bin/gh.exe",
                            },
                        },
                    },
                },
            },
        },
    }.ToJsonString();

    /// <summary>A location the person listed, with its copy as a look would have kept it.</summary>
    private void Located(string address, string list)
    {
        Tools.AddLocation(Home, address);
        var copy = ToolResources.LocationCopy(Home, address);
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.WriteAllText(copy, list);
    }

    /// <summary>A version laid out as a download leaves it: its record, and the executable the record names.</summary>
    private void LaidOut(string tool, string version, long size = 4321)
    {
        var folder = ToolInstall.VersionFolder(Home, tool, version);
        Directory.CreateDirectory(Path.Combine(folder, Tools.Package, "bin"));
        File.WriteAllText(Path.Combine(folder, Tools.Package, "bin", $"{tool}.exe"), "stub");
        File.WriteAllText(Path.Combine(folder, Tools.Record), new JsonObject
        {
            ["tool"] = tool,
            ["version"] = version,
            ["platform"] = Platform,
            ["sha256"] = new string('0', 64),
            ["size"] = size,
            ["archive"] = "zip",
            ["url"] = "https://maker.example/gh.zip",
            ["lists"] = new JsonArray("the built-in list"),
            ["exe"] = $"bin/{tool}.exe",
            ["paths"] = new JsonArray("bin"),
            ["at"] = "2026-10-01T00:00:00.000Z",
        }.ToJsonString());
    }

    private static JsonElement Tool(JsonElement answered, string id) =>
        answered.GetProperty("tools").EnumerateArray().Single(tool => tool.GetProperty("tool").GetString() == id);

    private static string[] Strings(JsonElement array) => [.. array.EnumerateArray().Select(each => each.GetString()!)];

    private static JsonElement Payload(Shenora.Core.Events.EventMessage message) => JsonSerializer.SerializeToElement(message.Payload);

    private JsonObject? Written() => System.IO.File.Exists(Path.Combine(Home, Tools.FileName))
        ? JsonNode.Parse(System.IO.File.ReadAllText(Path.Combine(Home, Tools.FileName)))!.AsObject()
        : null;

    /// <summary>
    /// A home that never opened Settings → Tools runs every tool as the system's (D121 §2.3), and the list says so for
    /// each declared tool in its declared order, with the file both doors edit and the list built in. It reads files
    /// and starts nothing: a version is asked only when the page asks for it.
    /// </summary>
    [Fact]
    public async Task A_fresh_home_lists_every_declared_tool_as_the_systems_and_writes_nothing()
    {
        var answered = await AnswerAsync(Module(), "TOOLS_LIST");

        Assert.Equal(["git", "node", "pwsh", "gh", "az"], answered.GetProperty("tools").EnumerateArray().Select(t => t.GetProperty("tool").GetString()!));
        Assert.All(answered.GetProperty("tools").EnumerateArray(), tool =>
        {
            Assert.Equal("system", tool.GetProperty("way").GetString());
            Assert.False(tool.GetProperty("refused").GetBoolean());
            Assert.Empty(tool.GetProperty("downloaded").EnumerateArray());
            Assert.True(tool.GetProperty("asked").ValueKind is JsonValueKind.Null or JsonValueKind.Undefined, "nothing was asked its version");
            Assert.True(tool.GetProperty("running").ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);
        });
        Assert.Equal("Git", Tool(answered, "git").GetProperty("name").GetString());
        Assert.Equal(Path.Combine(Home, Tools.FileName), answered.GetProperty("file").GetString());
        Assert.False(answered.GetProperty("exists").GetBoolean());
        Assert.Empty(answered.GetProperty("locations").EnumerateArray());
        Assert.Equal("built in", answered.GetProperty("builtIn").GetProperty("integrity").GetString());
        Assert.False(System.IO.File.Exists(Path.Combine(Home, Tools.FileName)), "a list writes nothing");
    }

    /// <summary>
    /// What the screen's version choice is made of (§4.1): the versions downloaded, each with its size and whether it is
    /// the one in use, then what the lists offer for this machine, each with its size and the lists naming it, and the
    /// tool's source and licence. A location is listed with what vouches for it and what it names.
    /// </summary>
    [Fact]
    public async Task The_list_answers_downloaded_versions_what_the_lists_offer_and_each_location()
    {
        LaidOut("gh", "99.0.0", size: 4321);
        LaidOut("gh", "98.0.0");
        Tools.UseManaged(Home, "gh", "99.0.0");
        var address = "http://127.0.0.1:9/list.json";
        Located(address, List("gh", "99.1.0", "http://127.0.0.1:9/gh.zip", Archive));

        var answered = await AnswerAsync(Module(), "TOOLS_LIST");
        var gh = Tool(answered, "gh");

        Assert.Equal("managed", gh.GetProperty("way").GetString());
        Assert.Equal("99.0.0", gh.GetProperty("version").GetString());
        Assert.Equal(Tools.ManagedExecutable(Home, "gh", "99.0.0").File, gh.GetProperty("resolved").GetString());
        var downloaded = gh.GetProperty("downloaded").EnumerateArray().ToList();
        Assert.Equal(["99.0.0", "98.0.0"], downloaded.Select(d => d.GetProperty("version").GetString()!));
        Assert.Equal(4321, downloaded[0].GetProperty("size").GetInt64());
        Assert.True(downloaded[0].GetProperty("inUse").GetBoolean());
        Assert.False(downloaded[1].GetProperty("inUse").GetBoolean());

        var offered = gh.GetProperty("offered").EnumerateArray().Single(o => o.GetProperty("version").GetString() == "99.1.0");
        Assert.Equal(Archive.Length, offered.GetProperty("size").GetInt64());
        Assert.Equal([address], Strings(offered.GetProperty("lists")));
        Assert.Equal(["127.0.0.1"], Strings(offered.GetProperty("hosts")));
        Assert.Equal("99.1.0", gh.GetProperty("newest").GetString());
        Assert.Equal("MIT", gh.GetProperty("licence").GetProperty("id").GetString());

        var location = answered.GetProperty("locations").EnumerateArray().Single();
        Assert.Equal(address, location.GetProperty("address").GetString());
        Assert.Equal("this machine", location.GetProperty("integrity").GetString());
        Assert.True(location.GetProperty("fetched").ValueKind == JsonValueKind.String, "its copy's time is when it was fetched");
        Assert.Equal(["gh 99.1.0"], Strings(location.GetProperty("names")));
        Assert.Equal(64, location.GetProperty("sha256").GetString()!.Length);
    }

    /// <summary>Two lists that disagree on one download refuse that version and name both (§3.4 rule 3).</summary>
    [Fact]
    public async Task A_version_two_lists_disagree_on_is_listed_as_refused_and_never_started()
    {
        Located("http://127.0.0.1:9/a.json", List("gh", "99.1.0", "http://127.0.0.1:9/a.zip", Archive));
        Located("http://127.0.0.1:9/b.json", List("gh", "99.1.0", "http://127.0.0.1:9/b.zip", Archive, sha256: new string('a', 64)));
        var module = Module();

        var gh = Tool(await AnswerAsync(module, "TOOLS_LIST"), "gh");
        var refused = gh.GetProperty("refusedVersions").EnumerateArray().Single();
        Assert.Equal("99.1.0", refused.GetProperty("version").GetString());
        Assert.Equal("sha256", refused.GetProperty("field").GetString());
        Assert.DoesNotContain(gh.GetProperty("offered").EnumerateArray(), o => o.GetProperty("version").GetString() == "99.1.0");

        var refusal = await RefusalAsync(module, "TOOLS_DOWNLOAD", new { tool = "gh", version = "99.1.0" });
        Assert.Contains(Refusals.ToolDownloadRefused, refusal);
        Assert.Contains("check=conflict", refusal);
        Assert.Contains("version=99.1.0", refusal);
    }

    /// <summary>The system's way is written as the terminal's `daoris tool use … system` writes it (D50), and logged by tool.</summary>
    [Fact]
    public async Task Use_the_systems_writes_the_file_the_terminal_edits()
    {
        LaidOut("gh", "99.0.0");
        Tools.UseManaged(Home, "gh", "99.0.0");

        var answered = await AnswerAsync(Module(), "TOOLS_USE", new { tool = "gh", action = "system" });

        Assert.Equal("system", answered.GetProperty("way").GetString());
        Assert.Equal("system", Written()!["tools"]!["gh"]!["use"]!.GetValue<string>());
        Assert.Null(Written()!["tools"]!["gh"]!["version"]);
    }

    /// <summary>
    /// A named file is checked before it is written (§4.1): a path that is not whole, or holds no file, is refused, and so
    /// is a file that does not start and answer a version. Nothing is written for any of them.
    /// </summary>
    [Fact]
    public async Task A_named_file_that_is_not_there_or_does_not_answer_a_version_is_refused_and_nothing_is_written()
    {
        var module = Module();

        var relative = await RefusalAsync(module, "TOOLS_USE", new { tool = "git", action = "file", file = "bin/git.exe" });
        Assert.Contains(Refusals.ToolFileMissing, relative);
        Assert.Contains("tool=Git", relative);

        var gone = Path.Combine(Home, "nowhere", "git.exe");
        var missing = await RefusalAsync(module, "TOOLS_USE", new { tool = "git", action = "file", file = gone });
        Assert.Contains(Refusals.ToolFileMissing, missing);
        Assert.Contains($"file={gone}", missing);

        // Bytes that are no program: the system will not start them, and no version is answered.
        var junk = Path.Combine(Home, "junk.exe");
        System.IO.File.WriteAllText(junk, "not a program");
        var silent = await RefusalAsync(module, "TOOLS_USE", new { tool = "git", action = "file", file = junk });
        Assert.Contains(Refusals.ToolFileNoVersion, silent);
        Assert.Contains($"file={junk}", silent);

        Assert.Null(Written());
    }

    /// <summary>Managed at a version no list names is refused before anything starts, and nothing switches.</summary>
    [Fact]
    public async Task Managed_at_a_version_no_list_names_is_refused_and_nothing_switches()
    {
        var refusal = await RefusalAsync(Module(), "TOOLS_USE", new { tool = "gh", action = "managed", version = "99.9.9" });

        Assert.Contains(Refusals.ToolVersionUnknown, refusal);
        Assert.Contains("tool=GitHub CLI", refusal);
        Assert.Contains("version=99.9.9", refusal);
        Assert.Null(Written());
    }

    /// <summary>A version already downloaded switches at once: there is nothing to follow, so the answer is the end.</summary>
    [Fact]
    public async Task Using_a_downloaded_version_switches_in_the_answer_itself()
    {
        LaidOut("gh", "99.0.0");

        var answered = await AnswerAsync(Module(), "TOOLS_USE", new { tool = "gh", action = "managed", version = "99.0.0" });

        Assert.False(answered.GetProperty("started").GetBoolean());
        Assert.Equal(0, answered.GetProperty("ended").GetProperty("exitCode").GetInt32());
        Assert.Equal("managed", Written()!["tools"]!["gh"]!["use"]!.GetValue<string>());
        Assert.Equal("99.0.0", Written()!["tools"]!["gh"]!["version"]!.GetValue<string>());
    }

    /// <summary>
    /// A download is an action the page starts and follows (§3.6, WSR7 a): answered once it has started, its lines on the
    /// console under the tool's own key, and its end news after. Nothing switches, and the version is then downloaded.
    /// </summary>
    [Fact]
    public async Task A_download_is_answered_once_started_and_its_end_is_news_after()
    {
        using var host = new LoopbackHost();
        var url = host.Serve("/gh.zip", Archive);
        Located(host.Address + "/list.json", List("gh", "99.1.0", url, Archive));
        var module = Module();

        var answered = await AnswerAsync(module, "TOOLS_DOWNLOAD", new { tool = "gh", version = "99.1.0" });
        Assert.True(answered.GetProperty("started").GetBoolean());
        Assert.Equal("99.1.0", answered.GetProperty("version").GetString());

        await UntilAsync(() => Raised.Any(m => m.Type == "TOOLS_ENDED"));
        var ended = Payload(Raised.Single(m => m.Type == "TOOLS_ENDED"));
        Assert.Equal("gh", ended.GetProperty("Tool").GetString());
        Assert.Equal("download", ended.GetProperty("Action").GetString());
        Assert.Equal(0, ended.GetProperty("ExitCode").GetInt32());
        // Its lines are the console's, under the tool's own key, and the console says the action ended.
        var console = await AnswerAsync(module, "TAIL_SESSION", new { id = "tools:gh" });
        Assert.NotEmpty(console.GetProperty("lines").EnumerateArray());
        Assert.False(console.GetProperty("live").GetBoolean());

        Assert.Equal(["99.1.0"], ToolInstall.Downloaded(Home, "gh"));
        var gh = Tool(await AnswerAsync(module, "TOOLS_LIST"), "gh");
        Assert.Equal("system", gh.GetProperty("way").GetString());
        Assert.True(gh.GetProperty("running").ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);
    }

    /// <summary>
    /// One at a time for each tool, and the person's stop ends it (§3.6): a second start is refused while one runs, the
    /// list says what runs, a stop ends it as news leaving nothing, and a stop with nothing running says so.
    /// </summary>
    [Fact]
    public async Task A_running_download_refuses_a_second_and_the_persons_stop_ends_it_leaving_nothing()
    {
        using var host = new LoopbackHost();
        var url = host.Stall("/gh.zip");
        Located(host.Address + "/list.json", List("gh", "99.1.0", url, Archive));
        var module = Module();

        Assert.True((await AnswerAsync(module, "TOOLS_DOWNLOAD", new { tool = "gh", version = "99.1.0" })).GetProperty("started").GetBoolean());
        await host.Stalled.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var running = Tool(await AnswerAsync(module, "TOOLS_LIST"), "gh").GetProperty("running");
        Assert.Equal("download", running.GetProperty("action").GetString());
        Assert.Equal("99.1.0", running.GetProperty("version").GetString());

        var busy = await RefusalAsync(module, "TOOLS_USE", new { tool = "gh", action = "managed", version = "99.1.0" });
        Assert.Contains(Refusals.ToolBusy, busy);
        Assert.Contains("tool=GitHub CLI", busy);

        Assert.True((await AnswerAsync(module, "TOOLS_STOP", new { tool = "gh" })).GetProperty("stopped").GetBoolean());
        await UntilAsync(() => Raised.Any(m => m.Type == "TOOLS_ENDED"));
        var ended = Payload(Raised.Single(m => m.Type == "TOOLS_ENDED"));
        Assert.True(ended.GetProperty("Stopped").GetBoolean());
        Assert.False(Directory.Exists(Path.Combine(Home, Tools.Folder, "gh")), "a stop leaves nothing under tools/gh");
        Assert.False((await AnswerAsync(module, "TOOLS_STOP", new { tool = "gh" })).GetProperty("stopped").GetBoolean());
    }

    /// <summary>
    /// A delete removes a version nothing uses (§3.6): the one in use is refused, so is one not downloaded, and the
    /// refusals name the tool and the version.
    /// </summary>
    [Fact]
    public async Task A_delete_removes_a_version_nothing_uses_and_refuses_the_one_in_use_and_one_not_there()
    {
        LaidOut("gh", "99.0.0");
        LaidOut("gh", "98.0.0");
        Tools.UseManaged(Home, "gh", "99.0.0");
        var module = Module();

        var inUse = await RefusalAsync(module, "TOOLS_DELETE", new { tool = "gh", version = "99.0.0" });
        Assert.Contains(Refusals.ToolInUse, inUse);
        Assert.Contains("version=99.0.0", inUse);

        var gone = await RefusalAsync(module, "TOOLS_DELETE", new { tool = "gh", version = "97.0.0" });
        Assert.Contains(Refusals.ToolNotDownloaded, gone);
        Assert.Contains("version=97.0.0", gone);

        var deleted = await AnswerAsync(module, "TOOLS_DELETE", new { tool = "gh", version = "98.0.0" });
        Assert.Equal("98.0.0", deleted.GetProperty("version").GetString());
        Assert.Equal(["99.0.0"], ToolInstall.Downloaded(Home, "gh"));
    }

    /// <summary>
    /// The resource locations, edited on the screen in the file the terminal's `daoris tool locations` edits (D50): an
    /// address that is not one is refused naming it, one already listed adds nothing, and a removal says whether it
    /// removed anything.
    /// </summary>
    [Fact]
    public async Task A_location_is_added_and_removed_in_the_file_and_one_that_is_no_address_is_refused()
    {
        var module = Module();

        var refused = await RefusalAsync(module, "TOOLS_LOCATION", new { action = "add", address = "ftp://mirror.example/list.json" });
        Assert.Contains(Refusals.ToolLocationRefused, refused);
        Assert.Contains("address=ftp://mirror.example/list.json", refused);
        Assert.Null(Written());

        Assert.True((await AnswerAsync(module, "TOOLS_LOCATION", new { action = "add", address = "https://mirror.example/list.json" })).GetProperty("added").GetBoolean());
        Assert.False((await AnswerAsync(module, "TOOLS_LOCATION", new { action = "add", address = "https://mirror.example/list.json" })).GetProperty("added").GetBoolean());
        Assert.Equal(["https://mirror.example/list.json"], Tools.Read(Home).Locations);

        Assert.True((await AnswerAsync(module, "TOOLS_LOCATION", new { action = "remove", address = "https://mirror.example/list.json" })).GetProperty("removed").GetBoolean());
        Assert.False((await AnswerAsync(module, "TOOLS_LOCATION", new { action = "remove", address = "https://mirror.example/list.json" })).GetProperty("removed").GetBoolean());
        Assert.Empty(Tools.Read(Home).Locations);
    }

    /// <summary>
    /// Look for updates (§3.7) fetches each location on the person's press, keeps the copy of a list that reads and says
    /// what it added, and says a location that could not be fetched rather than failing the look.
    /// </summary>
    [Fact]
    public async Task A_look_fetches_each_location_keeps_what_reads_and_says_what_failed()
    {
        using var host = new LoopbackHost();
        var list = List("gh", "99.2.0", host.Address + "/gh.zip", Archive);
        Tools.AddLocation(Home, host.Serve("/list.json", System.Text.Encoding.UTF8.GetBytes(list)));
        Tools.AddLocation(Home, host.Address + "/gone.json");
        var module = Module();

        var looks = (await AnswerAsync(module, "TOOLS_LOOK")).GetProperty("looks").EnumerateArray().ToList();

        Assert.Equal(["fetched", "failed"], looks.Select(l => l.GetProperty("outcome").GetString()!));
        Assert.Equal(["gh 99.2.0"], Strings(looks[0].GetProperty("added")));
        Assert.Contains("99.2.0", Tool(await AnswerAsync(module, "TOOLS_LIST"), "gh").GetProperty("newest").GetString());
    }

    /// <summary>
    /// What a switch of git changes is asked of the git it would run (§4.1), so a target that cannot run is refused
    /// before anything is asked: a managed version nobody downloaded, or a named file that is not there.
    /// </summary>
    [Fact]
    public async Task A_git_switch_to_a_version_not_downloaded_or_a_file_not_there_is_refused_before_anything_is_asked()
    {
        var module = Module();

        var managed = await RefusalAsync(module, "TOOLS_GIT", new { way = "managed", version = "2.99.0" });
        Assert.Contains(Refusals.ToolNotDownloaded, managed);
        Assert.Contains("tool=Git", managed);
        Assert.Contains("version=2.99.0", managed);

        var gone = Path.Combine(Home, "nowhere", "git.exe");
        var file = await RefusalAsync(module, "TOOLS_GIT", new { way = "file", file = gone });
        Assert.Contains(Refusals.ToolFileMissing, file);
    }

    /// <summary>
    /// *Browse…* (§4.1) is the system's file picker, which the application hands in: its choice is the answer, the person
    /// cancelling is no file, and a host with no picker says it has none.
    /// </summary>
    [Fact]
    public async Task Browse_answers_the_pickers_choice_and_says_when_this_host_has_no_picker()
    {
        string? asked = null;
        var picking = new DriverModule(Bus, Loop(), pickFile: title => { asked = title; return @"C:\Tools\git\cmd\git.exe"; });
        var picked = await AnswerAsync(picking, "TOOLS_PICK", new { title = "Choose Git" });
        Assert.Equal(@"C:\Tools\git\cmd\git.exe", picked.GetProperty("file").GetString());
        Assert.True(picked.GetProperty("can").GetBoolean());
        Assert.Equal("Choose Git", asked);

        var cancelled = await AnswerAsync(new DriverModule(Bus, Loop(), pickFile: _ => null), "TOOLS_PICK", new { title = "Choose Git" });
        Assert.True(cancelled.GetProperty("file").ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);

        var none = await AnswerAsync(Module(), "TOOLS_PICK", new { title = "Choose Git" });
        Assert.False(none.GetProperty("can").GetBoolean());
    }

    /// <summary>A version is read off whatever the program says, as each declared tool says it (§2.1's version question).</summary>
    [Theory]
    [InlineData("git version 2.51.0.windows.1\n", "2.51.0")]
    [InlineData("v22.11.0\n", "22.11.0")]
    [InlineData("PowerShell 7.4.6\n", "7.4.6")]
    [InlineData("gh version 2.63.0 (2024-11-27)\nhttps://github.com/cli/cli/releases/tag/v2.63.0\n", "2.63.0")]
    [InlineData("{\n  \"azure-cli\": \"2.67.0\",\n  \"azure-cli-core\": \"2.67.0\"\n}\n", "2.67.0")]
    [InlineData("usage: something\n", null)]
    [InlineData("", null)]
    public void A_version_is_the_first_dotted_number_the_program_says(string said, string? version)
    {
        Assert.Equal(version, ToolQuestions.VersionIn(said));
    }

    /// <summary>
    /// What makes two gits read one checkout differently (§4.1): the four keys, compared between each git's own
    /// `config --system --list`; a key one sets and the other leaves unset differs, and every other key is not looked at.
    /// </summary>
    [Fact]
    public void A_git_switch_names_only_the_checkout_keys_the_two_gits_read_differently()
    {
        var now = ToolQuestions.ConfigIn("diff.astextplain.textconv=astextplain\ncore.autocrlf=true\ncore.symlinks=false\nCORE.LONGPATHS=true\nhttp.sslbackend=schannel\n");
        var then = ToolQuestions.ConfigIn("core.autocrlf=input\ncore.longpaths=true\ncore.fscache=true\n");

        var differs = ToolQuestions.Differences(now, then);

        Assert.Equal(["core.autocrlf", "core.symlinks"], differs.Select(d => d.Key));
        Assert.Equal(("true", "input"), (differs[0].Now, differs[0].Then));
        Assert.Equal(("false", (string?)null), (differs[1].Now, differs[1].Then));
        Assert.Equal(["core.autocrlf", "core.eol", "core.symlinks", "core.longpaths"], ToolQuestions.CheckoutKeys);
    }
}
