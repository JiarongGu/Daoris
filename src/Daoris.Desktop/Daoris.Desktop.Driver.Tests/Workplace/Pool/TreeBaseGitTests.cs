using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A kept output over real git: a take whose start's rules say <c>base</c> keeps the previous line's declared configuration folders byte
/// for byte, marked and recorded in the claim before the switch, and sets every other entry under the top aside; a top holding a path
/// the start tracks is never moved whole; the undo of a refused take takes back the bases its plan wrote, with their markers.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeBaseGitTests : IDisposable
{
    private const string Rules = """
        { "form": 2, "rules": [
          { "kind": "output", "paths": ["bin/"], "take": "base" },
          { "kind": "output", "paths": ["gen/"] }
        ] }
        """;

    private PoolGitFixture? _fixture;

    public void Dispose() => _fixture?.Dispose();

    private async Task<PoolGitFixture> FixtureAsync()
    {
        var fixture = _fixture = await PoolGitFixture.CreateAsync(Rules);
        await File.AppendAllTextAsync(Path.Combine(fixture.Root, ".gitignore"), "bin/\ngen/\n");
        await GitAsync(fixture.Root, "commit", "--quiet", "-am", "ignore the outputs");
        fixture.Start = (await GitAsync(fixture.Root, "rev-parse", "HEAD")).Trim();
        return fixture;
    }

    /// <summary>A slot made for <paramref name="line"/>, its outputs built, its commit made and the line let go.</summary>
    private static async Task<KeptTree> BuiltAsync(PoolGitFixture fixture, TreePool pool, string line, params string[] outputs)
    {
        var slot = await fixture.MadeAsync(pool, 1, line);
        foreach (var output in outputs)
        {
            var path = Path.Combine(slot.Path, output.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, $"{line}'s\n");
        }

        await File.WriteAllTextAsync(Path.Combine(slot.Path, "work.txt"), $"{line}\n");
        await GitAsync(slot.Path, "add", "work.txt");
        await GitAsync(slot.Path, "commit", "--quiet", "-m", line);
        var ended = await pool.EndAsync(new PoolEnd(fixture.Site, 1, "quest-1", "session-1"));
        Assert.True(ended.Ok, ended.Refused ?? ended.Stuck);
        return ended.Slot!;
    }

    [Fact]
    public async Task A_base_keeps_its_declared_configurations_marked_and_recorded_and_every_other_entry_goes_aside()
    {
        var fixture = await FixtureAsync();
        var pool = fixture.Pool();
        var slot = await BuiltAsync(fixture, pool, "s-aaaa0001", "bin/Debug/net10.0/Engine.dll", "bin/Release/net10.0/Engine.dll", "bin/x64/Debug/Engine.dll", "bin/stray.txt");
        var tip = (await GitAsync(slot.Path, "rev-parse", "HEAD")).Trim();

        var taken = await pool.TakeAsync(fixture.Take(1, "s-bbbb0001"));

        Assert.True(taken.Ok, taken.Refused ?? taken.Stuck);
        Assert.Equal("s-aaaa0001's\n", await File.ReadAllTextAsync(Path.Combine(slot.Path, "bin", "Debug", "net10.0", "Engine.dll")));
        Assert.True(File.Exists(Path.Combine(slot.Path, "bin", "Release", "net10.0", "Engine.dll")));
        Assert.False(Directory.Exists(Path.Combine(slot.Path, "bin", "x64")));
        Assert.False(File.Exists(Path.Combine(slot.Path, "bin", "stray.txt")));
        var bases = TreeFilesRecord.Read(fixture.Home, slot.Path)!.Bases;
        Assert.Equal(
            [("bin/Debug/", "s-aaaa0001", tip, "Engine.csproj", "Debug"), ("bin/Release/", "s-aaaa0001", tip, "Engine.csproj", "Release")],
            bases.Select(each => (each.Path, each.From, each.Commit, each.Project, each.Configuration)).Order());
        var standing = BaseMarker.Above(Path.Combine(slot.Path, "bin", "Debug", "net10.0", "Engine.dll"));
        Assert.Equal("s-aaaa0001", standing?.From);
        Assert.Contains("daoris tree build bin/Debug", standing!.Says);
        Assert.Null(BaseMarker.Above(Path.Combine(slot.Path, "work.txt")));
        Assert.Equal("", (await GitAsync(slot.Path, "status", "--porcelain")).Trim());
    }

    [Fact]
    public async Task A_base_records_the_build_its_rule_names_and_one_no_build_releases_is_set_aside()
    {
        var fixture = await FixtureAsync();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "daoris.files.json"), """
            { "form": 2, "rules": [ { "kind": "output", "paths": ["bin/", "out/"], "take": "base" } ] }
            """);
        await File.AppendAllTextAsync(Path.Combine(fixture.Root, ".gitignore"), "out/\n");
        await GitAsync(fixture.Root, "commit", "--quiet", "-am", "keep out/ too");
        fixture.Start = (await GitAsync(fixture.Root, "rev-parse", "HEAD")).Trim();
        var pool = fixture.Pool();
        var slot = await BuiltAsync(fixture, pool, "s-aaaa0001", "bin/Debug/net10.0/Engine.dll", "out/Debug/Engine.dll");

        var taken = await pool.TakeAsync(fixture.Take(1, "s-bbbb0001"));

        Assert.True(taken.Ok, taken.Refused ?? taken.Stuck);
        Assert.Equal("dotnet build -c {configuration}", Assert.Single(TreeFilesRecord.Read(fixture.Home, slot.Path)!.Bases).Builds);
        Assert.False(Directory.Exists(Path.Combine(slot.Path, "out")));
        Assert.Null(BaseMarker.Above(Path.Combine(slot.Path, "out", "Debug", "Engine.dll")));
    }

    [Fact]
    public async Task A_new_sdk_pinned_at_the_start_moves_the_toolchain_and_sets_the_bases_aside_with_no_entry_and_no_marker_left()
    {
        var fixture = await FixtureAsync();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "daoris.files.json"), """
            { "form": 2, "rules": [
              { "kind": "output", "paths": ["bin/"], "take": "base" },
              { "kind": "intermediate", "paths": ["/obj/"], "toolchain": ["git cat-file -p HEAD:global.json"] }
            ] }
            """);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "global.json"), """{ "sdk": { "version": "10.0.100" } }""");
        await GitAsync(fixture.Root, "add", ".");
        await GitAsync(fixture.Root, "commit", "--quiet", "-m", "pin the SDK");
        fixture.Start = (await GitAsync(fixture.Root, "rev-parse", "HEAD")).Trim();
        var pool = fixture.Pool();
        var slot = await BuiltAsync(fixture, pool, "s-aaaa0001", "bin/Debug/net10.0/Engine.dll");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "global.json"), """{ "sdk": { "version": "10.0.200" } }""");
        await GitAsync(fixture.Root, "commit", "--quiet", "-am", "a new SDK");
        var start = (await GitAsync(fixture.Root, "rev-parse", "HEAD")).Trim();

        var taken = await pool.TakeAsync(fixture.Take(1, "s-bbbb0001", start));

        Assert.True(taken.Ok, taken.Refused ?? taken.Stuck);
        Assert.False(File.Exists(Path.Combine(slot.Path, "bin", "Debug", "net10.0", "Engine.dll")));
        Assert.Empty(TreeFilesRecord.Read(fixture.Home, slot.Path)?.Bases ?? []);
        Assert.Empty(Directory.EnumerateFiles(PoolClearing.AsideRoot(fixture.Site), BaseMarker.FileName, SearchOption.AllDirectories));
        Assert.Contains(Directory.EnumerateFiles(PoolClearing.AsideRoot(fixture.Site), "Engine.dll", SearchOption.AllDirectories), path => path.Contains("Debug", StringComparison.Ordinal));
        Assert.Equal("", (await GitAsync(slot.Path, "status", "--porcelain")).Trim());
    }

    [Fact]
    public async Task A_top_holding_a_path_the_start_tracks_is_never_moved_whole_and_its_other_entries_are_sorted_one_by_one()
    {
        var fixture = await FixtureAsync();
        var pool = fixture.Pool();
        var slot = await BuiltAsync(fixture, pool, "s-aaaa0001", "gen/out.txt");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "gen"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "gen", "README.md"), "# generated\n");
        await GitAsync(fixture.Root, "add", "-f", "gen/README.md");
        await GitAsync(fixture.Root, "commit", "--quiet", "-m", "track a file under gen/");
        var start = (await GitAsync(fixture.Root, "rev-parse", "HEAD")).Trim();

        var taken = await pool.TakeAsync(fixture.Take(1, "s-bbbb0001", start));

        Assert.True(taken.Ok, taken.Refused ?? taken.Stuck);
        Assert.Equal("# generated", (await File.ReadAllTextAsync(Path.Combine(slot.Path, "gen", "README.md"))).TrimEnd());
        Assert.False(File.Exists(Path.Combine(slot.Path, "gen", "out.txt")));
        Assert.Equal("", (await GitAsync(slot.Path, "status", "--porcelain")).Trim());
    }

    [Fact]
    public async Task A_take_put_back_takes_back_the_bases_its_plan_wrote_and_their_markers()
    {
        var fixture = await FixtureAsync();
        var slot = await BuiltAsync(fixture, fixture.Pool(), "s-aaaa0001", "bin/Debug/net10.0/Engine.dll");
        var pool = fixture.Pool(seams: new PoolSeams
        {
            Open = (claim, _) => Task.FromResult<string?>($"record-{claim.Line}"),
            BeforeSwitch = folder => File.WriteAllTextAsync(Path.Combine(folder, "planted.txt"), "late\n"),
        });

        var taken = await pool.TakeAsync(fixture.Take(1, "s-bbbb0001"));

        Assert.Contains(SlotJudgement.Changed, taken.Refused);
        Assert.Empty(TreeFilesRecord.Read(fixture.Home, slot.Path)?.Bases ?? []);
        Assert.Null(BaseMarker.Above(Path.Combine(slot.Path, "bin", "Debug", "net10.0", "Engine.dll")));
        Assert.True(File.Exists(Path.Combine(slot.Path, "bin", "Debug", "net10.0", "Engine.dll")));
    }
}
