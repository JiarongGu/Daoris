using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>resources.json</c>, schema 1, and the merge of every list read (TOOLS3, D121; the tools design §3.1–§3.5,
/// §5): the driver's half of a TWIN with the CLI's <c>resources.ts</c>, whose <c>resources.test.ts</c> holds the
/// same tables, row for row and in the same order. They share no code; a row changed here is changed there, in
/// the same commit.
/// </summary>
/// <remarks>
/// <para>The list's rules (§3.2), each a table below:</para>
/// <list type="number">
/// <item><c>schema</c> is read first: anything but 1 refuses the whole list, and so does a <c>tools</c> that is
/// no object.</item>
/// <item>A tool this build does not declare is named and never offered (§3.4 rule 1).</item>
/// <item>A version is one to four numbers; a platform is one of the table's; a version names its
/// <c>files</c>.</item>
/// <item>A file names its <c>url</c> (https://, or http:// to this machine), its <c>sha256</c> (64 hex, kept in
/// lower case), its <c>size</c> (whole bytes above 0), its <c>archive</c> (zip or tar.gz) and its <c>exe</c> (a
/// relative <c>/</c> path inside the archive). <c>paths</c> defaults to the folder <c>exe</c> is in, <c>.</c> for
/// the archive's root.</item>
/// <item>What does not read is skipped and said, and never guessed at.</item>
/// </list>
/// <para>Then the merge (§3.4): the person's locations in order, then the list built in; one tool, version and
/// platform is one download, so lists that disagree refuse that version, naming both; lists that agree under
/// other addresses are mirrors; the versions are the union, and the newest is the highest by number.</para>
/// <para>🔴 <b>The CLI reads these theories.</b> <c>resources.test.ts</c>'s <i>the driver's tables are these
/// tables</i> parses each <c>[InlineData]</c> row here and holds it to its own table, cell for cell and in order,
/// so a row changed on one side alone fails <c>npm run verify</c>. Keep each row on one line, its cells
/// literals.</para>
/// <para>Nothing here opens a connection: a list is text, or a file on disk.</para>
/// </remarks>
public sealed class ToolResourcesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-resources-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>A hash that reads: the placeholder <c>"SHA"</c> in a row.</summary>
    private static readonly string Hash = new('a', 64);

    /// <summary>The file every structural row uses, as the token <c>FILE</c>.</summary>
    private const string FileToken = """{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe"}""";

    private string Home => Path.Combine(_root, "data");

    public ToolResourcesTests() => Directory.CreateDirectory(Home);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>A row's text with its tokens spelled out, as the CLI's <c>expand</c> spells them.</summary>
    private static string Expand(string text) => text.Replace("FILE", FileToken, StringComparison.Ordinal)
        .Replace("\"SHA\"", $"\"{Hash}\"", StringComparison.Ordinal);

    /// <summary>One file of <c>gh</c> 2.62.0 for <c>win-x64</c>, in a list otherwise sound.</summary>
    private static string OneFile(string file) => """{"schema":1,"tools":{"gh":{"versions":{"2.62.0":{"files":{"win-x64":""" + file + "}}}}}}";

    /// <summary>What a list offers, in one line: each tool in ordinal order, its versions newest first, its platforms in the table's order.</summary>
    private static string Summary(ResourceList list)
    {
        var parts = new List<string>();
        foreach (var id in list.Tools.Keys.Order(StringComparer.Ordinal))
        {
            var tool = list.Tools[id];
            foreach (var version in tool.Versions.Keys.OrderDescending(Comparer<string>.Create(ToolResources.CompareVersions)))
            {
                var files = tool.Versions[version];
                parts.AddRange(ToolResources.Platforms.Where(files.ContainsKey).Select(platform => $"{id} {version} {platform}"));
            }
        }

        return string.Join("; ", parts);
    }

    // ── The constants both twins spell ─────────────────────────────────────────────────────────────────

    [Fact]
    public void The_list_where_it_is_and_what_it_may_name()
    {
        Assert.Equal("resources.json", ToolResources.FileName);
        Assert.Equal(["app", "resources.json"], ToolResources.Layout);
        Assert.Equal([Tools.Folder, "locations"], ToolResources.LocationsFolder);
        Assert.Equal(1, ToolResources.Schema);
        Assert.Equal(["zip", "tar.gz"], ToolResources.Archives);
        Assert.Equal("the built-in list", ToolResources.BuiltIn);
    }

    // ── The platform table (§3.2). The CLI's `a platform is named as the driver names it` ───────────────

    [Theory]
    [InlineData("win-x64", "win32", "x64", "Windows", "X64")]
    [InlineData("win-arm64", "win32", "arm64", "Windows", "Arm64")]
    [InlineData("linux-x64", "linux", "x64", "Linux", "X64")]
    [InlineData("linux-arm64", "linux", "arm64", "Linux", "Arm64")]
    [InlineData("osx-x64", "darwin", "x64", "OSX", "X64")]
    [InlineData("osx-arm64", "darwin", "arm64", "OSX", "Arm64")]
    [InlineData(null, "win32", "ia32", "Windows", "X86")]
    [InlineData(null, "linux", "arm", "Linux", "Arm")]
    [InlineData(null, "freebsd", "x64", "FreeBSD", "X64")]
    public void A_platform_is_named_as_the_cli_names_it(string? platform, string node, string nodeArch, string os, string arch)
    {
        // The CLI's columns: Node's names for the same row, read there.
        _ = (node, nodeArch);
        Assert.Equal(platform, ToolResources.PlatformFor(OSPlatform.Create(os), Enum.Parse<Architecture>(arch)));
    }

    [Fact]
    public void The_platforms_are_the_tables_and_this_process_is_one_of_them_or_none()
    {
        Assert.Equal(["win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64"], ToolResources.Platforms);
        var os = OperatingSystem.IsWindows() ? OSPlatform.Windows : OperatingSystem.IsLinux() ? OSPlatform.Linux : OSPlatform.OSX;
        Assert.Equal(ToolResources.PlatformFor(os, RuntimeInformation.ProcessArchitecture), ToolResources.Current);
    }

    // ── Versions, by number (§3.4 rule 6). The CLI's `versions compare by number…` ────────────────────

    [Theory]
    [InlineData("2.10.0", "2.9.0", 1)]
    [InlineData("2.9.0", "2.10.0", -1)]
    [InlineData("2.62.0", "2.62.0", 0)]
    [InlineData("24.21.0", "2.56.0", 1)]
    [InlineData("1.0.0.10", "1.0.0.9", 1)]
    [InlineData("3", "2.99.99.99", 1)]
    [InlineData("99999999999999999999", "1", 1)]
    [InlineData("2.62", "2.62.0", -1)]
    [InlineData("007", "7", -1)]
    public void Versions_compare_as_the_cli_compares_them(string a, string b, int sign)
    {
        Assert.Equal(sign, Math.Sign(ToolResources.CompareVersions(a, b)));
    }

    // ── Rules 1–3, 5: the list's shape. The CLI's `a list reads as the driver reads it` ──────────────────

    [Theory]
    [InlineData("not JSON", "not json", "is not readable JSON", null, "")]
    [InlineData("a list", "[]", "is not a JSON object", null, "")]
    [InlineData("no schema", """{"tools":{}}""", "names no `schema`", null, "")]
    [InlineData("a schema of null", """{"schema":null,"tools":{}}""", "names no `schema`", null, "")]
    [InlineData("a schema this build does not know", """{"schema":2,"tools":{}}""", "is schema 2, and this build reads schema 1", null, "")]
    [InlineData("a schema that is text", """{"schema":"1","tools":{}}""", """is schema "1", and this build reads schema 1""", null, "")]
    [InlineData("tools that are a list", """{"schema":1,"tools":[]}""", "`tools` is not an object", null, "")]
    [InlineData("no tools", """{"schema":1}""", null, null, "")]
    [InlineData("one download", """{"schema":1,"tools":{"gh":{"versions":{"2.62.0":{"files":{"win-x64":FILE}}}}}}""", null, null, "gh 2.62.0 win-x64")]
    [InlineData("versions newest first, platforms in the table’s order", """{"schema":1,"tools":{"gh":{"versions":{"2.9.0":{"files":{"win-x64":FILE}},"2.10.0":{"files":{"linux-x64":FILE,"win-x64":FILE}}}}}}""", null, null, "gh 2.10.0 win-x64; gh 2.10.0 linux-x64; gh 2.9.0 win-x64")]
    [InlineData("two tools", """{"schema":1,"tools":{"node":{"versions":{"24.21.0":{"files":{"win-x64":FILE}}}},"gh":{"versions":{"2.62.0":{"files":{"win-x64":FILE}}}}}}""", null, null, "gh 2.62.0 win-x64; node 24.21.0 win-x64")]
    [InlineData("a tool this build does not run", """{"schema":1,"tools":{"bun":{"versions":{"1.2.0":{"files":{"win-x64":FILE}}}}}}""", null, "names `bun`, which is not a tool this build runs", "")]
    [InlineData("a tool that is not an object", """{"schema":1,"tools":{"gh":"2.62.0"}}""", null, "`gh` is not an object", "")]
    [InlineData("a tool with no versions", """{"schema":1,"tools":{"gh":{}}}""", null, null, "")]
    [InlineData("versions that are not an object", """{"schema":1,"tools":{"gh":{"versions":[]}}}""", null, "`gh`'s `versions` is not an object", "")]
    [InlineData("a version that is a tag", """{"schema":1,"tools":{"gh":{"versions":{"v2.62.0":{"files":{"win-x64":FILE}}}}}}""", null, "`gh` `v2.62.0` is not an exact version", "")]
    [InlineData("a version of five numbers", """{"schema":1,"tools":{"gh":{"versions":{"2.62.0.1.1":{"files":{"win-x64":FILE}}}}}}""", null, "`gh` `2.62.0.1.1` is not an exact version", "")]
    [InlineData("a version with no files", """{"schema":1,"tools":{"gh":{"versions":{"2.62.0":{}}}}}""", null, "`gh` 2.62.0 has no `files` object", "")]
    [InlineData("files that are a list", """{"schema":1,"tools":{"gh":{"versions":{"2.62.0":{"files":[]}}}}}""", null, "`gh` 2.62.0 has no `files` object", "")]
    [InlineData("a platform this build does not know", """{"schema":1,"tools":{"gh":{"versions":{"2.62.0":{"files":{"win-x86":FILE}}}}}}""", null, "names `win-x86`, which is not a platform this build knows", "")]
    [InlineData("a file that does not read", """{"schema":1,"tools":{"gh":{"versions":{"2.62.0":{"files":{"win-x64":{}}}}}}}""", null, "`gh` 2.62.0 for win-x64 is skipped — ", "")]
    [InlineData("one file skipped beside one that reads", """{"schema":1,"tools":{"gh":{"versions":{"2.62.0":{"files":{"win-x64":FILE,"linux-x64":7}}}}}}""", null, "`gh` 2.62.0 for linux-x64 is skipped — it is not an object", "gh 2.62.0 win-x64")]
    [InlineData("a source that is no address", """{"schema":1,"tools":{"gh":{"source":"http://example.org/releases","versions":{"2.62.0":{"files":{"win-x64":FILE}}}}}}""", null, "`gh`'s `source` is not https://, or http:// to this machine, and is not shown", "gh 2.62.0 win-x64")]
    [InlineData("a licence with no id", """{"schema":1,"tools":{"gh":{"licence":{"url":"https://example.org/licence"},"versions":{"2.62.0":{"files":{"win-x64":FILE}}}}}}""", null, "`gh`'s `licence` names no `id`, and is not shown", "gh 2.62.0 win-x64")]
    [InlineData("a licence whose address is no address", """{"schema":1,"tools":{"gh":{"licence":{"id":"MIT","url":"ftp://example.org/licence"},"versions":{"2.62.0":{"files":{"win-x64":FILE}}}}}}""", null, "`gh`'s licence `url` is not https://, or http:// to this machine, and is not shown", "gh 2.62.0 win-x64")]
    public void A_list_reads_as_the_cli_reads_it(string name, string text, string? problem, string? note, string offers)
    {
        var list = ToolResources.Parse(Expand(text), "a list");

        if (problem is null) Assert.True(list.Problem is null, $"{name}: {list.Problem}");
        else Assert.True(list.Problem?.Contains(problem, StringComparison.Ordinal) == true, $"{name}: {list.Problem}");
        if (note is null) Assert.True(list.Notes.Count == 0, $"{name}: {string.Join(" / ", list.Notes)}");
        else Assert.True(list.Notes.Count == 1 && list.Notes[0].Contains(note, StringComparison.Ordinal), $"{name}: {string.Join(" / ", list.Notes)}");
        Assert.Equal(offers, Summary(list));
    }

    [Fact]
    public void A_list_that_does_not_read_names_itself_offers_nothing_and_says_a_newer_Daoris_may_read_it()
    {
        var list = ToolResources.Parse("""{"schema":2,"tools":{"gh":{}}}""", "https://example.org/r.json");

        Assert.Equal(
            "https://example.org/r.json is schema 2, and this build reads schema 1: nothing in it is read, and a newer Daoris may read it",
            list.Problem);
        Assert.Empty(list.Tools);
        Assert.Empty(list.Unknown);
    }

    [Fact]
    public void A_tool_this_build_does_not_run_is_kept_by_name_and_nothing_of_it_is_read()
    {
        var list = ToolResources.Parse(Expand("""{"schema":1,"tools":{"bun":{"versions":{"1.2.0":{"files":{"win-x64":FILE}}}},"gh":{}}}"""), "first");

        Assert.Equal(["bun"], list.Unknown);
        Assert.Equal(["gh"], list.Tools.Keys);
        Assert.Equal(["first names `bun`, which is not a tool this build runs: nothing of it is offered"], list.Notes);
    }

    // ── Rule 4: one file. The CLI's `a file reads as the driver reads it` ─────────────────────────────────

    [Theory]
    [InlineData("the whole file", """{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe"}""", null, "bin")]
    [InlineData("its folders named", """{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe","paths":["bin","."]}""", null, "bin,.")]
    [InlineData("folders of null", """{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe","paths":null}""", null, "bin")]
    [InlineData("an executable at the root", """{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"gh.exe"}""", null, ".")]
    [InlineData("an executable two folders down", """{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"gh_2.62.0/bin/gh.exe"}""", null, "gh_2.62.0/bin")]
    [InlineData("a tar.gz", """{"url":"https://example.org/gh.tar.gz","sha256":"SHA","size":10,"archive":"tar.gz","exe":"bin/gh"}""", null, "bin")]
    [InlineData("an address on this machine", """{"url":"http://127.0.0.1:8080/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe"}""", null, "bin")]
    [InlineData("a hash in capitals", """{"url":"https://example.org/gh.zip","sha256":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA","size":10,"archive":"zip","exe":"bin/gh.exe"}""", null, "bin")]
    [InlineData("not an object", "\"gh.zip\"", "it is not an object", null)]
    [InlineData("no address", """{"sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe"}""", "it names no `url`", null)]
    [InlineData("an address that is not text", """{"url":7,"sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe"}""", "it names no `url`", null)]
    [InlineData("an address over http to another host", """{"url":"http://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe"}""", "`url` is not https://, or http:// to this machine", null)]
    [InlineData("an address that is no address", """{"url":"gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe"}""", "`url` is not https://, or http:// to this machine", null)]
    [InlineData("no hash", """{"url":"https://example.org/gh.zip","size":10,"archive":"zip","exe":"bin/gh.exe"}""", "`sha256` is not 64 hex digits", null)]
    [InlineData("a short hash", """{"url":"https://example.org/gh.zip","sha256":"abc","size":10,"archive":"zip","exe":"bin/gh.exe"}""", "`sha256` is not 64 hex digits", null)]
    [InlineData("a hash that is not hex", """{"url":"https://example.org/gh.zip","sha256":"gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg","size":10,"archive":"zip","exe":"bin/gh.exe"}""", "`sha256` is not 64 hex digits", null)]
    [InlineData("no size", """{"url":"https://example.org/gh.zip","sha256":"SHA","archive":"zip","exe":"bin/gh.exe"}""", "`size` is not a whole number of bytes above 0", null)]
    [InlineData("a size of nothing", """{"url":"https://example.org/gh.zip","sha256":"SHA","size":0,"archive":"zip","exe":"bin/gh.exe"}""", "`size` is not a whole number of bytes above 0", null)]
    [InlineData("a size below nothing", """{"url":"https://example.org/gh.zip","sha256":"SHA","size":-10,"archive":"zip","exe":"bin/gh.exe"}""", "`size` is not a whole number of bytes above 0", null)]
    [InlineData("a size with a fraction", """{"url":"https://example.org/gh.zip","sha256":"SHA","size":10.5,"archive":"zip","exe":"bin/gh.exe"}""", "`size` is not a whole number of bytes above 0", null)]
    [InlineData("a size that is text", """{"url":"https://example.org/gh.zip","sha256":"SHA","size":"10","archive":"zip","exe":"bin/gh.exe"}""", "`size` is not a whole number of bytes above 0", null)]
    [InlineData("no archive", """{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"exe":"bin/gh.exe"}""", "`archive` is not zip or tar.gz", null)]
    [InlineData("an archive nobody reads", """{"url":"https://example.org/gh.7z","sha256":"SHA","size":10,"archive":"7z","exe":"bin/gh.exe"}""", "`archive` is not zip or tar.gz", null)]
    [InlineData("an archive in capitals", """{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"ZIP","exe":"bin/gh.exe"}""", "`archive` is not zip or tar.gz", null)]
    [InlineData("no executable", """{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip"}""", "it names no `exe`", null)]
    [InlineData("an executable that climbs out", """{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"../gh.exe"}""", "`exe` is not a relative path inside the archive", null)]
    [InlineData("an executable with a backslash", """{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin\\gh.exe"}""", "`exe` is not a relative path inside the archive", null)]
    [InlineData("an executable from the root", """{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"/gh.exe"}""", "`exe` is not a relative path inside the archive", null)]
    [InlineData("an executable on a drive", """{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"C:/gh.exe"}""", "`exe` is not a relative path inside the archive", null)]
    [InlineData("an executable that is a folder", """{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin/"}""", "`exe` is not a relative path inside the archive", null)]
    [InlineData("folders that climb out", """{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe","paths":["../bin"]}""", "`paths` is not a list of folders inside the archive", null)]
    [InlineData("no folders", """{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe","paths":[]}""", "`paths` is not a list of folders inside the archive", null)]
    [InlineData("folders that are text", """{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe","paths":"bin"}""", "`paths` is not a list of folders inside the archive", null)]
    [InlineData("a folder that is a number", """{"url":"https://example.org/gh.zip","sha256":"SHA","size":10,"archive":"zip","exe":"bin/gh.exe","paths":["bin",7]}""", "`paths` is not a list of folders inside the archive", null)]
    public void A_file_reads_as_the_cli_reads_it(string name, string file, string? why, string? paths)
    {
        var list = ToolResources.Parse(Expand(OneFile(file)), "a list");
        Assert.True(list.Problem is null, name);
        var read = list.Tools.TryGetValue("gh", out var gh) && gh.Versions.TryGetValue("2.62.0", out var files)
            && files.TryGetValue("win-x64", out var found) ? found : null;

        if (why is null)
        {
            Assert.True(list.Notes.Count == 0, $"{name}: {string.Join(" / ", list.Notes)}");
            Assert.Equal(paths, read is null ? null : string.Join(',', read.Paths));
        }
        else
        {
            Assert.True(read is null, $"{name}: a file that does not read is skipped");
            Assert.True(list.Notes.Count == 1, $"{name}: {string.Join(" / ", list.Notes)}");
            Assert.StartsWith("a list: `gh` 2.62.0 for win-x64 is skipped — ", list.Notes[0]);
            Assert.True(list.Notes[0].Contains(why, StringComparison.Ordinal), $"{name}: {list.Notes[0]}");
        }
    }

    [Fact]
    public void A_file_as_read_keeps_the_hash_in_lower_case_and_every_field_as_the_list_names_it()
    {
        var upper = string.Concat(Enumerable.Repeat("AB", 32));
        var list = ToolResources.Parse(
            OneFile($$"""{"url":"https://example.org/gh.zip","sha256":"{{upper}}","size":15512013,"archive":"zip","exe":"bin/gh.exe"}"""),
            "a list");

        var file = list.Tools["gh"].Versions["2.62.0"]["win-x64"];
        Assert.Equal(
            ("https://example.org/gh.zip", upper.ToLowerInvariant(), 15512013L, "zip", "bin/gh.exe", "bin"),
            (file.Url, file.Sha256, file.Size, file.Archive, file.Exe, string.Join(',', file.Paths)));
    }

    [Fact]
    public void A_lists_source_and_licence_read_with_the_tool_and_a_byte_order_mark_is_not_part_of_the_text()
    {
        var list = ToolResources.Parse(
            "\uFEFF" + Expand("""{"schema":1,"tools":{"git":{"source":"https://github.com/git-for-windows/git/releases","licence":{"id":"GPL-2.0-only","url":"https://example.org/COPYING"},"versions":{}},"gh":{"licence":{"id":"MIT"},"versions":{"2.62.0":{"files":{"win-x64":FILE}}}}}}"""),
            "a list");

        Assert.Null(list.Problem);
        Assert.Equal("https://github.com/git-for-windows/git/releases", list.Tools["git"].Source);
        Assert.Equal(new ToolLicence("GPL-2.0-only", "https://example.org/COPYING"), list.Tools["git"].Licence);
        Assert.Empty(list.Tools["git"].Versions);
        Assert.Equal(new ToolLicence("MIT", null), list.Tools["gh"].Licence);
        Assert.Null(list.Tools["gh"].Source);
    }

    // ── The merge (§3.4). The CLI's `lists merge as the driver merges them` ──────────────────────────────

    /// <summary>
    /// A list, spelled short: downloads separated by <c>; </c>, each <c>&lt;tool&gt; &lt;version&gt; &lt;platform&gt;
    /// &lt;hash letter&gt; &lt;host&gt;</c> and any of <c>size=</c>, <c>archive=</c>, <c>exe=</c>, <c>paths=a,b</c> it
    /// differs by; <c>&lt;tool&gt; licence &lt;id&gt;</c> names a licence; and <c>!</c> is a list that is not JSON.
    /// The CLI's <c>listText</c> spells the same.
    /// </summary>
    private static string ListText(string spec)
    {
        if (spec == "!") return "not json";
        var tools = new JsonObject();
        foreach (var entry in spec.Split("; ", StringSplitOptions.RemoveEmptyEntries))
        {
            var words = entry.Split(' ');
            if (tools[words[0]] is not JsonObject held)
            {
                held = new JsonObject { ["versions"] = new JsonObject() };
                tools[words[0]] = held;
            }

            if (words[1] == "licence")
            {
                held["licence"] = new JsonObject { ["id"] = words[2] };
                continue;
            }

            var (tool, version, platform, letter, host) = (words[0], words[1], words[2], words[3], words[4]);
            var file = new JsonObject
            {
                ["url"] = $"https://{host}/{tool}-{version}.zip",
                ["sha256"] = string.Concat(Enumerable.Repeat(letter, 64)),
                ["size"] = 10,
                ["archive"] = "zip",
                ["exe"] = $"bin/{tool}.exe",
            };
            foreach (var differ in words.Skip(5))
            {
                var (key, value) = (differ[..differ.IndexOf('=')], differ[(differ.IndexOf('=') + 1)..]);
                file[key] = key switch
                {
                    "size" => JsonValue.Create(long.Parse(value, System.Globalization.CultureInfo.InvariantCulture)),
                    "paths" => new JsonArray(value.Split(',').Select(path => (JsonNode?)JsonValue.Create(path)).ToArray()),
                    _ => JsonValue.Create(value),
                };
            }

            var versions = (JsonObject)held["versions"]!;
            if (versions[version] is not JsonObject named)
            {
                named = new JsonObject { ["files"] = new JsonObject() };
                versions[version] = named;
            }

            named["files"]![platform] = file;
        }

        return new JsonObject { ["schema"] = 1, ["tools"] = tools }.ToJsonString();
    }

    /// <summary>What a merge offers, in one line, as the CLI's <c>render</c> says it.</summary>
    private static string Render(MergedResources merged)
    {
        var parts = new List<string>();
        foreach (var tool in merged.Tools)
        {
            if (tool.Lists.Count == 0) continue;
            var text = new StringBuilder($"{tool.Tool} [{string.Join(", ", tool.Lists)}]");
            foreach (var version in tool.Versions) text.Append($" {version.Version}={string.Join('+', version.Urls.Select(url => new Uri(url).Authority))}");
            foreach (var refused in tool.Refused) text.Append($" refused {refused.Version}({refused.Field})");
            text.Append($" newest {tool.Newest ?? "-"}");
            if (tool.Licence is not null) text.Append($" licence {tool.Licence.Id} from {tool.LicenceFrom}");
            parts.Add(text.ToString());
        }

        parts.AddRange(merged.Unknown.Select(unknown => $"{unknown.Tool}? [{string.Join(", ", unknown.Lists)}]"));
        return string.Join(" | ", parts);
    }

    private static MergedResources Merged(string? first, string? second, string? builtIn, string? platform) =>
        ToolResources.Merge(
            new[] { ("first", first), ("second", second), ("built in", builtIn) }
                .Where(list => list.Item2 is not null)
                .Select(list => ToolResources.Parse(ListText(list.Item2!), list.Item1))
                .ToList(),
            platform);

    [Theory]
    [InlineData("nothing read", null, null, null, "win-x64", "")]
    [InlineData("the list built in, alone", null, null, "gh 2.62.0 win-x64 a maker.example", "win-x64", "gh [built in] 2.62.0=maker.example newest 2.62.0")]
    [InlineData("a location adds a newer version", "gh 2.63.0 win-x64 b mirror.example", null, "gh 2.62.0 win-x64 a maker.example", "win-x64", "gh [first, built in] 2.63.0=mirror.example 2.62.0=maker.example newest 2.63.0")]
    [InlineData("lists that agree under other addresses are mirrors, the person’s first", "gh 2.62.0 win-x64 a mirror.example", null, "gh 2.62.0 win-x64 a maker.example", "win-x64", "gh [first, built in] 2.62.0=mirror.example+maker.example newest 2.62.0")]
    [InlineData("one address named twice is tried once", "gh 2.62.0 win-x64 a maker.example", null, "gh 2.62.0 win-x64 a maker.example", "win-x64", "gh [first, built in] 2.62.0=maker.example newest 2.62.0")]
    [InlineData("lists that disagree on the hash refuse that version, and only that one", "gh 2.62.0 win-x64 b other.example; gh 2.63.0 win-x64 c other.example", null, "gh 2.62.0 win-x64 a maker.example", "win-x64", "gh [first, built in] 2.63.0=other.example refused 2.62.0(sha256) newest 2.63.0")]
    [InlineData("lists that disagree on the size", "gh 2.62.0 win-x64 a other.example size=11", null, "gh 2.62.0 win-x64 a maker.example", "win-x64", "gh [first, built in] refused 2.62.0(size) newest -")]
    [InlineData("lists that disagree on the archive", "gh 2.62.0 win-x64 a other.example archive=tar.gz", null, "gh 2.62.0 win-x64 a maker.example", "win-x64", "gh [first, built in] refused 2.62.0(archive) newest -")]
    [InlineData("lists that disagree on the executable", "gh 2.62.0 win-x64 a other.example exe=gh.exe", null, "gh 2.62.0 win-x64 a maker.example", "win-x64", "gh [first, built in] refused 2.62.0(exe) newest -")]
    [InlineData("lists that disagree on the folders for PATH", "gh 2.62.0 win-x64 a other.example paths=bin,.", null, "gh 2.62.0 win-x64 a maker.example", "win-x64", "gh [first, built in] refused 2.62.0(paths) newest -")]
    [InlineData("a third list refuses what two agreed on", "gh 2.62.0 win-x64 a mirror.example", "gh 2.62.0 win-x64 a other.example", "gh 2.62.0 win-x64 b maker.example", "win-x64", "gh [first, second, built in] refused 2.62.0(sha256) newest -")]
    [InlineData("a disagreement on another platform refuses nothing here", "gh 2.62.0 linux-x64 b other.example", null, "gh 2.62.0 win-x64 a maker.example; gh 2.62.0 linux-x64 a maker.example", "win-x64", "gh [first, built in] 2.62.0=maker.example newest 2.62.0")]
    [InlineData("and refuses it on its own platform", "gh 2.62.0 linux-x64 b other.example", null, "gh 2.62.0 win-x64 a maker.example; gh 2.62.0 linux-x64 a maker.example", "linux-x64", "gh [first, built in] refused 2.62.0(sha256) newest -")]
    [InlineData("a version for another platform is not offered here", null, null, "gh 2.62.0 linux-x64 a maker.example", "win-x64", "gh [built in] newest -")]
    [InlineData("the newest is by number, whichever list names it", "gh 2.9.0 win-x64 a m.example", null, "gh 2.10.0 win-x64 b m.example", "win-x64", "gh [first, built in] 2.10.0=m.example 2.9.0=m.example newest 2.10.0")]
    [InlineData("a refused version is never the newest", "gh 2.63.0 win-x64 b x.example", "gh 2.63.0 win-x64 c y.example", "gh 2.62.0 win-x64 a maker.example", "win-x64", "gh [first, second, built in] 2.62.0=maker.example refused 2.63.0(sha256) newest 2.62.0")]
    [InlineData("a tool this build does not run offers nothing, and is named", "bun 1.2.0 win-x64 a x.example", null, "gh 2.62.0 win-x64 a maker.example", "win-x64", "gh [built in] 2.62.0=maker.example newest 2.62.0 | bun? [first]")]
    [InlineData("a list that does not read offers nothing", "!", null, "gh 2.62.0 win-x64 a maker.example", "win-x64", "gh [built in] 2.62.0=maker.example newest 2.62.0")]
    [InlineData("the licence is the first list’s that names one", "gh licence Apache-2.0; gh 2.63.0 win-x64 b m.example", null, "gh licence MIT; gh 2.62.0 win-x64 a maker.example", "win-x64", "gh [first, built in] 2.63.0=m.example 2.62.0=maker.example newest 2.63.0 licence Apache-2.0 from first")]
    [InlineData("a list naming no licence leaves it to the next", "gh 2.63.0 win-x64 b m.example", null, "gh licence MIT; gh 2.62.0 win-x64 a maker.example", "win-x64", "gh [first, built in] 2.63.0=m.example 2.62.0=maker.example newest 2.63.0 licence MIT from built in")]
    [InlineData("no platform offers nothing", null, null, "gh 2.62.0 win-x64 a maker.example", null, "gh [built in] newest -")]
    [InlineData("every tool in the declared order", null, null, "gh 2.62.0 win-x64 a m.example; git 2.56.0 win-x64 b m.example", "win-x64", "git [built in] 2.56.0=m.example newest 2.56.0 | gh [built in] 2.62.0=m.example newest 2.62.0")]
    public void Lists_merge_as_the_cli_merges_them(string name, string? first, string? second, string? builtIn, string? platform, string offers)
    {
        Assert.True(offers == Render(Merged(first, second, builtIn, platform)), $"{name}: {Render(Merged(first, second, builtIn, platform))}");
    }

    [Fact]
    public void A_refusal_names_both_lists_and_both_hashes_and_nothing_is_fetched_until_one_changes()
    {
        var merged = Merged("gh 2.62.0 win-x64 b other.example", null, "gh 2.62.0 win-x64 a maker.example", "win-x64");
        var refused = Assert.Single(merged.Tools.Single(tool => tool.Tool == "gh").Refused);

        Assert.Equal("2.62.0", refused.Version);
        Assert.Equal("sha256", refused.Field);
        Assert.Equal(["first", "built in"], refused.Lists);
        Assert.Equal(
            $"GitHub CLI 2.62.0 for win-x64 is refused: first names its sha256 as {new string('b', 64)}, and built in as "
            + $"{new string('a', 64)}. Two lists that disagree on one download refuse it, and nothing is fetched until one of them changes",
            refused.Problem);
        Assert.Equal([refused.Problem], merged.Notes);
    }

    [Fact]
    public void An_offered_version_carries_every_address_and_every_list_that_names_it_and_its_file_once()
    {
        var merged = Merged("gh 2.62.0 win-x64 a mirror.example", null, "gh 2.62.0 win-x64 a maker.example", "win-x64");
        var offered = Assert.Single(merged.Tools.Single(tool => tool.Tool == "gh").Versions);

        Assert.Equal(("2.62.0", Hash, 10L, "zip", "bin/gh.exe", "bin"), (offered.Version, offered.Sha256, offered.Size, offered.Archive, offered.Exe, string.Join(',', offered.Paths)));
        Assert.Equal(["https://mirror.example/gh-2.62.0.zip", "https://maker.example/gh-2.62.0.zip"], offered.Urls);
        Assert.Equal(["first", "built in"], offered.Lists);
        Assert.Equal(Tools.Declared.Select(tool => tool.Id), merged.Tools.Select(tool => tool.Tool));
    }

    [Fact]
    public void A_merge_carries_every_lists_problem_and_notes_in_read_order()
    {
        var merged = ToolResources.Merge(
            [ToolResources.Parse("not json", "first"), ToolResources.Parse("""{"schema":1,"tools":{"bun":{}}}""", "built in")], "win-x64");

        Assert.Equal(2, merged.Notes.Count);
        Assert.StartsWith("first is not readable JSON", merged.Notes[0]);
        Assert.Equal("built in names `bun`, which is not a tool this build runs: nothing of it is offered", merged.Notes[1]);
    }

    // ── Where the lists are (§3.1, §3.3). The CLI's `a location's copy is named…` and `a list is vouched for…` ──

    [Theory]
    [InlineData("https://example.org/daoris/resources.json", "83e03ee627324bedbd8aaea591e2a14753db2074954ef09fc29f5417fb769fe8.json")]
    [InlineData("HTTPS://EXAMPLE.ORG/resources.json", "8ef852c6746bfde60ad9bf0f202e88b307be50a9bd8007665d05c16a72ef0da8.json")]
    [InlineData("http://localhost:8080/resources.json", "676166cf6d6760078d2ef005ed1c378e9e067a9b97106e3e9b6bd8bbb5b4c703.json")]
    [InlineData("https://例え.jp/r.json", "e2e2d45ac6cca9317d9ebea32cf02ce29eb338fe0926e0e2eecdd7111a125f9b.json")]
    public void A_location_s_copy_is_named_as_the_cli_names_it(string address, string name)
    {
        Assert.Equal(Path.Combine(["HOME", .. ToolResources.LocationsFolder, name]), ToolResources.LocationCopy("HOME", address));
    }

    [Theory]
    [InlineData("https://example.org/daoris/resources.json", "example.org")]
    [InlineData("HTTPS://EXAMPLE.ORG/resources.json", "example.org")]
    [InlineData("https://example.org:8443/resources.json", "example.org:8443")]
    [InlineData("https://例え.jp/r.json", "xn--r8jz45g.jp")]
    [InlineData("http://localhost:8080/resources.json", "this machine")]
    [InlineData("http://127.0.0.1/resources.json", "this machine")]
    [InlineData("http://[::1]:5177/resources.json", "this machine")]
    [InlineData("https://localhost:8443/resources.json", "this machine")]
    public void A_list_is_vouched_for_as_the_cli_says(string address, string integrity)
    {
        Assert.Equal(integrity, ToolResources.IntegrityOf(address));
    }

    [Fact]
    public void The_lists_are_the_persons_locations_in_order_then_the_one_built_in_beside_the_home()
    {
        const string first = "https://mirror.example/resources.json";
        const string second = "http://localhost:5177/resources.json";
        File.WriteAllText(Path.Combine(Home, Tools.FileName), $$"""{"locations":["{{first}}","{{second}}"]}""");
        var firstText = ListText("gh 2.63.0 win-x64 b mirror.example");
        Directory.CreateDirectory(Path.Combine([Home, .. ToolResources.LocationsFolder]));
        File.WriteAllText(ToolResources.LocationCopy(Home, first), firstText);
        Directory.CreateDirectory(Path.Combine(_root, "app"));
        File.WriteAllText(Path.Combine([_root, .. ToolResources.Layout]), ListText("gh 2.62.0 win-x64 a maker.example"));

        // A folder for the application that holds no list, so the one beside the home is read.
        var lists = ToolResources.ReadLists(Home, Path.Combine(_root, "elsewhere"));

        Assert.Equal(
            [(first, "mirror.example", true), (second, "this machine", false), (ToolResources.BuiltIn, "built in", true)],
            lists.Select(list => (list.Origin, list.Integrity, list.Exists)));
        Assert.Equal(ToolResources.LocationCopy(Home, first), lists[0].Path);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(firstText))), lists[0].Sha256);
        Assert.Equal([$"{second} has not been fetched yet, so it names nothing"], lists[1].Notes);
        Assert.Equal(ToolResources.BesideHome(Home), lists[2].Path);
        Assert.Equal(
            $"gh [{first}, {ToolResources.BuiltIn}] 2.63.0=mirror.example 2.62.0=maker.example newest 2.63.0",
            Render(ToolResources.Merge(lists, "win-x64")));
    }

    [Fact]
    public void The_list_built_in_is_the_one_beside_the_application_first()
    {
        var application = Path.Combine(_root, "application");
        Directory.CreateDirectory(application);
        File.WriteAllText(Path.Combine(application, ToolResources.FileName), ListText("gh 2.62.0 win-x64 a beside.example"));
        Directory.CreateDirectory(Path.Combine(_root, "app"));
        File.WriteAllText(Path.Combine([_root, .. ToolResources.Layout]), ListText("gh 2.62.0 win-x64 a home.example"));

        Assert.Equal(Path.Combine(application, ToolResources.FileName), ToolResources.BuiltInFor(Home, application));
        Assert.Equal(ToolResources.BesideHome(Home), ToolResources.BuiltInFor(Home, Path.Combine(_root, "elsewhere")));
        Assert.Equal(Path.Combine([_root, .. ToolResources.Layout]), ToolResources.BesideHome(Home));
        Assert.Equal(
            "gh [the built-in list] 2.62.0=beside.example newest 2.62.0",
            Render(ToolResources.Merge(ToolResources.ReadLists(Home, application), "win-x64")));
    }

    [Fact]
    public void A_home_with_no_install_beside_it_has_no_list_built_in_and_says_so()
    {
        var lists = ToolResources.ReadLists(Home, Path.Combine(_root, "elsewhere"));

        var list = Assert.Single(lists);
        Assert.Equal((ToolResources.BuiltIn, false, (string?)null), (list.Origin, list.Exists, list.Problem));
        Assert.Equal([$"no list is built in beside this home ({ToolResources.BesideHome(Home)}), so only the locations are read"], list.Notes);
    }

    [Fact]
    public void A_copy_is_hashed_as_its_bytes_and_read_as_its_text()
    {
        var path = Path.Combine(Home, "copy.json");
        File.WriteAllBytes(path, [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("{\"schema\":1,\"tools\":{}}\n")]);

        var list = ToolResources.ReadFile(path, "https://example.org/r.json");
        Assert.Equal(
            (true, (string?)null, "example.org", "b649957df9c285c1ab0d01306767157f8a6a9e74d4718da94a7ea5c7bebfcd3c"),
            (list.Exists, list.Problem, list.Integrity, list.Sha256));

        File.WriteAllText(path, "{\"schema\":1,\"tools\":{}}\n");
        Assert.Equal("c5dd286d689d840e89162ec6c963a65cac731a88e08d4b8389c416d88185ac3b", ToolResources.ReadFile(path, "https://example.org/r.json").Sha256);
    }

    // ── The list built in (§3.1, §3.2), beside this build as it is beside an installed application ──────

    [Fact]
    public void The_list_built_in_is_beside_the_driver_reads_whole_and_names_one_version_of_every_tool_for_win_x64()
    {
        var path = ToolResources.BesideApplication(AppContext.BaseDirectory);
        Assert.True(File.Exists(path), $"the build carries no {ToolResources.FileName} beside the driver: {path}");

        var list = ToolResources.ReadFile(path, ToolResources.BuiltIn);
        Assert.Null(list.Problem);
        Assert.Empty(list.Notes);
        Assert.Equal("built in", list.Integrity);
        Assert.Equal(Tools.Declared.Select(tool => tool.Id).Order(StringComparer.Ordinal), list.Tools.Keys.Order(StringComparer.Ordinal));

        foreach (var tool in ToolResources.Merge([list], "win-x64").Tools)
        {
            var offered = Assert.Single(tool.Versions);
            Assert.Empty(tool.Refused);
            Assert.Equal(offered.Version, tool.Newest);
            Assert.StartsWith("https://", tool.Source);
            Assert.StartsWith("https://", tool.Licence?.Url);
            Assert.Single(offered.Urls);
        }
    }
}
