using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The files stage meeting a person's <c>ask</c> over a real repository: the install not run at that start and the ask kept for a
/// supervisor, run at the next start once an allow for it stands, asked again once the allow is taken back, and kept the person's where
/// the ask names them.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class FileAsksStageTests : IDisposable
{
    private const string Act = "run(node install.cjs)";

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "files-asks", Guid.NewGuid().ToString("N")[..8]);
    private string Home => Path.Combine(_scratch, "home");
    private string Root => Path.Combine(_scratch, "engine");

    public void Dispose() => GitSeed.Discard(_scratch);

    private async Task MakeAsync()
    {
        Directory.CreateDirectory(Root);
        await GitAsync(Root, "init", "-q", "-b", "main");
        await GitAsync(Root, "config", "user.email", "test@example.com");
        await GitAsync(Root, "config", "user.name", "Test");
        await File.WriteAllTextAsync(Path.Combine(Root, ".gitignore"), "deps/\n");
        await File.WriteAllTextAsync(Path.Combine(Root, "install.cjs"), "require('fs').mkdirSync('deps', { recursive: true }); require('fs').writeFileSync('deps/made.txt', 'x');\n");
        await File.WriteAllTextAsync(Path.Combine(Root, "daoris.files.json"), """{ "rules": [ { "kind": "dependency", "paths": ["deps/"], "install": "node install.cjs", "key": ["install.cjs"] } ] }""");
        await GitAsync(Root, "add", ".");
        await GitAsync(Root, "commit", "-q", "-m", "first");
    }

    private async Task<TreeFilesOutcome> StageAsync(string name)
    {
        var tree = Path.Combine(_scratch, name);
        await GitAsync(Root, "worktree", "add", "-q", "-b", $"daoris/{name}", tree, "main");
        TreeFilesRecord.Mark(Home, tree);
        Trees[name] = tree;
        return await new TreeFilesStage(Home, Root, tree, "engine").RunAsync(CancellationToken.None);
    }

    private Dictionary<string, string> Trees { get; } = [];

    private bool Installed(string name) => File.Exists(Path.Combine(Trees[name], "deps", "made.txt"));

    [Fact]
    public async Task A_held_install_is_not_run_and_is_kept_as_an_ask_run_at_the_next_start_once_allowed_and_asked_again_when_the_allow_is_taken_back()
    {
        await MakeAsync();
        PersonPermissions.Add(Home, null, "ask", "run(node *)", null);

        var first = await StageAsync("t1");

        Assert.Contains(StartNoteCodes.StartedFilesWaiting.Code, first.Said.SelectMany(each => each.Parts).Select(part => part.Code));
        Assert.False(Installed("t1"));
        var ask = Assert.Single(FileAsks.Read(Home));
        Assert.Equal(("engine", FileAsk.Run, Act, false), (ask.Repository, ask.What, ask.Text, ask.Person));

        await StageAsync("t2");
        Assert.Equal(ask.Start, Assert.Single(FileAsks.Read(Home)).Start);

        FileAllows.Give(Home, ask, "supervisor", "d1");
        FileAsks.Settle(Home, "d1", made: true);
        await StageAsync("t3");

        Assert.True(Installed("t3"));
        Assert.Empty(FileAsks.Read(Home));

        // A new key, so the store has no hit to place and only the command could make the folder.
        await File.AppendAllTextAsync(Path.Combine(Root, "install.cjs"), "// again\n");
        await GitAsync(Root, "commit", "-qam", "again");
        PersonPermissions.Remove(Home, "engine", "allow", Act);
        await StageAsync("t4");

        Assert.False(Installed("t4"));
        Assert.NotEqual(ask.Start, Assert.Single(FileAsks.Read(Home)).Start);
    }

    private IEnumerable<string> Codes(TreeFilesOutcome outcome) => outcome.Said.SelectMany(each => each.Parts).Select(part => part.Code);

    private async Task DeclareInputAsync()
    {
        await File.WriteAllTextAsync(Path.Combine(Root, ".gitignore"), "deps/\nlocal/\n");
        await GitAsync(Root, "commit", "-qam", "ignore local");
        await File.WriteAllTextAsync(Path.Combine(Root, "daoris.gates.json"), """{ "localInputs": ["local/notes.txt"] }""");
        Directory.CreateDirectory(Path.Combine(Root, "local"));
        await File.WriteAllTextAsync(Path.Combine(Root, "local", "notes.txt"), "private");
    }

    [Fact]
    public async Task A_held_copy_is_not_made_and_is_kept_as_an_ask_made_at_the_next_start_once_allowed_and_asked_again_when_the_allow_is_taken_back()
    {
        await MakeAsync();
        await DeclareInputAsync();
        PersonPermissions.Add(Home, null, "ask", "copy(*)", null);

        var first = await StageAsync("t1");

        Assert.Contains(StartNoteCodes.StartedLocalHeld.Code, Codes(first));
        Assert.False(File.Exists(Path.Combine(Trees["t1"], "local", "notes.txt")));
        var ask = Assert.Single(FileAsks.Read(Home), each => each.What == FileAsk.Copy);
        Assert.Equal(("engine", "copy(local/notes.txt)", false), (ask.Repository, ask.Text, ask.Person));

        FileAllows.Give(Home, ask, "supervisor", "d1");
        FileAsks.Settle(Home, "d1", made: true);
        var second = await StageAsync("t2");

        Assert.Contains(StartNoteCodes.StartedLocalCopied.Code, Codes(second));
        Assert.True(File.Exists(Path.Combine(Trees["t2"], "local", "notes.txt")));
        Assert.DoesNotContain(FileAsks.Read(Home), each => each.What == FileAsk.Copy);

        PersonPermissions.Remove(Home, "engine", "allow", "copy(local/notes.txt)");
        await StageAsync("t3");

        Assert.False(File.Exists(Path.Combine(Trees["t3"], "local", "notes.txt")));
        Assert.Single(FileAsks.Read(Home), each => each.What == FileAsk.Copy);
    }

    [Fact]
    public async Task A_denied_copy_is_not_made_and_leaves_no_ask()
    {
        await MakeAsync();
        await DeclareInputAsync();
        PersonPermissions.Add(Home, null, "deny", "copy(local/*)", null);

        var first = await StageAsync("t1");

        Assert.Contains(StartNoteCodes.StartedLocalDenied.Code, Codes(first));
        Assert.False(File.Exists(Path.Combine(Trees["t1"], "local", "notes.txt")));
        Assert.Empty(FileAsks.Read(Home));
    }

    [Fact]
    public async Task An_ask_naming_the_person_is_kept_so_and_is_not_run()
    {
        await MakeAsync();
        PersonPermissions.Add(Home, null, "ask", "run(node *)@person", null);

        await StageAsync("t1");

        Assert.False(Installed("t1"));
        Assert.True(Assert.Single(FileAsks.Read(Home)).Person);
    }
}
