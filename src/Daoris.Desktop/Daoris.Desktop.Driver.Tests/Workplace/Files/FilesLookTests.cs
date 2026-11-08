using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The page's reading of a repository's rule file (<see cref="FilesLooks"/>): each ignored top's effective rule, its layer and the
/// layers below it, the acts the rules would make with the person's answer to each, the person's own rules and permissions with who
/// gave each allow, and a file that does not read said, with nothing written over it. Composed from what the layers said, so no git
/// runs here.
/// </summary>
public sealed class FilesLookTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "files-look", Guid.NewGuid().ToString("N")[..8]);

    public FilesLookTests() => Directory.CreateDirectory(_home);

    public void Dispose() => TreeSetAside.DeleteNeverFollowing(_home);

    private static readonly FileRule Npm = new(FileKind.Dependency, ["node_modules/"]) { Install = "npm ci", Key = ["package-lock.json"], Evidence = ["beside package-lock.json"], Size = new FileSize(120, 4096, false) };

    private FilesLook Look(IReadOnlyList<FileRule> found, IReadOnlyList<FileRule> own, params string[] tops)
    {
        var reading = new FileReading(new FoundReading("abc", DateTimeOffset.UnixEpoch, found), tops, new FileFloorFacts([], []), (_, _) => null);
        var gathered = new FilesGathered(new FoundFilesRead(new FoundReading("abc1234567890", DateTimeOffset.UnixEpoch, found), []), reading, new RepositoryFilesRead(own, []), PersonFiles.Read(_home));
        return FilesLooks.Compose("web", "def1234567890", gathered);
    }

    [Fact]
    public void A_person_s_rule_wins_and_is_shown_beside_the_repository_s_and_the_found_one()
    {
        PersonFiles.Set(_home, "web", new FileRule(FileKind.Scratch, ["node_modules/"]));

        var look = Look([Npm], [new FileRule(FileKind.Output, ["node_modules/"])], "node_modules/");

        var file = Assert.Single(look.Files);
        Assert.Equal(("scratch", "person"), (file.Kind, file.Layer));
        Assert.Equal([("found", "dependency"), ("repository", "output")], file.Below.Select(each => (each.Layer, each.Kind)));
        Assert.Equal(("def1234567", "abc1234567"), (look.Commit, look.FoundCommit));
        Assert.Equal(120, file.Files);
        Assert.Equal(["beside package-lock.json"], file.Evidence);
    }

    [Fact]
    public void An_ask_holds_an_act_until_the_allow_a_press_writes_with_its_maker_and_taking_it_back_holds_it_again()
    {
        PersonPermissions.Add(_home, "web", "ask", "run(npm *)", null);
        Assert.Equal("held", Assert.Single(Look([Npm], [], "node_modules/").Acts).Verdict);

        Assert.True(PersonPermissions.Add(_home, "web", "allow", "run(npm ci)", "person"));
        var allowed = Look([Npm], [], "node_modules/");
        Assert.Equal(("run(npm ci)", "made"), (allowed.Acts[0].Act, allowed.Acts[0].Verdict));
        var scope = Assert.Single(allowed.Permissions, each => each.Scope == "repository");
        Assert.Equal([("run(npm ci)", "person")], scope.Allow.Select(each => (each.Rule, each.By)));
        Assert.Contains("\"by\": \"person\"", File.ReadAllText(PersonFiles.PathOf(_home)), StringComparison.Ordinal);

        Assert.True(PersonPermissions.Remove(_home, "web", "allow", "run(npm ci)"));
        Assert.Equal("held", Assert.Single(Look([Npm], [], "node_modules/").Acts).Verdict);
        Assert.False(PersonPermissions.Remove(_home, "web", "allow", "run(npm ci)"));
    }

    [Fact]
    public void A_copy_rule_shows_its_copy_and_the_person_s_answer_to_it()
    {
        PersonPermissions.Add(_home, "web", "deny", "copy(*)", null);
        var local = new FileRule(FileKind.Local, ["local/"]) { Reach = "copy" };

        var look = Look([local], [], "local/");

        var file = Assert.Single(look.Files);
        Assert.True(file.Copy);
        Assert.Equal(("copy(local/)", "denied"), (look.Acts[0].Act, look.Acts[0].Verdict));
    }

    [Fact]
    public void A_file_that_does_not_read_is_said_refuses_every_write_and_names_no_path()
    {
        var path = PersonFiles.PathOf(_home);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        const string stray = """{ "machine": { "permissions": { "deny": ["run(*)"] } }, }""";
        File.WriteAllText(path, stray);

        var look = Look([Npm], [], "node_modules/");

        Assert.NotNull(look.Unread);
        Assert.Equal("held", Assert.Single(look.Acts).Verdict);
        Assert.Throws<DriverException>(() => PersonPermissions.Add(_home, "web", "allow", "run(npm ci)", "person"));
        Assert.Throws<DriverException>(() => PersonFiles.Set(_home, "web", new FileRule(FileKind.Scratch, ["tmp/"])));
        Assert.Equal(stray, File.ReadAllText(path));
        Assert.DoesNotContain(_home, JsonSerializer.Serialize(look), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_permission_names_an_act_and_a_list()
    {
        Assert.Throws<DriverException>(() => PersonPermissions.Add(_home, "web", "allow", "npm ci", "person"));
        Assert.Throws<DriverException>(() => PersonPermissions.Add(_home, "web", "maybe", "run(npm ci)", null));
        Assert.False(File.Exists(PersonFiles.PathOf(_home)));
    }

    [Fact]
    public void The_machine_s_scope_is_listed_beside_the_repository_s()
    {
        PersonPermissions.Add(_home, null, "deny", "share(*)", null);

        var look = Look([], [], "x/");

        Assert.Equal(["share(*)"], Assert.Single(look.Permissions, each => each.Scope == "machine").Deny);
    }
}
