using System.IO.Compression;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A managed version, downloaded, verified, unpacked and laid out (TOOLS4, D121; the tools design §3.6, §3.7, §5):
/// the driver's half of a TWIN with the CLI's <c>toolinstall.ts</c>, whose <c>toolinstall.test.ts</c> holds the same
/// tables, row for row and in the same order. They share no code; a row changed here is changed there, in the same
/// commit.
/// </summary>
/// <remarks>
/// <para>🔴 <b>The CLI reads these theories.</b> <c>toolinstall.test.ts</c>'s <i>the driver's tables are these
/// tables</i> parses each <c>[InlineData]</c> row here and holds it to its own table, cell for cell and in order, so
/// a row changed on one side alone fails <c>npm run verify</c>. Keep each row on one line, its cells literals.</para>
/// <para>Every archive is built in the test from a row's words (<see cref="Archive"/>, the CLI's
/// <c>_archives.ts</c>), and nothing here reaches a network: a download is served by a stand-in that answers as a
/// host would.</para>
/// </remarks>
public sealed class ToolInstallTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-toolinstall-" + Guid.NewGuid().ToString("N")[..8]);

    public ToolInstallTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    // ── An archive's refusals (§3.6). The CLI's `an archive unpacks, or is refused by its check…` ──────────────

    [Theory]
    [InlineData("a zip that unpacks", "zip", "bin/gh.exe=gh; bin/README=read me; docs/", "", "bin/gh.exe", null)]
    [InlineData("a zip of stored entries", "zip", "bin/gh.exe=gh!stored; docs/", "", "bin/gh.exe", null)]
    [InlineData("a zip written as zip64", "zip", "bin/gh.exe=gh; docs/", "zip64", "bin/gh.exe", null)]
    [InlineData("a zip name from the root", "zip", "bin/gh.exe=gh; /gh.exe=x", "", "bin/gh.exe", "absolute")]
    [InlineData("a zip name on a drive", "zip", "C:/gh.exe=x", "", "bin/gh.exe", "absolute")]
    [InlineData("a zip name that climbs out", "zip", "bin/gh.exe=gh; ../gh.exe=x", "", "bin/gh.exe", "outside")]
    [InlineData("a zip name that climbs out partway", "zip", "bin/../../gh.exe=x", "", "bin/gh.exe", "outside")]
    [InlineData("a zip name that climbs out by a backslash", "zip", "..\\gh.exe=x", "", "bin/gh.exe", "outside")]
    [InlineData("a zip stream name", "zip", "bin/gh.exe:hidden=x", "", "bin/gh.exe", "stream")]
    [InlineData("a zip symbolic link", "zip", "bin/gh.exe=/usr/bin/gh!symlink", "", "bin/gh.exe", "link")]
    [InlineData("a zip entry encrypted", "zip", "bin/gh.exe=gh!encrypted", "", "bin/gh.exe", "encrypted")]
    [InlineData("a zip method other than stored or deflate", "zip", "bin/gh.exe=gh!method12", "", "bin/gh.exe", "method")]
    [InlineData("a zip entry that fails its checksum", "zip", "bin/gh.exe=gh!badcrc", "", "bin/gh.exe", "checksum")]
    [InlineData("a zip with no end record", "zip", "bin/gh.exe=gh", "cut-end", "bin/gh.exe", "truncated")]
    [InlineData("a zip cut in half", "zip", "bin/gh.exe=gh; bin/README=read me", "cut-data", "bin/gh.exe", "truncated")]
    [InlineData("a zip without the executable", "zip", "bin/gh.exe=gh", "", "bin/gh2.exe", "exe")]
    [InlineData("a zip whose executable is a folder", "zip", "bin/gh.exe/", "", "bin/gh.exe", "exe")]
    [InlineData("a tar.gz that unpacks", "tar.gz", "bin/gh=gh; bin/README=read me; docs/", "", "bin/gh", null)]
    [InlineData("a tar.gz whose names are in extended headers", "tar.gz", "bin/gh=gh; docs/", "pax", "bin/gh", null)]
    [InlineData("a tar.gz name from the root", "tar.gz", "/gh=x", "", "bin/gh", "absolute")]
    [InlineData("a tar.gz name that climbs out", "tar.gz", "bin/gh=gh; ../gh=x", "", "bin/gh", "outside")]
    [InlineData("a tar.gz stream name", "tar.gz", "bin/gh:hidden=x", "", "bin/gh", "stream")]
    [InlineData("a tar.gz symbolic link", "tar.gz", "bin/gh=/usr/bin/gh!symlink", "", "bin/gh", "link")]
    [InlineData("a tar.gz hard link", "tar.gz", "bin/gh=bin/other!hardlink", "", "bin/gh", "link")]
    [InlineData("a tar.gz entry neither file nor folder", "tar.gz", "bin/gh=x!fifo", "", "bin/gh", "entry")]
    [InlineData("a tar.gz header that fails its checksum", "tar.gz", "bin/gh=gh!badcrc", "", "bin/gh", "checksum")]
    [InlineData("a tar.gz with no end", "tar.gz", "bin/gh=gh", "cut-end", "bin/gh", "truncated")]
    [InlineData("a tar.gz cut inside an entry", "tar.gz", "bin/gh=gh", "cut-data", "bin/gh", "truncated")]
    [InlineData("a tar.gz whose gzip is cut before its trailer", "tar.gz", "bin/gh=gh", "cut-gzip", "bin/gh", "truncated")]
    [InlineData("a tar.gz without the executable", "tar.gz", "bin/gh=gh", "", "bin/gh2", "exe")]
    [InlineData("an archive kind nobody unpacks", "7z", "bin/gh.exe=gh", "", "bin/gh.exe", "archive")]
    public void An_archive_unpacks_as_the_cli_unpacks_it(string name, string kind, string entries, string shape, string exe, string? check)
    {
        var archive = Path.Combine(_root, "download");
        File.WriteAllBytes(archive, Archive.Build(kind, entries, shape));
        // Two folders down, so a name that climbs out one or two lands somewhere this can look.
        var into = Path.Combine(_root, "a", "b", "package");

        if (check is null)
        {
            var file = ToolInstall.Unpack(archive, kind, into, exe);
            Assert.Equal(Path.Combine([into, .. exe.Split('/')]), file);
            Assert.Equal("gh", File.ReadAllText(file));
        }
        else
        {
            var refused = Assert.Throws<ToolRefusal>(() => ToolInstall.Unpack(archive, kind, into, exe));
            Assert.True(check == refused.Check, $"{name}: {refused.Check} — {refused.Message}");
        }

        foreach (var outside in new[]
        {
            Path.Combine(_root, "gh.exe"), Path.Combine(_root, "a", "gh.exe"), Path.Combine(_root, "a", "b", "gh.exe"),
            Path.Combine(_root, "a", "b", "gh"),
        })
        {
            Assert.False(File.Exists(outside), $"{name}: nothing lands outside the folder ({outside})");
        }
    }

    [Fact]
    public void A_zip_unpacks_whole_top_folder_included_and_what_may_run_stays_runnable()
    {
        var archive = Path.Combine(_root, "download");
        File.WriteAllBytes(archive, Archive.Build("zip", "gh_2.62.0/; gh_2.62.0/bin/gh.exe=gh; gh_2.62.0/LICENSE=MIT", ""));

        var into = Path.Combine(_root, "package");
        var file = ToolInstall.Unpack(archive, "zip", into, "gh_2.62.0/bin/gh.exe");
        Assert.Equal(Path.Combine(into, "gh_2.62.0", "bin", "gh.exe"), file);
        Assert.Equal("MIT", File.ReadAllText(Path.Combine(into, "gh_2.62.0", "LICENSE")));
        if (!OperatingSystem.IsWindows()) Assert.True((File.GetUnixFileMode(file) & UnixFileMode.UserExecute) != 0);
    }

    [Fact]
    public void A_refusal_says_what_it_found_in_a_sentence()
    {
        var archive = Path.Combine(_root, "download");
        File.WriteAllBytes(archive, Archive.Build("zip", "bin/gh.exe=gh!badcrc", ""));

        var crc = Assert.Throws<ToolRefusal>(() => ToolInstall.Unpack(archive, "zip", Path.Combine(_root, "package"), "bin/gh.exe"));
        Assert.Matches(@"^the archive's `bin/gh\.exe` fails its own check — 2 bytes with CRC-32 [0-9a-f]{8}, where it states 2 with [0-9a-f]{8}\z", crc.Message);
        var kind = Assert.Throws<ToolRefusal>(() => ToolInstall.Unpack(archive, "rar", Path.Combine(_root, "package"), "bin/gh.exe"));
        Assert.Contains("`rar` is not an archive this build unpacks — zip or tar.gz", kind.Message);
    }

    // ── A scratch home, its lists, and a host that answers as a stand-in ───────────────────────────────────────

    /// <summary>The placeholder a row writes where a whole path goes; each side spells its own.</summary>
    private const string Whole = "WHOLE";

    private const string Lists = "gh 2.62.0 win-x64 a m.example; gh 2.63.0 win-x64 b m.example";

    private string Home => Path.Combine(_root, "data");

    private string WholeFile => Path.Combine(_root, "bin", OperatingSystem.IsWindows() ? "gh.exe" : "gh");

    private string ToolsFile => Path.Combine(Home, Tools.FileName);

    /// <summary>A downloaded version as the resolution finds it: its record, naming an executable that is there.</summary>
    private void Downloaded(string tool, string version)
    {
        var folder = ToolInstall.VersionFolder(Home, tool, version);
        Directory.CreateDirectory(Path.Combine(folder, Tools.Package, "bin"));
        File.WriteAllText(Path.Combine(folder, Tools.Package, "bin", $"{tool}.exe"), tool);
        File.WriteAllText(Path.Combine(folder, Tools.Record), $$"""{"exe":"bin/{{tool}}.exe"}""");
    }

    /// <summary>A list, spelled short — the CLI's <c>listText</c>: downloads separated by <c>; </c>, each
    /// <c>&lt;tool&gt; &lt;version&gt; &lt;platform&gt; &lt;hash letter&gt; &lt;host&gt;</c>.</summary>
    private static string ListText(string spec)
    {
        var tools = new System.Text.Json.Nodes.JsonObject();
        foreach (var entry in spec.Split("; ", StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = entry.Split(' ');
            var (tool, version, platform, letter, host) = (parts[0], parts[1], parts[2], parts[3], parts[4]);
            var versions = (tools[tool] ??= new System.Text.Json.Nodes.JsonObject { ["versions"] = new System.Text.Json.Nodes.JsonObject() })["versions"]!;
            var files = (versions[version] ??= new System.Text.Json.Nodes.JsonObject { ["files"] = new System.Text.Json.Nodes.JsonObject() })["files"]!;
            files[platform] = new System.Text.Json.Nodes.JsonObject
            {
                ["url"] = $"https://{host}/{tool}-{version}.zip", ["sha256"] = new string(letter[0], 64), ["size"] = 10, ["archive"] = "zip",
                ["exe"] = $"bin/{tool}.exe",
            };
        }

        return new System.Text.Json.Nodes.JsonObject { ["schema"] = 1, ["tools"] = tools }.ToJsonString();
    }

    /// <summary>The lists a row names, separated by <c> | </c>: a location called <c>first</c>, then the list built in, last.</summary>
    private static MergedResources MergedOf(string spec)
    {
        var specs = spec.Length == 0 ? [] : spec.Split(" | ");
        return ToolResources.Merge(
            [.. specs.Select((one, at) => ToolResources.Parse(ListText(one), at == specs.Length - 1 ? ToolResources.BuiltIn : "first"))],
            "win-x64");
    }

    /// <summary>
    /// A host, as a stand-in: each address answers bytes, a status, or a redirect (a <c>Uri</c>). No socket is opened,
    /// and every hop is followed by <see cref="ToolInstall"/> itself, held to the rule as a real download's is.
    /// </summary>
    private sealed class Host(Dictionary<string, object> answers) : HttpMessageHandler
    {
        public List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            Asked.Add(url);
            var response = answers.TryGetValue(url, out var answer)
                ? answer switch
                {
                    byte[] bytes => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) },
                    int status => new HttpResponseMessage((System.Net.HttpStatusCode)status),
                    Uri location => new HttpResponseMessage(System.Net.HttpStatusCode.Found) { Headers = { Location = location } },
                    _ => throw new InvalidOperationException("an answer this host does not give"),
                }
                : new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            return Task.FromResult(response);
        }
    }

    // ── Which version, and whether anything is fetched (§3.7). The CLI's `a plan says which version…` ──────────────

    [Theory]
    [InlineData("download the newest the lists name", null, Lists, "", "download", null, "2.63.0", true, null, null)]
    [InlineData("download a version named", null, Lists, "", "download", "2.62.0", "2.62.0", true, null, null)]
    [InlineData("download a version already downloaded", null, Lists, "2.62.0", "download", "2.62.0", "2.62.0", false, null, null)]
    [InlineData("download a version no list names", null, Lists, "", "download", "2.64.0", null, false, null, "unknown")]
    [InlineData("download a version that is not exact", null, Lists, "", "download", "latest", null, false, null, "version")]
    [InlineData("download with no list naming any", null, "", "", "download", null, null, false, null, "unknown")]
    [InlineData("download a version two lists disagree on", null, "gh 2.62.0 win-x64 b o.example | gh 2.62.0 win-x64 a m.example", "", "download", "2.62.0", null, false, null, "conflict")]
    [InlineData("use a version downloaded that no list names", null, "", "2.61.0", "use", "2.61.0", "2.61.0", false, null, null)]
    [InlineData("use the newest, not downloaded", null, Lists, "", "use", null, "2.63.0", true, null, null)]
    [InlineData("update a managed tool to the newest", """{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}""", Lists, "2.62.0", "update", null, "2.63.0", true, null, null)]
    [InlineData("update at the newest, downloaded", """{"tools":{"gh":{"use":"managed","version":"2.63.0"}}}""", Lists, "2.63.0", "update", null, null, false, "nothing to do", null)]
    [InlineData("update at the newest, not downloaded", """{"tools":{"gh":{"use":"managed","version":"2.63.0"}}}""", Lists, "", "update", null, "2.63.0", true, null, null)]
    [InlineData("update never moves a tool back", """{"tools":{"gh":{"use":"managed","version":"2.64.0"}}}""", Lists, "2.64.0", "update", null, null, false, "never moves a tool back", null)]
    [InlineData("update the system’s", null, Lists, "", "update", null, null, false, null, "machine")]
    [InlineData("update a file you name", """{"tools":{"gh":{"use":"file","file":"WHOLE"}}}""", Lists, "", "update", null, null, false, null, "machine")]
    [InlineData("update an entry that does not read", """{"tools":{"gh":{"use":"managed"}}}""", Lists, "", "update", null, null, false, null, "file")]
    [InlineData("update with no list naming any", """{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}""", "", "2.62.0", "update", null, null, false, null, "unknown")]
    public void A_plan_is_made_as_the_cli_makes_it(
        string name, string? tools, string lists, string has, string action, string? version, string? planned, bool fetch, string? nothing,
        string? check)
    {
        Directory.CreateDirectory(Home);
        if (tools is not null) File.WriteAllText(ToolsFile, tools.Replace(Whole, System.Text.Json.JsonEncodedText.Encode(WholeFile).ToString()));
        foreach (var each in has.Split(',', StringSplitOptions.RemoveEmptyEntries)) Downloaded("gh", each);

        var plan = ToolInstall.Plan(Home, MergedOf(lists), "gh", action, version);
        Assert.True(planned == plan.Version, $"{name}: {plan.Version} — {plan.Problem ?? plan.Nothing}");
        Assert.True(fetch == plan.Fetch, name);
        Assert.True(fetch == (plan.Offered is not null), $"{name}: what is fetched is offered");
        if (nothing is null) Assert.True(plan.Nothing is null, $"{name}: {plan.Nothing}");
        else Assert.True(plan.Nothing?.Contains(nothing, StringComparison.Ordinal) == true, $"{name}: {plan.Nothing}");
        Assert.True(check == plan.Check, $"{name}: {plan.Check} — {plan.Problem}");
        Assert.True((check is not null) == (plan.Problem is not null), name);
    }

    [Fact]
    public void A_plans_refusals_say_what_to_do_next()
    {
        Directory.CreateDirectory(Home);
        Assert.Equal(
            "no list names a version of GitHub CLI for win-x64 — `daoris tool look` fetches the locations, and "
            + "`daoris tool locations add <address>` adds one",
            ToolInstall.Plan(Home, MergedOf(""), "gh", "download", null).Problem);
        Assert.Equal(
            "GitHub CLI runs the system's: its updates are the machine's, not Daoris's — `daoris tool use gh managed` keeps a "
            + "version in the home",
            ToolInstall.Plan(Home, MergedOf(Lists), "gh", "update", null).Problem);
        Assert.Equal("no list names GitHub CLI 2.64.0 for win-x64", ToolInstall.Plan(Home, MergedOf(Lists), "gh", "download", "2.64.0").Problem);
    }

    // ── A download (§3.6). The CLI's `a download is verified, staged and laid out…` ──────────────────────────────────

    /// <summary>The archive a download row serves, and the offer a list makes of it — the CLI's <c>offerOf</c>.</summary>
    private static (OfferedVersion Offered, Dictionary<string, object> Answers) OfferOf(string kind, string addresses)
    {
        var bytes = Archive.Build(kind, kind == "tar.gz" ? "bin/gh=gh" : "bin/gh.exe=gh", "");
        var file = $"gh.{kind}";
        var answers = new Dictionary<string, object>(StringComparer.Ordinal);
        var urls = new List<string>();
        foreach (var address in addresses.Split(' '))
        {
            var (@base, answer) = (address[..address.IndexOf('=')], address[(address.IndexOf('=') + 1)..]);
            var url = $"{@base}/{file}";
            urls.Add(url);
            var changed = (byte[])bytes.Clone();
            changed[^1] ^= 0xff;
            answers[url] = answer switch
            {
                "ok" => bytes,
                "other" => changed,
                "longer" => bytes.Append((byte)0).ToArray(),
                "missing" => 404,
                "to-https" => new Uri($"https://cdn.example/{file}"),
                _ => new Uri($"http://elsewhere.example/{file}"),
            };
        }

        answers[$"https://cdn.example/{file}"] = bytes;
        answers[$"http://elsewhere.example/{file}"] = bytes;
        var offered = new OfferedVersion(
            "2.62.0", Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)), bytes.Length, kind,
            kind == "tar.gz" ? "bin/gh" : "bin/gh.exe", ["bin"], urls, [ToolResources.BuiltIn]);
        return (offered, answers);
    }

    [Theory]
    [InlineData("one address, the bytes it names", "zip", "https://maker.example=ok", null, "maker.example")]
    [InlineData("a tar.gz from one address", "tar.gz", "https://maker.example=ok", null, "maker.example")]
    [InlineData("bytes of another hash", "zip", "https://maker.example=other", "hash", null)]
    [InlineData("bytes of another size", "zip", "https://maker.example=longer", "size", null)]
    [InlineData("nothing at the address", "zip", "https://maker.example=missing", "unreachable", null)]
    [InlineData("an address over http to another host", "zip", "http://maker.example=ok", "address", null)]
    [InlineData("an address over http to this machine", "zip", "http://127.0.0.1:8080=ok", null, "127.0.0.1:8080")]
    [InlineData("a redirect over https", "zip", "https://maker.example=to-https", null, "maker.example")]
    [InlineData("a redirect to http on another host", "zip", "https://maker.example=to-http", "address", null)]
    [InlineData("a mirror with other bytes, then the maker", "zip", "https://mirror.example=other https://maker.example=ok", null, "maker.example")]
    [InlineData("a mirror with nothing, then the maker", "zip", "https://mirror.example=missing https://maker.example=ok", null, "maker.example")]
    [InlineData("every address fails, and the last says why", "zip", "https://mirror.example=missing https://maker.example=other", "hash", null)]
    [InlineData("an archive kind nobody unpacks, before a byte is fetched", "7z", "https://maker.example=ok", "archive", null)]
    public async Task A_download_is_verified_as_the_cli_verifies_it(string name, string kind, string addresses, string? check, string? servedBy)
    {
        var (offered, answers) = OfferOf(kind, addresses);
        var host = new Host(answers);
        var folder = ToolInstall.VersionFolder(Home, "gh", "2.62.0");

        if (check is null)
        {
            await ToolInstall.DownloadAsync(Home, "gh", offered, "win-x64", _ => { }, CancellationToken.None, host);
            var record = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(folder, Tools.Record)))!;
            Assert.True(servedBy == new Uri(record["url"]!.GetValue<string>()).Authority, name);
            Assert.Equal("gh", File.ReadAllText(Path.Combine([folder, Tools.Package, .. offered.Exe.Split('/')])));
            Assert.False(File.Exists(ToolsFile), $"{name}: nothing switches");
        }
        else
        {
            var refused = await Assert.ThrowsAsync<ToolRefusal>(() =>
                ToolInstall.DownloadAsync(Home, "gh", offered, "win-x64", _ => { }, CancellationToken.None, host));
            Assert.True(check == refused.Check, $"{name}: {refused.Check} — {refused.Message}");
            var tool = Path.Combine(Home, Tools.Folder, "gh");
            Assert.True(!Directory.Exists(tool) || !Directory.EnumerateFileSystemEntries(tool).Any(), $"{name}: nothing under tools/gh");
        }

        Assert.False(Directory.Exists(folder + ToolInstall.Staging), $"{name}: no staging is left");
        if (kind == "7z") Assert.Empty(host.Asked);
    }

    [Theory]
    [InlineData("tool,version,platform,sha256,size,archive,url,lists,exe,paths,at")]
    public async Task The_record_names_what_the_cli_names(string keys)
    {
        var (offered, answers) = OfferOf("zip", "https://mirror.example=missing https://maker.example=ok");
        offered = offered with { Lists = ["https://lists.example/r.json", ToolResources.BuiltIn] };
        var lines = new List<string>();
        var exe = await ToolInstall.DownloadAsync(Home, "gh", offered, "win-x64", lines.Add, CancellationToken.None, new Host(answers));

        var folder = ToolInstall.VersionFolder(Home, "gh", "2.62.0");
        Assert.Equal(Path.Combine(folder, Tools.Package, "bin", "gh.exe"), exe);
        var text = File.ReadAllText(Path.Combine(folder, Tools.Record));
        var record = System.Text.Json.Nodes.JsonNode.Parse(text)!.AsObject();
        Assert.Equal(keys.Split(','), record.Select(pair => pair.Key));
        Assert.Equal(keys.Split(','), ToolInstall.RecordKeys);
        Assert.Equal("gh", record["tool"]!.GetValue<string>());
        Assert.Equal("2.62.0", record["version"]!.GetValue<string>());
        Assert.Equal("win-x64", record["platform"]!.GetValue<string>());
        Assert.Equal(offered.Sha256, record["sha256"]!.GetValue<string>());
        Assert.Equal(offered.Size, record["size"]!.GetValue<long>());
        Assert.Equal("zip", record["archive"]!.GetValue<string>());
        Assert.Equal("https://maker.example/gh.zip", record["url"]!.GetValue<string>());
        Assert.Equal(["https://lists.example/r.json", ToolResources.BuiltIn], record["lists"]!.AsArray().Select(each => each!.GetValue<string>()));
        Assert.Equal("bin/gh.exe", record["exe"]!.GetValue<string>());
        Assert.Equal(["bin"], record["paths"]!.AsArray().Select(each => each!.GetValue<string>()));
        Assert.Matches(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z\z", record["at"]!.GetValue<string>());
        Assert.True(text.EndsWith("}\n", StringComparison.Ordinal) && !text.Contains('\r'), "two-space JSON, LF, a final newline");
        Assert.Equal([Tools.Package, Tools.Record], Directory.EnumerateFileSystemEntries(folder).Select(each => Path.GetFileName(each)).Order(StringComparer.Ordinal));
        Assert.Contains(lines, line => line.Contains("https://mirror.example/gh.zip", StringComparison.Ordinal) && line.Contains("nothing there", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains(offered.Sha256, StringComparison.Ordinal));
        Assert.Equal(["2.62.0"], ToolInstall.Downloaded(Home, "gh"));
    }

    [Fact]
    public async Task A_version_already_downloaded_fetches_nothing_and_one_that_runs_nothing_is_replaced()
    {
        var (offered, answers) = OfferOf("zip", "https://maker.example=ok");
        await ToolInstall.DownloadAsync(Home, "gh", offered, "win-x64", _ => { }, CancellationToken.None, new Host(answers));

        var silent = new Host(new Dictionary<string, object>());
        var lines = new List<string>();
        await ToolInstall.DownloadAsync(Home, "gh", offered, "win-x64", lines.Add, CancellationToken.None, silent);
        Assert.Empty(silent.Asked);
        Assert.Contains(lines, line => line.Contains("already downloaded", StringComparison.Ordinal));

        // A folder whose executable went is no proof: it is replaced by a verified one.
        var folder = ToolInstall.VersionFolder(Home, "gh", "2.62.0");
        File.WriteAllText(Path.Combine(folder, Tools.Record), """{"exe":"bin/missing.exe"}""");
        await ToolInstall.DownloadAsync(Home, "gh", offered, "win-x64", _ => { }, CancellationToken.None, new Host(answers));
        Assert.Equal("bin/gh.exe", System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(folder, Tools.Record)))!["exe"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(".part", 10, 30000)]
    public void The_bounds_are_the_cli_s(string staging, int redirects, int lookMilliseconds)
    {
        Assert.Equal(ToolInstall.Staging, staging);
        Assert.Equal(ToolInstall.Redirects, redirects);
        Assert.Equal(lookMilliseconds, ToolInstall.LookBound.TotalMilliseconds);
    }

    // ── Deleting a version (§3.6). The CLI's `a version is deleted only when nothing uses it…` ──────────────────────

    [Theory]
    [InlineData("a version nothing uses", null, true, "2.62.0", null)]
    [InlineData("a version not downloaded", null, false, "2.62.0", "missing")]
    [InlineData("the version the tool runs", """{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}""", true, "2.62.0", "in-use")]
    [InlineData("another version than the one it runs", """{"tools":{"gh":{"use":"managed","version":"2.63.0"}}}""", true, "2.62.0", null)]
    [InlineData("beside a file that does not read", "not json", true, "2.62.0", "file")]
    [InlineData("a version that climbs out", null, false, "../2.62.0", "version")]
    public void A_version_is_deleted_as_the_cli_deletes_it(string name, string? tools, bool has, string version, string? check)
    {
        Directory.CreateDirectory(Home);
        if (tools is not null) File.WriteAllText(ToolsFile, tools);
        if (has) Downloaded("gh", "2.62.0");

        if (check is null)
        {
            ToolInstall.Delete(Home, "gh", version);
            Assert.False(Directory.Exists(ToolInstall.VersionFolder(Home, "gh", version)), name);
        }
        else
        {
            var refused = Assert.Throws<ToolRefusal>(() => ToolInstall.Delete(Home, "gh", version));
            Assert.True(check == refused.Check, $"{name}: {refused.Check} — {refused.Message}");
            if (has) Assert.True(Directory.Exists(ToolInstall.VersionFolder(Home, "gh", "2.62.0")), $"{name}: the version stays");
        }
    }

    // ── Look for updates (§3.7). The CLI's `a location is looked at as the driver looks…` ──────────────────────────

    private const string Location = "https://lists.example/resources.json";

    private static readonly string OldList = ListText("gh 2.62.0 win-x64 a m.example");

    private static readonly string NewList = ListText("gh 2.63.0 win-x64 b m.example");

    [Theory]
    [InlineData("a list, never fetched before", "list", false, "fetched", "new")]
    [InlineData("a list, over an older copy", "list", true, "fetched", "new")]
    [InlineData("a list this build does not read, over a copy", "unread", true, "unread", "old")]
    [InlineData("a list this build does not read, never fetched", "unread", false, "unread", "none")]
    [InlineData("text that is not JSON, over a copy", "not json", true, "unread", "old")]
    [InlineData("nothing there, over a copy", "missing", true, "failed", "old")]
    [InlineData("nothing there, never fetched", "missing", false, "failed", "none")]
    [InlineData("a redirect to http on another host", "to-http", true, "failed", "old")]
    public async Task A_location_is_looked_at_as_the_cli_looks(string name, string answers, bool before, string outcome, string after)
    {
        Directory.CreateDirectory(Home);
        File.WriteAllText(ToolsFile, $$"""{"locations":["{{Location}}"]}""");
        var copy = ToolResources.LocationCopy(Home, Location);
        if (before)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            File.WriteAllText(copy, OldList);
        }

        var served = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["http://elsewhere.example/resources.json"] = Encoding.UTF8.GetBytes(NewList),
        };
        if (answers != "missing")
        {
            served[Location] = answers switch
            {
                "list" => Encoding.UTF8.GetBytes(NewList),
                "unread" => Encoding.UTF8.GetBytes("""{"schema":2}"""),
                "not json" => Encoding.UTF8.GetBytes("not json"),
                _ => new Uri("http://elsewhere.example/resources.json"),
            };
        }

        var looks = await ToolInstall.LookAsync(Home, "win-x64", CancellationToken.None, new Host(served));
        Assert.Equal([(Location, outcome)], looks.Select(look => (look.Address, look.Outcome)));
        var kept = File.Exists(copy) ? File.ReadAllText(copy) : null;
        Assert.True((after switch { "new" => NewList, "old" => OldList, _ => null }) == kept, name);
    }

    [Fact]
    public async Task A_look_says_what_a_list_added_and_dropped_and_the_age_of_a_copy_it_kept()
    {
        Directory.CreateDirectory(Home);
        File.WriteAllText(ToolsFile, $$"""{"locations":["{{Location}}"]}""");
        var copy = ToolResources.LocationCopy(Home, Location);
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.WriteAllText(copy, OldList);

        var fetched = (await ToolInstall.LookAsync(Home, "win-x64", CancellationToken.None, new Host(new() { [Location] = Encoding.UTF8.GetBytes(NewList) })))[0];
        Assert.Equal(["gh 2.63.0"], fetched.Added);
        Assert.Equal(["gh 2.62.0"], fetched.Dropped);
        Assert.Equal("https://lists.example/resources.json: fetched — 1 version for win-x64; added gh 2.63.0; dropped gh 2.62.0", fetched.Sentence);

        var failed = (await ToolInstall.LookAsync(Home, "win-x64", CancellationToken.None, new Host(new Dictionary<string, object>())))[0];
        Assert.Matches(@"^https://lists\.example/resources\.json could not be fetched \(nothing there\); its copy from .+ ago is kept\z", failed.Sentence);
    }

    [Fact]
    public async Task A_location_that_does_not_answer_within_its_bound_keeps_its_copy()
    {
        Directory.CreateDirectory(Home);
        File.WriteAllText(ToolsFile, $$"""{"locations":["{{Location}}"]}""");

        var looks = await ToolInstall.LookAsync(Home, "win-x64", CancellationToken.None, new Silent(), TimeSpan.FromMilliseconds(50));
        Assert.Equal("failed", looks[0].Outcome);
        Assert.Contains("lists.example did not answer within", looks[0].Sentence);
        Assert.Contains("it has never been fetched, so it names nothing", looks[0].Sentence);
    }

    /// <summary>A host that never answers until it is stopped.</summary>
    private sealed class Silent : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("unreachable");
        }
    }

    /// <summary>
    /// Archives built from a row's words — the CLI's <c>_archives.ts</c>, spelled again with this side's own code.
    /// </summary>
    /// <remarks>
    /// <para><c>entries</c> is items separated by <c>; </c>. An item is a folder, <c>&lt;name&gt;/</c>, or a file,
    /// <c>&lt;name&gt;=&lt;text&gt;</c>, followed by any of these flags, each after a <c>!</c>: <c>stored</c>,
    /// <c>method12</c>, <c>encrypted</c>, <c>badcrc</c>, <c>symlink</c>, <c>hardlink</c>, <c>fifo</c>.</para>
    /// <para><c>shape</c> is <c>""</c>, <c>zip64</c>, <c>pax</c>, <c>cut-end</c>, <c>cut-data</c> or
    /// <c>cut-gzip</c>.</para>
    /// </remarks>
    internal static class Archive
    {
        public sealed record Item(string Name, string? Text, IReadOnlyList<string> Flags);

        public static IReadOnlyList<Item> Parse(string spec) =>
        [
            .. spec.Split("; ", StringSplitOptions.RemoveEmptyEntries).Select(item =>
            {
                var parts = item.Split('!');
                var head = parts[0];
                var equals = head.IndexOf('=');
                return equals < 0 ? new Item(head, null, parts[1..]) : new Item(head[..equals], head[(equals + 1)..], parts[1..]);
            }),
        ];

        /// <summary>The archive a row names: <c>zip</c>, <c>tar.gz</c>, or any other kind, built as a zip.</summary>
        public static byte[] Build(string kind, string entries, string shape)
        {
            var items = Parse(entries);
            if (kind == "tar.gz")
            {
                var gz = Gzip(Tar(items, shape));
                return shape == "cut-gzip" ? gz[..^4] : gz;
            }

            var zip = Zip(items, shape == "zip64");
            return shape switch
            {
                "cut-end" => zip[..^22],
                "cut-data" => zip[..(zip.Length / 2)],
                _ => zip,
            };
        }

        private static byte[] Tar(IReadOnlyList<Item> items, string shape)
        {
            var tar = new MemoryStream();
            foreach (var item in items)
            {
                var folder = item.Text is null;
                var type = folder ? '5' : item.Flags.Contains("symlink") ? '2' : item.Flags.Contains("hardlink") ? '1'
                    : item.Flags.Contains("fifo") ? '6' : '0';
                var linked = type is '1' or '2';
                var body = type == '0' ? Encoding.UTF8.GetBytes(item.Text!) : [];
                if (shape == "pax") tar.Write(TarEntry("PaxHeader", Encoding.UTF8.GetBytes(Pax("path", item.Name)), 'x', 0x1A4, ""));
                var entry = TarEntry(shape == "pax" ? "placeholder" : item.Name, body, type, folder ? 0x1ED : 0x1A4, linked ? item.Text! : "");
                if (item.Flags.Contains("badcrc")) Encoding.ASCII.GetBytes("000000\0 ").CopyTo(entry, 148);
                tar.Write(entry);
            }

            if (shape != "cut-end") tar.Write(new byte[1024]);
            var bytes = tar.ToArray();
            return shape == "cut-data" ? bytes[..513] : bytes;
        }

        /// <summary>One ustar entry: its header, then its data padded to the block.</summary>
        private static byte[] TarEntry(string name, byte[] data, char type, int mode, string link)
        {
            var header = new byte[512];
            Put(header, 0, name.Length > 100 ? name[..100] : name);
            Put(header, 100, Octal(mode, 7));
            Put(header, 108, Octal(0, 7));
            Put(header, 116, Octal(0, 7));
            Put(header, 124, Octal(data.Length, 11));
            Put(header, 136, Octal(0, 11));
            Put(header, 148, "        ");
            header[156] = (byte)type;
            Put(header, 157, link);
            Put(header, 257, "ustar\000");
            var sum = header.Sum(value => value);
            Put(header, 148, Convert.ToString(sum, 8).PadLeft(6, '0') + "\0 ");

            var padded = new byte[(data.Length + 511) / 512 * 512];
            data.CopyTo(padded, 0);
            return [.. header, .. padded];
        }

        /// <summary>A pax record: <c>&lt;length&gt; &lt;key&gt;=&lt;value&gt;\n</c>, the length counting itself.</summary>
        private static string Pax(string key, string value)
        {
            var tail = $" {key}={value}\n";
            var length = tail.Length + 1;
            while ($"{length}{tail}".Length != length) length++;
            return $"{length}{tail}";
        }

        private static string Octal(int value, int width) => Convert.ToString(value, 8).PadLeft(width, '0') + "\0";

        private static void Put(byte[] into, int at, string text) => Encoding.UTF8.GetBytes(text).CopyTo(into, at);

        private static byte[] Gzip(byte[] data)
        {
            var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true)) gzip.Write(data);
            return output.ToArray();
        }

        private static byte[] Deflate(byte[] data)
        {
            var output = new MemoryStream();
            using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true)) deflate.Write(data);
            return output.ToArray();
        }

        private static byte[] Zip64Extra(params long[] values)
        {
            var extra = new MemoryStream();
            using var writer = new BinaryWriter(extra);
            writer.Write((ushort)0x0001);
            writer.Write((ushort)(8 * values.Length));
            foreach (var value in values) writer.Write((ulong)value);
            writer.Flush();
            return extra.ToArray();
        }

        private static byte[] Zip(IReadOnlyList<Item> items, bool zip64)
        {
            var locals = new MemoryStream();
            var centrals = new MemoryStream();
            using var local = new BinaryWriter(locals);
            using var central = new BinaryWriter(centrals);

            foreach (var item in items)
            {
                var folder = item.Text is null;
                var data = Encoding.UTF8.GetBytes(item.Text ?? "");
                ushort method = folder || item.Flags.Contains("stored") ? (ushort)0 : item.Flags.Contains("method12") ? (ushort)12 : (ushort)8;
                var body = method == 8 ? Deflate(data) : data;
                var crc = ToolInstall.Crc32(data);
                if (item.Flags.Contains("badcrc")) crc ^= 0xffffffff;
                var flags = (ushort)((item.Flags.Contains("encrypted") ? 1 : 0) | 0x800);
                var mode = item.Flags.Contains("symlink") ? 0xA1FF : folder ? 0x41ED : 0x81A4;
                var name = Encoding.UTF8.GetBytes(item.Name);
                ushort version = zip64 ? (ushort)45 : (ushort)20;
                var offset = locals.Length;

                var localExtra = zip64 ? Zip64Extra(data.Length, body.Length) : [];
                local.Write(0x04034b50u);
                local.Write(version);
                local.Write(flags);
                local.Write(method);
                local.Write((ushort)0);
                local.Write((ushort)0x21);
                local.Write(crc);
                local.Write(zip64 ? 0xffffffffu : (uint)body.Length);
                local.Write(zip64 ? 0xffffffffu : (uint)data.Length);
                local.Write((ushort)name.Length);
                local.Write((ushort)localExtra.Length);
                local.Write(name);
                local.Write(localExtra);
                local.Write(body);

                var centralExtra = zip64 ? Zip64Extra(data.Length, body.Length, offset) : [];
                central.Write(0x02014b50u);
                central.Write((ushort)(0x0300 | version));
                central.Write(version);
                central.Write(flags);
                central.Write(method);
                central.Write((ushort)0);
                central.Write((ushort)0x21);
                central.Write(crc);
                central.Write(zip64 ? 0xffffffffu : (uint)body.Length);
                central.Write(zip64 ? 0xffffffffu : (uint)data.Length);
                central.Write((ushort)name.Length);
                central.Write((ushort)centralExtra.Length);
                central.Write((ushort)0);
                central.Write((ushort)0);
                central.Write((ushort)0);
                central.Write((uint)mode << 16);
                central.Write(zip64 ? 0xffffffffu : (uint)offset);
                central.Write(name);
                central.Write(centralExtra);
            }

            local.Flush();
            central.Flush();
            var directory = centrals.ToArray();
            var at = locals.Length;
            var all = new MemoryStream();
            using var tail = new BinaryWriter(all);
            tail.Write(locals.ToArray());
            tail.Write(directory);
            if (zip64)
            {
                tail.Write(0x06064b50u);
                tail.Write(44ul);
                tail.Write((ushort)45);
                tail.Write((ushort)45);
                tail.Write(0u);
                tail.Write(0u);
                tail.Write((ulong)items.Count);
                tail.Write((ulong)items.Count);
                tail.Write((ulong)directory.Length);
                tail.Write((ulong)at);
                tail.Write(0x07064b50u);
                tail.Write(0u);
                tail.Write((ulong)(at + directory.Length));
                tail.Write(1u);
            }

            tail.Write(0x06054b50u);
            tail.Write((ushort)0);
            tail.Write((ushort)0);
            tail.Write(zip64 ? (ushort)0xffff : (ushort)items.Count);
            tail.Write(zip64 ? (ushort)0xffff : (ushort)items.Count);
            tail.Write(zip64 ? 0xffffffffu : (uint)directory.Length);
            tail.Write(zip64 ? 0xffffffffu : (uint)at);
            tail.Write((ushort)0);
            tail.Flush();
            return all.ToArray();
        }
    }
}
