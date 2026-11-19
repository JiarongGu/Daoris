using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An install's update reading Daoris's own releases: the newest published release, newer than the build running, fetched
/// by its checksum, unpacked as a staged build and held to its own manifest, over a stand-in transport, never the network.
/// </summary>
public sealed class UpdateFetchTests : ScratchInstall
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
    private const string Latest = "https://api.github.com/repos/JiarongGu/Daoris/releases/latest";
    private const string ByTag = "https://api.github.com/repos/JiarongGu/Daoris/releases/tags/v";
    private const string Download = "https://github.test/releases/download/";

    private string Scratch => Path.Combine(TestHomes.Root, "daoris-fetch-" + Path.GetFileName(_install));

    [Fact]
    public async Task The_newest_release_is_fetched_held_to_its_checksum_and_staged_as_a_build_the_install_will_check()
    {
        var served = Release("0.2.0");

        var (code, said) = await UpdateFetch.RunAsync(_install, "0.1.0+abc1234", CancellationToken.None, New(served));

        Assert.True(code == 0, said);
        Assert.Contains("Daoris 0.2.0 is staged", said);
        Assert.True(StagedBuild.IsStaged(_install));
        Assert.Null(StagedBuild.Check(_install));
        Assert.Equal("0.2.0", StagedBuild.Read(_install, out _)!.Version);
        Assert.Equal("new launcher", Read("update/staged/Daoris.exe"));
        // Nothing of the fetch is left beside the staged build, and the build it replaces at the swap is untouched.
        Assert.Equal(["staged"], Directory.GetDirectories(Path.Combine(_install, "update")).Select(Path.GetFileName));
        AssertTheBuildBefore();
        Assert.Equal([Latest, ByTag + "0.2.0", Download + "Daoris-0.2.0-win-x64.zip.sha256", Download + "Daoris-0.2.0-win-x64.zip"], served.Asked);
    }

    [Fact]
    public async Task A_release_no_newer_than_the_build_running_stages_nothing_and_fetches_no_package()
    {
        var served = Release("0.2.0");

        var (code, said) = await UpdateFetch.RunAsync(_install, "0.2.0+abc1234", CancellationToken.None, New(served));

        Assert.Equal(0, code);
        Assert.Contains("is the newest release", said);
        Assert.False(StagedBuild.IsStaged(_install));
        Assert.Equal([Latest], served.Asked);
    }

    [Fact]
    public async Task A_package_that_is_not_the_one_its_checksum_lists_is_refused_and_nothing_is_staged()
    {
        var served = Release("0.2.0", listedHash: new string('0', 64));

        var (code, said) = await UpdateFetch.RunAsync(_install, "0.1.0", CancellationToken.None, New(served));

        Assert.Equal(1, code);
        Assert.Contains("is not the one Daoris 0.2.0 publishes", said);
        Assert.False(StagedBuild.IsStaged(_install));
        Assert.Empty(Directory.GetDirectories(Path.Combine(_install, "update")));
    }

    [Fact]
    public async Task Two_published_hashes_that_disagree_are_refused_before_the_package_is_downloaded()
    {
        var served = Release("0.2.0", apiDigest: new string('1', 64));

        var (code, said) = await UpdateFetch.RunAsync(_install, "0.1.0", CancellationToken.None, New(served));

        Assert.Equal(1, code);
        Assert.Contains("two published hashes", said);
        Assert.DoesNotContain(Download + "Daoris-0.2.0-win-x64.zip", served.Asked);
    }

    [Fact]
    public async Task A_package_whose_manifest_names_another_version_is_removed_with_the_sentence()
    {
        var served = Release("0.2.0", builtAs: "0.9.0");

        var (code, said) = await UpdateFetch.RunAsync(_install, "0.1.0", CancellationToken.None, New(served));

        Assert.Equal(1, code);
        Assert.Contains("the package says it is Daoris 0.9.0, and the release is 0.2.0", said);
        Assert.False(StagedBuild.IsStaged(_install));
    }

    [Fact]
    public async Task A_package_that_misses_a_file_its_manifest_names_is_removed_not_staged()
    {
        var served = Release("0.2.0", dropFromPackage: "app/Daoris.Desktop.App.dll");

        var (code, said) = await UpdateFetch.RunAsync(_install, "0.1.0", CancellationToken.None, New(served));

        Assert.Equal(1, code);
        Assert.Contains("not a build this install installs", said);
        Assert.False(StagedBuild.IsStaged(_install));
    }

    [Fact]
    public async Task A_draft_or_a_missing_release_is_no_release_so_nothing_is_fetched()
    {
        var (code, said) = await UpdateFetch.RunAsync(_install, "0.1.0", CancellationToken.None, New(new Served([])));

        Assert.Equal(1, code);
        Assert.Contains("has published no release", said);
        Assert.False(StagedBuild.IsStaged(_install));
    }

    [Fact]
    public async Task A_build_already_staged_is_installed_first()
    {
        Stage();
        var served = Release("0.2.0");

        var (code, said) = await UpdateFetch.RunAsync(_install, "0.1.0", CancellationToken.None, New(served));

        Assert.Equal(1, code);
        Assert.Contains("is already staged", said);
        Assert.Empty(served.Asked);
        Assert.Equal("new launcher", Read("update/staged/Daoris.exe"));
    }

    [Fact]
    public void The_door_stages_the_release_and_a_session_s_terminal_is_refused_it()
    {
        var served = Release("9.9.9");

        Assert.Equal(1, UpdateCommand.Run(["--fetch"], Path.Combine(_install, "data"), () => Now, null, "s-1", out var refused, New(served)));
        Assert.Contains(DriverChoicesCommand.SessionsTerminal, refused);
        Assert.Empty(served.Asked);

        Assert.Equal(0, UpdateCommand.Run(["--fetch"], Path.Combine(_install, "data"), () => Now, null, null, out var said, New(served)));
        Assert.Contains("Daoris 9.9.9 is staged", said);
        Assert.Equal("9.9.9", StagedBuild.Read(_install, out _)!.Version);

        Assert.Equal(2, UpdateCommand.Run(["--fetch", "--now"], Path.Combine(_install, "data"), () => Now, null, null, out said, New(served)));
        Assert.Contains("not two", said);
    }

    private static ReleaseFetch New(Served served) => new(served, "win-x64", null);

    /// <summary>A published release of <paramref name="version"/>: its metadata, its checksum file and its package, as the workflow attaches them.</summary>
    private Served Release(string version, string? listedHash = null, string? apiDigest = null, string? builtAs = null, string? dropFromPackage = null)
    {
        var package = Zip(builtAs ?? version, dropFromPackage);
        var name = DaorisReleases.Package(version);
        var actual = Convert.ToHexStringLower(SHA256.HashData(package));
        var asset = (string file, string? digest = null) => new JsonObject
        {
            ["name"] = file,
            ["browser_download_url"] = Download + file,
            ["digest"] = digest is null ? null : "sha256:" + digest,
        };
        var metadata = new JsonObject
        {
            ["tag_name"] = "v" + version,
            ["assets"] = new JsonArray(asset(name, apiDigest), asset(name + ".sha256")),
        }.ToJsonString();

        return new Served(new()
        {
            [Latest] = Encoding.UTF8.GetBytes(new JsonObject { ["tag_name"] = "v" + version }.ToJsonString()),
            [ByTag + version] = Encoding.UTF8.GetBytes(metadata),
            [Download + name + ".sha256"] = Encoding.UTF8.GetBytes($"{listedHash ?? actual}  {name}\n"),
            [Download + name] = package,
        });
    }

    /// <summary>A package as the workflow writes it: a staged build, its manifest among its files, zipped.</summary>
    private byte[] Zip(string version, string? drop)
    {
        var folder = Path.Combine(Scratch, "package");
        Directory.CreateDirectory(Path.Combine(folder, "app"));
        File.WriteAllText(Path.Combine(folder, "INSTALLED.md"), "# Daoris — installed desktop\nnew\n");
        File.WriteAllText(Path.Combine(folder, "Daoris.exe"), "new launcher");
        File.WriteAllText(Path.Combine(folder, "app", "Daoris.Desktop.exe"), "new application");
        File.WriteAllText(Path.Combine(folder, "app", "Daoris.Desktop.App.dll"), "new library");

        var files = new JsonArray();
        foreach (var full in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            files.Add(new JsonObject
            {
                ["path"] = Path.GetRelativePath(folder, full).Replace(Path.DirectorySeparatorChar, '/'),
                ["size"] = new FileInfo(full).Length,
                ["sha256"] = StagedBuild.Sha256Of(full),
            });
        }

        File.WriteAllText(Path.Combine(folder, "build.json"), new JsonObject
        {
            ["schema"] = 1, ["id"] = "b-" + version, ["version"] = version, ["commit"] = null, ["at"] = "2026-11-19T12:00:00Z", ["files"] = files,
        }.ToJsonString());
        if (drop is not null) File.Delete(Path.Combine(folder, drop.Replace('/', Path.DirectorySeparatorChar)));

        var archive = Path.Combine(Scratch, "package.zip");
        ZipFile.CreateFromDirectory(folder, archive);
        var bytes = File.ReadAllBytes(archive);
        Directory.Delete(Scratch, recursive: true);
        return bytes;
    }

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
