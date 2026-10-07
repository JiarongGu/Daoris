using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 <b>Update does what it says</b> (USE1a). Update on a pinned <c>claude-code-acp</c> answered
/// "that agent declares no updater": the door is an npm package Daoris PINS, and the action only knew
/// a tool's own updater. Now a pinned door with a package or a channel MOVES ITS PIN to the newest
/// release, an unpinned door with its own updater runs it, and a door with neither offers no Update
/// at all. The CLI's <c>agent update</c> is the twin, held by the same cases in
/// <c>toolchain.test.ts</c>.
/// </summary>
/// <remarks>
/// <b>No test here downloads anything.</b> npm is a stand-in script that answers <c>view</c> and lays
/// down a shim for <c>install</c>, and the channel is a transport over a table.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class DoorUpdateTests : IDisposable
{
    private const string AcpPackage = "@agentclientprotocol/claude-agent-acp";

    // Scratch in the repository's own gitignored `_fixtures/`, never OS temp.
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "door-update", Guid.NewGuid().ToString("N")[..8]);

    private readonly List<string> _said = [];

    private readonly List<string> _pinned = [];

    public DoorUpdateTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static HarnessToolchain Built(string name) => AdapterSet.Built().Resolve(name).Toolchain!;

    // ——— Which Update a door has, which is what the roster tells the page.

    [Fact]
    public void A_door_s_update_is_its_pin_when_pinned_its_own_updater_when_not_and_otherwise_none()
    {
        var npmDoor = new HarnessToolchain(["x"], ["--version"], Package: "x-package");
        var channelDoor = new HarnessToolchain(["x"], ["--version"], Channel: ClaudeReleases.Channel);
        var ownUpdater = new HarnessToolchain(["x"], ["--version"], UpdateArguments: ["update"]);
        var neither = new HarnessToolchain(["x"], ["--version"]);

        Assert.Equal("pin", HarnessActions.UpdateOf(npmDoor, "1.0.0"));
        Assert.Equal("pin", HarnessActions.UpdateOf(channelDoor, "2.1.281"));
        Assert.Equal("tool", HarnessActions.UpdateOf(ownUpdater, pinned: null));
        Assert.Null(HarnessActions.UpdateOf(npmDoor, pinned: null));
        Assert.Null(HarnessActions.UpdateOf(neither, pinned: null));
        // Pinned with nowhere to find a newer version: its own updater would move a copy sessions do
        // not run, so there is no Update to offer.
        Assert.Null(HarnessActions.UpdateOf(ownUpdater, "1.0.0"));

        // The built-in doors, as a person meets them.
        Assert.Equal("tool", HarnessActions.UpdateOf(Built("claude-code"), pinned: null));
        Assert.Equal("pin", HarnessActions.UpdateOf(Built("claude-code"), "2.1.281"));
        Assert.Null(HarnessActions.UpdateOf(Built("claude-code-acp"), pinned: null));
        Assert.Equal("pin", HarnessActions.UpdateOf(Built("claude-code-acp"), "0.79.0"));
        Assert.Null(HarnessActions.UpdateOf(Built("dsh"), pinned: null));
    }

    // ——— A pinned npm door.

    [Fact]
    public async Task A_pinned_package_door_resolves_the_newest_and_pins_that_exact_version()
    {
        Installed("claude-code-acp", "0.79.0");
        var npm = StandInNpm("console.log('0.84.0');");

        var code = await UpdateAsync(Built("claude-code-acp"), "claude-code-acp", "0.79.0", npm: npm);

        Assert.Equal(0, code);
        Assert.Equal(["0.84.0"], _pinned);
        Assert.Contains(_said, line => line.Contains("0.79.0 → 0.84.0"));
        Assert.NotNull(HarnessSettings.ManagedBinary(_home, "claude-code-acp", "0.84.0", ["claude-agent-acp"]));
        // Resolved first, then installed at the CONCRETE version — never `@latest`.
        Assert.Equal(
            [
                $"view {AcpPackage} version",
                $"install --prefix {HarnessSettings.ManagedHome(_home, "claude-code-acp", "0.84.0")} {AcpPackage}@0.84.0",
            ],
            Asked());
    }

    [Fact]
    public async Task A_pin_already_at_the_newest_fetches_nothing_and_stays()
    {
        Installed("claude-code-acp", "0.79.0");
        var npm = StandInNpm("console.log('0.79.0');");

        var code = await UpdateAsync(Built("claude-code-acp"), "claude-code-acp", "0.79.0", npm: npm);

        Assert.Equal(0, code);
        Assert.Empty(_pinned);
        Assert.Contains(_said, line => line.Contains("already the newest"));
        Assert.Equal([$"view {AcpPackage} version"], Asked());
    }

    [Fact]
    public async Task A_newest_release_older_than_the_pin_never_moves_it_backwards()
    {
        Installed("claude-code-acp", "0.85.0");
        var npm = StandInNpm("console.log('0.84.0');");

        var code = await UpdateAsync(Built("claude-code-acp"), "claude-code-acp", "0.85.0", npm: npm);

        Assert.Equal(0, code);
        Assert.Empty(_pinned);
        Assert.Contains(_said, line => line.Contains("newer than"));
        Assert.Single(Asked());
    }

    /// <summary>npm that fails, or answers something that is not one version, is a sentence — the pin stays.</summary>
    [Theory]
    [InlineData("console.error('npm error code E404'); process.exit(1);")]
    [InlineData("console.log('{ weird: true }');")]
    [InlineData("console.log('0.83.0'); console.log('0.84.0');")]
    public async Task An_update_that_cannot_learn_the_newest_version_says_so_and_pins_nothing(string view)
    {
        Installed("claude-code-acp", "0.79.0");
        var npm = StandInNpm(view);

        var error = await Assert.ThrowsAsync<DriverException>(
            () => UpdateAsync(Built("claude-code-acp"), "claude-code-acp", "0.79.0", npm: npm));

        Assert.Contains("Nothing was fetched or pinned", error.Message);
        Assert.Contains("0.79.0", error.Message);
        Assert.Empty(_pinned);
        Assert.Equal([$"view {AcpPackage} version"], Asked());
    }

    // ——— A pinned channel door.

    /// <summary>
    /// The channel's <c>latest</c> names the version; the pin then goes through the verified install
    /// exactly as a typed one would. Here that version is already installed where Daoris keeps it —
    /// the proof it verified — so the move downloads nothing and still writes the pin.
    /// </summary>
    [Fact]
    public async Task A_pinned_channel_door_pins_the_version_its_channel_names_newest()
    {
        Installed("claude-code", "2.1.270", vendor: true);
        Installed("claude-code", "2.1.281", vendor: true);
        var transport = new Served(new() { [ClaudeReleases.Latest] = "2.1.281\n"u8.ToArray() });

        var code = await UpdateAsync(Built("claude-code"), "claude-code", "2.1.270", transport: transport);

        Assert.Equal(0, code);
        Assert.Equal(["2.1.281"], _pinned);
        Assert.Contains(_said, line => line.Contains("2.1.270 → 2.1.281"));
        Assert.Equal([$"{ClaudeReleases.Base}/latest"], transport.Asked);
    }

    /// <summary>
    /// 🔴 The pointer only chooses a version and vouches for nothing: the vendor's real signed manifest
    /// verifies, a binary that is not the one it names is refused, and nothing is pinned.
    /// </summary>
    [Fact]
    public async Task A_channel_move_goes_through_the_verified_install_and_a_refusal_pins_nothing()
    {
        Installed("claude-code", "2.1.270", vendor: true);
        var vendor = VendorFixtures();
        var transport = new Served(new()
        {
            [ClaudeReleases.Latest] = "2.1.281"u8.ToArray(),
            [$"{ClaudeReleases.Base}/2.1.281/manifest.json"] =
                File.ReadAllBytes(Path.Combine(vendor, "claude-code", "2.1.281", "manifest.json")),
            [$"{ClaudeReleases.Base}/2.1.281/manifest.json.sig"] =
                File.ReadAllBytes(Path.Combine(vendor, "claude-code", "2.1.281", "manifest.json.sig")),
            [$"{ClaudeReleases.Base}/2.1.281/{ClaudeReleases.Current()}/{(OperatingSystem.IsWindows() ? "claude.exe" : "claude")}"] =
                "not the vendor's binary"u8.ToArray(),
        });

        var error = await Assert.ThrowsAsync<DriverException>(
            () => UpdateAsync(Built("claude-code"), "claude-code", "2.1.270", transport: transport));

        Assert.Contains("SHA-256", error.Message);
        Assert.Empty(_pinned);
        Assert.Equal(4, transport.Asked.Count);
        Assert.Null(HarnessSettings.ManagedBinary(_home, "claude-code", "2.1.281", ["claude"]));
    }

    [Theory]
    [InlineData("<html>maintenance</html>")]
    [InlineData("latest")]
    [InlineData("2.1")]
    [InlineData("")]
    public async Task A_channel_pointer_that_names_no_version_is_refused_and_pins_nothing(string answered)
    {
        Installed("claude-code", "2.1.270", vendor: true);
        var transport = new Served(new() { [ClaudeReleases.Latest] = Encoding.UTF8.GetBytes(answered) });

        var error = await Assert.ThrowsAsync<DriverException>(
            () => UpdateAsync(Built("claude-code"), "claude-code", "2.1.270", transport: transport));

        Assert.Contains("not a version", error.Message);
        Assert.Contains("nothing was fetched or pinned", error.Message);
        Assert.Empty(_pinned);
        Assert.Single(transport.Asked);
    }

    [Fact]
    public async Task The_channel_s_newest_is_read_from_its_latest_pointer()
    {
        var transport = new Served(new() { [ClaudeReleases.Latest] = "2.1.281\n"u8.ToArray() });

        Assert.Equal("2.1.281", await ClaudeReleases.LatestAsync(CancellationToken.None, transport));
        Assert.Equal("https://downloads.claude.ai/claude-code-releases/latest", ClaudeReleases.Latest);

        var nothing = await Assert.ThrowsAsync<DriverException>(
            () => ClaudeReleases.LatestAsync(CancellationToken.None, new Served([])));
        Assert.Contains("nothing answered at", nothing.Message);
    }

    // ——— An unpinned door, and a door with neither.

    [Fact]
    public async Task An_unpinned_door_with_its_own_updater_runs_it()
    {
        var script = Path.Combine(_home, "own-updater.mjs");
        var marker = Path.Combine(_home, "updated.txt");
        File.WriteAllText(script,
            $"import {{ writeFileSync }} from 'node:fs';\nwriteFileSync({JsonSerializer.Serialize(marker)}, process.argv.slice(2).join(' '));\n");
        var toolchain = new HarnessToolchain(["node", script], ["--version"], UpdateArguments: ["update"]);

        var code = await UpdateAsync(toolchain, "own", pinned: null, ownCommand: true);

        Assert.Equal(0, code);
        Assert.Equal("update", File.ReadAllText(marker));
        Assert.Empty(_pinned);
    }

    [Fact]
    public async Task An_unpinned_door_with_no_updater_of_its_own_is_refused_as_before()
    {
        var error = await Assert.ThrowsAsync<DriverException>(
            () => UpdateAsync(Built("claude-code-acp"), "claude-code-acp", pinned: null));

        Assert.Contains("declares no updater", error.Message);
    }

    /// <summary>What <c>npm view &lt;package&gt; version</c> answers, read defensively — the CLI's <c>versionFromNpm</c> cases.</summary>
    [Theory]
    [InlineData("0.84.0\n", "0.84.0")]
    [InlineData("npm warn config production Use `--omit=dev` instead.\n0.84.0\n", "0.84.0")]
    [InlineData("'0.84.0'\n", "0.84.0")]
    [InlineData("1.0.0-beta.2\n", "1.0.0-beta.2")]
    [InlineData("", null)]
    [InlineData("{ weird: true }", null)]
    [InlineData("0.83.0\n0.84.0\n", null)]
    [InlineData("latest", null)]
    public void Npm_s_answer_is_one_version_or_none(string output, string? version)
    {
        Assert.Equal(version, HarnessActions.VersionFromNpm(output.Split('\n')));
    }

    // ——— Helpers.

    /// <summary>
    /// What an updater would run as, unless a test names it: a no-op that exits 7. 🔴 Measured while
    /// proving these tests fail: a regression that fell back to the tool's own updater ran this
    /// machine's real <c>claude update</c>. Configured like this, it fails the test and touches nothing.
    /// </summary>
    private static readonly string[] Harmless = ["node", "-e", "process.exit(7)"];

    private Task<int> UpdateAsync(
        HarnessToolchain toolchain, string harness, string? pinned,
        IReadOnlyList<string>? npm = null, HttpMessageHandler? transport = null, bool ownCommand = false) =>
        HarnessActions.UpdateAsync(
            toolchain, ownCommand ? null : Harmless, _home, harness, pinned, _pinned.Add, _said.Add,
            CancellationToken.None, started: null, transport: transport, npm: npm);

    /// <summary>An install at a version, in npm's layout or the vendor's.</summary>
    private void Installed(string harness, string version, bool vendor = false)
    {
        var binary = Built(harness).Binary[0];
        var where = HarnessSettings.ManagedHome(_home, harness, version);
        var bin = vendor ? Path.Combine(where, "bin") : Path.Combine(where, "node_modules", ".bin");
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, vendor && OperatingSystem.IsWindows() ? binary + ".exe" : binary), "");
    }

    private string AskedLog => Path.Combine(_home, "npm-asked.log");

    private string[] Asked() =>
        File.Exists(AskedLog) ? File.ReadAllLines(AskedLog).Where(line => line.Length > 0).ToArray() : [];

    /// <summary>
    /// npm's stand-in: <c>view</c> runs the body given, and <c>install --prefix &lt;dir&gt; &lt;spec&gt;</c>
    /// lays down the adapter's shim where npm's would land. Every call is logged, in order.
    /// </summary>
    private IReadOnlyList<string> StandInNpm(string view)
    {
        var script = Path.Combine(_home, "npm-stand-in.mjs");
        File.WriteAllText(script, $$"""
            import { appendFileSync, mkdirSync, writeFileSync } from 'node:fs';
            import { join } from 'node:path';
            const [verb, ...rest] = process.argv.slice(2);
            appendFileSync({{JsonSerializer.Serialize(AskedLog)}}, [verb, ...rest].join(' ') + '\n');
            if (verb === 'view') { {{view}} }
            if (verb === 'install') {
              const bin = join(rest[rest.indexOf('--prefix') + 1], 'node_modules', '.bin');
              mkdirSync(bin, { recursive: true });
              writeFileSync(join(bin, 'claude-agent-acp'), '');
            }
            """);
        return ["node", script];
    }

    private static string VendorFixtures() => Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Cli", "test", "fixtures", "vendor");

    /// <summary>A transport over a table of URLs, and the URLs it was asked for, in order.</summary>
    private sealed class Served(Dictionary<string, byte[]> files) : HttpMessageHandler
    {
        public List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();
            Asked.Add(url);
            return Task.FromResult(files.TryGetValue(url, out var body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
