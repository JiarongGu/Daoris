using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// When the analysis writes the found layer: at registration, where the line moved past a file it reads, where the toolchain
/// moved, and at a finish; a look that changed nothing writes nothing, the file's bytes and write time as they were.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class FoundReadsTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 8, 9, 0, 0, TimeSpan.Zero);

    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "found-reads", Guid.NewGuid().ToString("N")[..8]);
    private readonly List<string> _asked = [];
    private string _version = "v24.0.0";

    public void Dispose() => GitSeed.Discard(_scratch);

    private string Root => Path.Combine(_scratch, "repo");

    private string Home => Path.Combine(_scratch, "home");

    private Task<string> Ask(string root, string command, CancellationToken ct)
    {
        _asked.Add(command);
        return Task.FromResult(command.StartsWith("node ", StringComparison.Ordinal) ? _version : "fixed");
    }

    private void Write(string relative, string content = "x")
    {
        var path = Path.Combine(Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static async Task<string> Git(string folder, params string[] arguments)
    {
        var (code, stdout, stderr) = await WorkingTree.GitAsync(folder, arguments, CancellationToken.None);
        Assert.True(code == 0, $"git {string.Join(' ', arguments)}: {stderr}");
        return stdout.Trim();
    }

    private async Task<string> Commit()
    {
        if (!Directory.Exists(Path.Combine(Root, ".git"))) await Git(Root, "init", "-q", "-b", "main");
        await Git(Root, "add", "-A");
        await Git(Root, "-c", "user.name=t", "-c", "user.email=t@example.invalid", "-c", "commit.gpgsign=false", "commit", "-q", "-m", "c");
        return await Git(Root, "rev-parse", "HEAD");
    }

    private async Task<string> Seed()
    {
        Write(".gitignore", "node_modules/\nstray/\n");
        Write("package.json", """{ "name": "root" }""");
        Write("package-lock.json", """{ "lockfileVersion": 3, "packages": { "": {} } }""");
        Write("src/index.js", "one\n");
        var commit = await Commit();
        Write("node_modules/left-pad/index.js");
        Write("stray/notes.txt");
        return commit;
    }

    private Task<string?> Look(string commit, TimeSpan after) =>
        FoundReads.LookAsync(Home, "repo", Root, commit, Start + after, Ask, CancellationToken.None);

    private (string Text, DateTime Written) Layer() =>
        (File.ReadAllText(FoundFiles.PathOf(Home)), File.GetLastWriteTimeUtc(FoundFiles.PathOf(Home)));

    [Fact]
    public async Task A_repository_s_first_look_writes_its_layer_and_a_look_that_changed_nothing_writes_nothing()
    {
        var commit = await Seed();

        Assert.Equal("it was registered", await Look(commit, TimeSpan.Zero));
        var written = Layer();
        var reading = FoundFiles.Read(Home, "repo").Reading!;
        Assert.Equal(commit, reading.Commit);
        Assert.Equal(FileKind.Unknown, reading.Rules.Single(rule => rule.Paths.Contains("/stray/")).Kind);
        Assert.NotNull(reading.Toolchain);

        await Task.Delay(30);
        Assert.Null(await Look(commit, TimeSpan.FromHours(2)));

        Assert.Equal(written, Layer());
    }

    [Fact]
    public async Task A_line_that_moved_past_a_source_file_alone_writes_nothing_and_past_a_signature_file_or_an_ignore_file_writes_the_layer_again()
    {
        var first = await Seed();
        await Look(first, TimeSpan.Zero);
        var written = Layer();

        Write("src/index.js", "two\n");
        var source = await Commit();
        await Task.Delay(30);
        Assert.Null(await Look(source, TimeSpan.FromHours(1)));
        Assert.Equal(written, Layer());

        Write("package-lock.json", """{ "lockfileVersion": 3, "packages": { "": {}, "node_modules/x": {} } }""");
        var lockfile = await Commit();
        Assert.Equal("its line moved past a file the analysis reads", await Look(lockfile, TimeSpan.FromHours(2)));
        Assert.Equal(lockfile, FoundFiles.Read(Home, "repo").Reading!.Commit);

        Write(".gitignore", "node_modules/\n");
        var ignore = await Commit();
        Assert.Equal("its line moved past a file the analysis reads", await Look(ignore, TimeSpan.FromHours(3)));
        Assert.Equal(ignore, FoundFiles.Read(Home, "repo").Reading!.Commit);
    }

    [Fact]
    public async Task A_toolchain_that_moved_writes_the_layer_again_and_the_toolchain_is_asked_once_a_day()
    {
        var commit = await Seed();
        await Look(commit, TimeSpan.Zero);
        var registered = _asked.Count;
        Assert.True(registered > 0);
        var written = Layer();

        _version = "v25.0.0";
        Assert.Null(await Look(commit, TimeSpan.FromHours(2)));
        Assert.Equal(registered, _asked.Count);
        Assert.Equal(written, Layer());

        Assert.Equal("its toolchain moved", await Look(commit, TimeSpan.FromHours(25)));
        Assert.NotEqual(written.Text, Layer().Text);
        var moved = _asked.Count;

        Assert.Null(await Look(commit, TimeSpan.FromHours(26)));
        Assert.Equal(moved, _asked.Count);
    }

    [Fact]
    public async Task A_finish_marks_an_unknown_top_the_tree_held_as_scratch_once_and_a_later_reading_keeps_it()
    {
        var commit = await Seed();
        await Look(commit, TimeSpan.Zero);

        Assert.True(FoundReads.Finished(Home, "repo", ["stray/notes.txt"]));
        var rule = FoundFiles.Read(Home, "repo").Reading!.Rules.Single(each => each.Paths.Contains("/stray/"));
        Assert.Equal(FileKind.Scratch, rule.Kind);
        Assert.Equal([FoundReads.WrittenBySession], rule.Evidence);
        var written = Layer();

        await Task.Delay(30);
        Assert.False(FoundReads.Finished(Home, "repo", ["stray/notes.txt", "elsewhere/"]));
        Assert.Equal(written, Layer());

        Write("package-lock.json", """{ "lockfileVersion": 3, "packages": { "": {}, "node_modules/x": {} } }""");
        await Look(await Commit(), TimeSpan.FromHours(1));
        Assert.Equal(FileKind.Scratch, FoundFiles.Read(Home, "repo").Reading!.Rules.Single(each => each.Paths.Contains("/stray/")).Kind);
    }

    [Fact]
    public void A_finish_of_a_repository_never_read_writes_nothing()
    {
        Assert.False(FoundReads.Finished(Home, "repo", ["stray/"]));
        Assert.False(File.Exists(FoundFiles.PathOf(Home)));
    }
}
