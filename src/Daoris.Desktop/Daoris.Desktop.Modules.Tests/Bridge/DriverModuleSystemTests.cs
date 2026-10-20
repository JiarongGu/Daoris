using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// This Daoris's read over the bridge (`SystemRoutes.cs`, WIRING1b, the this-Daoris design §6): the driver library's one
/// reader as the window answers it, the window's own facts from its loop and supervisor, a null kept as a field, sizes
/// measured only when asked, and no secret in the answer. The account's and the machine's variables are this test's own, so
/// nothing of the machine running it is read.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class DriverModuleSystemTests : DriverModuleBridge
{
    private const string Secret = "a-service-key-never-shown";

    /// <summary>🔴 A scratch tool home, handed to every world: no test here reads or touches the machine's own.</summary>
    private string ToolHome => Path.Combine(Home, "scratch-person", ".claude");

    private DriverModule WiredModule(WiringSizes sizes, IReadOnlyList<ToolOrigin>? origins = null) => new(Bus, Loop())
    {
        WiringWorlds = world => world with
        {
            Account = name => name == "DAORIS_SERVICE_KEY" ? Secret + "-account" : null,
            Machine = _ => null,
            Sizes = sizes,
            ToolHome = ToolHome,
            ToolHomeFrom = "process",
            ToolOrigins = origins,
        },
    };

    /// <summary>A project folder in the scratch tool home, named as Claude Code names the folder a session ran in, written long ago.</summary>
    private string Project(string ranIn)
    {
        var folder = Path.Combine(ToolHome, "projects", ToolHomeLeftovers.Flatten(ranIn));
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "s.jsonl");
        File.WriteAllBytes(file, new byte[7]);
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddHours(-3));
        Directory.SetLastWriteTimeUtc(folder, DateTime.UtcNow.AddHours(-3));
        return folder;
    }

    [Fact]
    public async Task The_wiring_lists_the_tool_home_s_folders_Daoris_made_and_the_route_removes_nothing()
    {
        var tree = DaorisHome.Entry(Home, HomeEntry.Trees, "main", "repo", "s-1");
        var mine = Project(tree);
        var theirs = Project(Path.Combine(Path.GetTempPath(), "somebody", "app"));
        var module = WiredModule(new WiringSizes(), [new ToolOrigin(tree, "tree", null)]);

        var listed = (await AnswerAsync(module, "SYSTEM_WIRING")).GetProperty("toolHome");
        Assert.Equal(ToolHome, listed.GetProperty("path").GetString());
        Assert.Equal("process", listed.GetProperty("from").GetString());
        var row = Assert.Single(listed.GetProperty("folders").EnumerateArray());
        Assert.Equal(Path.GetFileName(mine), row.GetProperty("name").GetString());
        // Listed by name alone: no size until a measure is asked, and its why is a field, not an absent one.
        Assert.Equal(JsonValueKind.Null, row.GetProperty("bytes").ValueKind);

        // Read-only: nothing here removes or moves a folder, the person's own or Daoris's.
        Assert.True(Directory.Exists(mine));
        Assert.True(Directory.Exists(theirs));
        Assert.DoesNotContain("SYSTEM_TOOL_CLEAR", DriverModule.Routes);
    }

    [Fact]
    public async Task With_no_service_to_read_the_records_from_nothing_is_claimed_and_the_list_says_why()
    {
        Project(HelpRoom.PathOf(Home));
        var module = WiredModule(new WiringSizes());

        var listed = (await AnswerAsync(module, "SYSTEM_WIRING")).GetProperty("toolHome");

        Assert.Empty(listed.GetProperty("folders").EnumerateArray());
        Assert.Equal("records-unread", listed.GetProperty("why").GetString());
    }

    [Fact]
    public async Task The_window_answers_its_wiring_with_what_only_it_knows_and_each_unread_fact_as_a_null_field()
    {
        var wiring = await AnswerAsync(WiredModule(new WiringSizes()), "SYSTEM_WIRING");

        Assert.Equal(1, wiring.GetProperty("schema").GetInt32());
        Assert.Equal("window", wiring.GetProperty("reader").GetString());
        Assert.Equal(Home, wiring.GetProperty("folder").GetProperty("path").GetString());
        // This loop's supervisor has neither started nor adopted a host.
        Assert.Equal("none", wiring.GetProperty("service").GetProperty("held").GetString());
        // 🔴 A null survives the bridge as a field with its why, which the page reads (design §6): the bridge leaves a null out
        // of an object it serializes, so the answer is handed as the reader's own JSON.
        var bytes = wiring.GetProperty("folders").EnumerateArray().First(row => row.GetProperty("id").GetString() == "data")
            .GetProperty("bytes");
        Assert.Equal(JsonValueKind.Null, bytes.ValueKind);
        Assert.False(wiring.GetProperty("measuring").GetBoolean());
    }

    [Fact]
    public async Task A_measure_asked_starts_in_the_background_and_the_next_read_has_the_sizes()
    {
        File.WriteAllText(DriverConfigPath, "{}");
        var sizes = new WiringSizes();
        var module = WiredModule(sizes);

        var asked = await AnswerAsync(module, "SYSTEM_WIRING", new { measure = true });
        await sizes.Finished(Home);
        var read = await AnswerAsync(module, "SYSTEM_WIRING");

        Assert.True(asked.GetProperty("measuring").GetBoolean() || asked.GetProperty("measured").ValueKind != JsonValueKind.Null);
        var data = read.GetProperty("folders").EnumerateArray().First(row => row.GetProperty("id").GetString() == "data");
        Assert.Equal(2, data.GetProperty("bytes").GetInt64());
    }

    /// <summary>
    /// FABRIC-EVENT5b: a walk that started and a walk that ended each tell the windows <c>wiring</c>, in place of the page's 2 s read: a
    /// size lands once, as the walk's reading is kept, and the end is told after the walk has left the home, so the read that follows
    /// says it is no longer measuring. A measure asked while one is under way starts nothing and tells nothing more.
    /// </summary>
    [Fact]
    public async Task A_walk_s_start_and_its_end_are_told_as_wiring()
    {
        File.WriteAllText(DriverConfigPath, "{}");
        var sizes = new WiringSizes();
        var module = WiredModule(sizes);

        await AnswerAsync(module, "SYSTEM_WIRING", new { measure = true });
        await sizes.Finished(Home);

        await UntilAsync(() => Raised.Count(m => m.Type == StatePublisher.Type) == 2);
        Assert.All(Raised.Where(m => m.Type == StatePublisher.Type), told =>
            Assert.Equal("""{"scopes":["wiring"]}""", JsonSerializer.Serialize(told.Payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })));
        Assert.False(sizes.Measuring(Home));

        // A plain read tells nothing: the open walks no folder.
        await AnswerAsync(module, "SYSTEM_WIRING");
        await Task.Delay(100);
        Assert.Equal(2, Raised.Count(m => m.Type == StatePublisher.Type));
    }

    /// <summary>
    /// PROVED1 (the this-Daoris design §3.2): a host this window started and handed the person key proves it holds it on the
    /// readiness probe, and the window's answer says <c>proved: true</c>; one that answers no proof says <c>false</c>. The
    /// key itself is in no answer.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_started_host_that_proves_the_person_key_is_said_proved(bool proves)
    {
        var key = System.Buffers.Text.Base64Url.EncodeToString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var (url, location) = KeyedStandIn(proves);
        using var supervisor = new HostSupervisor(url, () => location) { PersonKey = key, StopWithin = TimeSpan.FromSeconds(20) };

        Assert.True(await supervisor.EnsureAsync(), supervisor.Trouble);
        var module = new DriverModule(Bus, new DriverLoop(Bus, supervisor, url)) { WiringWorlds = WiredModule(new WiringSizes()).WiringWorlds };
        var response = await AskAsync(module, "SYSTEM_WIRING");
        var service = (await AnswerAsync(module, "SYSTEM_WIRING")).GetProperty("service");

        Assert.Equal(proves, supervisor.Proved);
        Assert.Equal("hosted", service.GetProperty("held").GetString());
        Assert.Equal(proves, service.GetProperty("proved").GetBoolean());
        Assert.DoesNotContain(key, JsonSerializer.Serialize(response), StringComparison.Ordinal);
        Assert.Equal(HostStop.Exited, supervisor.Stop());
    }

    /// <summary>
    /// A stand-in for the HTTP host as PERSONDOOR1a reads its key: asked by the variable, it reads the key on its input's
    /// first line before it listens, answers <c>proof</c> for a <c>prove</c> nonce where <paramref name="proves"/>, and stops
    /// when its input ends.
    /// </summary>
    private (string Url, HostLocation Location) KeyedStandIn(bool proves)
    {
        var folder = Path.Combine(Home, "stand-in-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(folder);
        var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        probe.Start();
        var port = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        File.WriteAllText(Path.Combine(folder, "host.mjs"), $$"""
            import { createServer } from 'node:http';
            import { createHmac } from 'node:crypto';
            import { createInterface } from 'node:readline';
            const lines = createInterface({ input: process.stdin });
            lines.on('close', () => process.exit(0));
            const asked = process.env.{{HostSupervisor.PersonKeyOnInput}} === '1';
            const serve = (key) => createServer((request, response) => {
              const nonce = new URL(request.url, 'http://stand-in').searchParams.get('prove');
              const status = { semantic: false, tier: 'lexical only', note: null };
              if ({{(proves ? "true" : "false")}} && key && nonce) status.proof = createHmac('sha256', key).update(nonce, 'utf8').digest('base64url');
              response.writeHead(200, { 'content-type': 'application/json' });
              response.end(JSON.stringify(status));
            }).listen({{port}}, '127.0.0.1');
            if (asked) lines.once('line', (line) => serve(Buffer.from(line.trim(), 'base64url')));
            else serve(null);
            """);
        string executable;
        if (OperatingSystem.IsWindows())
        {
            executable = Path.Combine(folder, "host.cmd");
            File.WriteAllText(executable, "@echo off\r\nnode \"%~dp0host.mjs\"\r\n");
        }
        else
        {
            executable = Path.Combine(folder, "host");
            File.WriteAllText(executable, "#!/bin/sh\nexec node \"$(dirname \"$0\")/host.mjs\"\n");
            File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return ($"http://127.0.0.1:{port}", new HostLocation(executable, folder));
    }

    [Fact]
    public async Task No_key_is_in_the_windows_answer()
    {
        Redirect("DAORIS_SERVICE_KEY", Secret);

        var response = await AskAsync(WiredModule(new WiringSizes()), "SYSTEM_WIRING");
        var text = JsonSerializer.Serialize(response);

        Assert.DoesNotContain(Secret, text, StringComparison.Ordinal);
        var key = (await AnswerAsync(WiredModule(new WiringSizes()), "SYSTEM_WIRING")).GetProperty("variables").EnumerateArray()
            .Single(row => row.GetProperty("name").GetString() == "DAORIS_SERVICE_KEY");
        Assert.True(key.GetProperty("set").GetBoolean());
        Assert.Equal(JsonValueKind.Null, key.GetProperty("value").ValueKind);
        Assert.True(key.GetProperty("accountSet").GetBoolean());
    }
}
