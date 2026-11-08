using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The home's two files of rules: the person's <c>data/files.json</c>, one reader and writer with a hand edit read, and the
/// analysis's <c>data/files-found.json</c>, each reading replacing its repository's entry whole and never touching the person's.
/// </summary>
public sealed class PersonFilesTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "file-rules", Guid.NewGuid().ToString("N")[..8]);

    public PersonFilesTests() => Directory.CreateDirectory(_home);

    public void Dispose() => TreeSetAside.DeleteNeverFollowing(_home);

    private void Hand(string text)
    {
        var path = PersonFiles.PathOf(_home);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    [Fact]
    public void An_absent_file_is_empty_and_restricts_nothing()
    {
        var read = PersonFiles.Read(_home);

        Assert.Null(read.Unread);
        Assert.Empty(read.Machine.Rules);
        Assert.Equal(FileVerdict.Made, FileRules.Of([], [], read, "web").Judge(FileActs.Run("npm ci")));
        Assert.False(File.Exists(PersonFiles.PathOf(_home)));
    }

    [Fact]
    public async Task A_write_waits_for_another_edit_of_the_file_so_neither_is_lost()
    {
        var lockPath = RecordLock.PathOf(_home, PersonFiles.LockName);
        var waiting = new TaskCompletionSource();
        void Waits(string path)
        {
            if (path == lockPath) waiting.TrySetResult();
        }

        RecordLock.Waiting += Waits;
        try
        {
            Task write;
            using (RecordLock.Take(lockPath))
            {
                write = Task.Run(() => PersonFiles.Set(_home, "web", new FileRule(FileKind.Local, ["local/a.txt"])));
                await Task.WhenAny(waiting.Task, Task.Delay(TimeSpan.FromSeconds(5)));
                Assert.True(waiting.Task.IsCompleted, "the write did not wait for the lock another edit holds");
            }

            await write;
        }
        finally
        {
            RecordLock.Waiting -= Waits;
        }

        Assert.Single(PersonFiles.Read(_home).For("web").Rules);
    }

    [Fact]
    public void A_set_and_a_remove_write_the_person_s_layer_and_keep_what_a_hand_wrote()
    {
        Hand("""{ "_why": "mine", "machine": { "permissions": { "deny": ["share(*)"] } } }""");

        PersonFiles.Set(_home, "web", new FileRule(FileKind.Local, ["local/a.txt"]) { Reach = "copy" });
        PersonFiles.Set(_home, null, new FileRule(FileKind.Scratch, ["tmp/"]));
        var read = PersonFiles.Read(_home);

        Assert.Equal(["local/a.txt"], Assert.Single(read.For("web").Rules).Paths);
        Assert.Equal("copy", read.For("WEB").Rules[0].Reach);
        Assert.Equal(["tmp/"], Assert.Single(read.Machine.Rules).Paths);
        Assert.Equal(["share(*)"], read.Machine.Permissions.Deny);
        Assert.Contains("\"_why\": \"mine\"", File.ReadAllText(PersonFiles.PathOf(_home)), StringComparison.Ordinal);

        Assert.Equal(1, PersonFiles.Remove(_home, "web", "local/a.txt"));
        Assert.Equal(0, PersonFiles.Remove(_home, "web", "local/a.txt"));
        Assert.Empty(PersonFiles.Read(_home).For("web").Rules);
    }

    [Fact]
    public void A_file_that_does_not_read_is_said_holds_every_act_and_is_never_written_over()
    {
        const string stray = """{ "machine": { "permissions": { "deny": ["run(*)"] } }, }""";
        Hand(stray);

        var read = PersonFiles.Read(_home);
        Assert.NotNull(read.Unread);
        Assert.Equal(FileVerdict.Held, FileRules.Of([], [], read, "web").Judge(FileActs.Run("npm ci")));

        var refused = Assert.Throws<DriverException>(() => PersonFiles.Set(_home, "web", new FileRule(FileKind.Scratch, ["tmp/"])));
        Assert.Contains("nothing was written", refused.Message, StringComparison.Ordinal);
        Assert.Throws<DriverException>(() => PersonFiles.Remove(_home, "web", "tmp/"));
        Assert.Equal(stray, File.ReadAllText(PersonFiles.PathOf(_home)));

        FoundFiles.Write(_home, "web", new FoundReading("abc123", DateTimeOffset.UnixEpoch, [new FileRule(FileKind.Output, ["out/"])]));
        Assert.Equal(stray, File.ReadAllText(PersonFiles.PathOf(_home)));
    }

    [Fact]
    public void A_reading_replaces_its_repository_s_found_entry_whole_and_a_found_file_that_does_not_read_is_written_again()
    {
        var path = FoundFiles.PathOf(_home);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ not json");
        Assert.NotEmpty(FoundFiles.Read(_home, "web").Problems);

        FoundFiles.Write(_home, "web", new FoundReading("one", DateTimeOffset.UnixEpoch, [new FileRule(FileKind.Output, ["out/"]), new FileRule(FileKind.Scratch, ["tmp/"])]));
        FoundFiles.Write(_home, "api", new FoundReading("two", DateTimeOffset.UnixEpoch, [new FileRule(FileKind.Intermediate, ["obj/"]) { Toolchain = ["dotnet --version"] }]));
        FoundFiles.Write(_home, "web", new FoundReading("three", DateTimeOffset.UnixEpoch,
            [new FileRule(FileKind.Dependency, ["node_modules/"]) { Install = "npm ci", Key = [], Evidence = ["beside package-lock.json"], Size = new FileSize(3, 12, More: false) }]));

        var web = FoundFiles.Read(_home, "web");
        Assert.Empty(web.Problems);
        Assert.Equal("three", web.Reading!.Commit);
        var rule = Assert.Single(web.Reading.Rules);
        Assert.Equal((FileKind.Dependency, "npm ci"), (rule.Kind, rule.Install));
        Assert.Empty(rule.Key!);
        Assert.Equal(["beside package-lock.json"], rule.Evidence);
        Assert.Equal(new FileSize(3, 12, More: false), rule.Size);
        Assert.Equal("two", FoundFiles.Read(_home, "api").Reading!.Commit);
        Assert.False(File.Exists(PersonFiles.PathOf(_home)));
    }
}
