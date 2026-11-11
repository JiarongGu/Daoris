using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What the place's world reads of a room, over a real checkout in the repository's gitignored <c>_fixtures</c>. The
/// repository's declaration is read as git objects at its line's tip, never from the working files, and the path is judged
/// against the tree there: through a link, onto a file, or past a submodule's commit.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class PlaceRoomWorldTests : IDisposable
{
    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "place-room", Guid.NewGuid().ToString("N")[..8]);

    public PlaceRoomWorldTests() => Directory.CreateDirectory(_scratch);

    public void Dispose() => GitSeed.Remove(_scratch);

    private static readonly DriverConfig Config = DriverConfig.Empty;

    private string Root => Path.Combine(_scratch, "engine");

    private static PlaceWorld World() => new(new ServiceClient("http://stand-in", null, new HttpClient(new Refusing())));

    private RepoView Row(string? root = null) => new("engine", true, root ?? Root, "aurora");

    private async Task WriteAsync(string path, string text)
    {
        var file = Path.Combine(Root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, text);
    }

    private async Task<string> CommitAsync(string message = "declare")
    {
        await GitFixture.GitAsync(Root, "add", "-A");
        await GitFixture.GitAsync(Root, "commit", "--quiet", "-m", message);
        return (await GitFixture.GitAsync(Root, "rev-parse", "HEAD")).Trim();
    }

    private async Task LinkAsync(string path, string mode, string objectId)
    {
        await GitFixture.GitAsync(Root, "update-index", "--add", "--cacheinfo", $"{mode},{objectId},{path}");
        await GitFixture.GitAsync(Root, "commit", "--quiet", "-m", $"link {path}");
    }

    [Fact]
    public async Task The_declaration_is_read_as_the_line_holds_it_the_rooms_the_lanes_and_the_folders_with_text_of_their_own()
    {
        await GitSeed.PlainAsync(Root, "engine");
        await WriteAsync("daoris.json", """{ "rooms": ["docs", "src/api/"], "source": "daoris@0.0.1" }""");
        await WriteAsync("daoris.lanes.json", """
            { "lanes": [ { "id": "web-shell", "room": "web", "paths": ["web/**"] }, { "id": "api", "paths": ["src/api/**"], "builds": { "units": ["src/api/api.csproj"] } } ] }
            """);
        await WriteAsync("docs/AGENTS.md", "# Docs\n");
        await WriteAsync(".claude/skills/x/AGENTS.md", "# not a room\n");
        var commit = await CommitAsync();

        var read = await World().RoomAsync(Row(), Config, "tools/web", CancellationToken.None);

        Assert.Null(read.Problem);
        Assert.Equal(("main", commit, true), (read.Line, read.Commit, read.Adopted));
        Assert.Equal(["docs", "src/api"], read.Declared);
        Assert.Equal(["web", "api"], read.Lanes);
        Assert.Equal(["docs"], read.Folders);
        Assert.Equal((null, false), (read.Through, read.IsFile));
    }

    [Fact]
    public async Task What_is_in_flight_in_the_working_files_is_never_read()
    {
        await GitSeed.PlainAsync(Root, "engine");
        await WriteAsync("daoris.json", """{ "rooms": ["docs"] }""");
        await CommitAsync();
        await WriteAsync("daoris.json", """{ "rooms": ["docs", "tools/web"] }""");
        await WriteAsync("tools/web/AGENTS.md", "# in flight\n");

        var read = await World().RoomAsync(Row(), Config, "tools/web", CancellationToken.None);

        Assert.Equal(["docs"], read.Declared);
        Assert.Empty(read.Folders);
    }

    [Fact]
    public async Task A_line_with_no_manifest_is_not_adopted_and_declares_nothing()
    {
        await GitSeed.PlainAsync(Root, "engine");

        var read = await World().RoomAsync(Row(), Config, "tools/web", CancellationToken.None);

        Assert.Null(read.Problem);
        Assert.False(read.Adopted);
        Assert.Empty(read.Declared);
        Assert.Empty(read.Lanes);
    }

    [Fact]
    public async Task A_link_a_file_and_a_submodules_commit_on_the_line_are_found_on_the_path()
    {
        await GitSeed.PlainAsync(Root, "engine");
        await WriteAsync("daoris.json", "{}");
        await WriteAsync("tools/note.txt", "a file\n");
        await CommitAsync();
        var target = (await GitFixture.RunWithAsync(Root, null, "../elsewhere", "hash-object", "-w", "--stdin")).Stdout.Trim();
        await LinkAsync("linked", "120000", target);
        var head = (await GitFixture.GitAsync(Root, "rev-parse", "HEAD")).Trim();
        await LinkAsync("vendor", "160000", head);

        var world = World();
        var through = await world.RoomAsync(Row(), Config, "linked/web", CancellationToken.None);
        var deeper = await world.RoomAsync(Row(), Config, "linked/a/b", CancellationToken.None);
        var onFile = await world.RoomAsync(Row(), Config, "tools/note.txt", CancellationToken.None);
        var underFile = await world.RoomAsync(Row(), Config, "tools/note.txt/x", CancellationToken.None);
        var submodule = await world.RoomAsync(Row(), Config, "vendor/lib", CancellationToken.None);
        var plain = await world.RoomAsync(Row(), Config, "tools/web", CancellationToken.None);

        Assert.Equal(("linked", false), (through.Through, through.IsFile));
        Assert.Equal("linked", deeper.Through);
        Assert.True(onFile.IsFile);
        Assert.Equal("tools/note.txt", underFile.Through);
        Assert.Equal("vendor", submodule.Through);
        Assert.Equal((null, false), (plain.Through, plain.IsFile));
    }

    [Fact]
    public async Task A_row_with_no_checkout_here_and_a_folder_that_is_no_repository_say_why_they_were_not_read()
    {
        var world = World();
        Directory.CreateDirectory(Path.Combine(_scratch, "plain-folder"));

        var rootless = await world.RoomAsync(new RepoView("engine", true, null, "aurora"), Config, "tools/web", CancellationToken.None);
        var folder = await world.RoomAsync(Row(Path.Combine(_scratch, "plain-folder")), Config, "tools/web", CancellationToken.None);

        Assert.Contains("has no checkout", rootless.Problem);
        Assert.False(string.IsNullOrWhiteSpace(folder.Problem));
        Assert.Null(folder.Commit);
    }

    private sealed class Refusing : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("a room's read asks the service nothing");
    }
}
