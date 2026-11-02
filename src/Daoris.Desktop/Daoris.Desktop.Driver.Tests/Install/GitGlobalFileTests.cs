using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What Daoris's git carries (TOOLS6 inside GITAUTH1c; D121 §2.5, the git-auth design §4.1): <c>profiles/git/global.gitconfig</c>, the
/// person's own global files included first by their paths and the allow-listed keys between Daoris's markers after them, handed as
/// <c>GIT_CONFIG_GLOBAL</c> to every start that takes the tools' environment where git carries a key; a child's own global writes kept;
/// a git known older than 2.32 not handed it, and a setting refused on it naming its floor.
/// </summary>
/// <remarks>Its scratch is the repository's <c>_fixtures/</c>; nothing here reads a person's own files or starts a program.</remarks>
public sealed class GitGlobalFileTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "git-global-file", Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "data");

    private string User => Path.Combine(_root, "user");

    private const string Header =
        "# Written by Daoris (D121, D198): your own global git configuration is included first, unchanged, and the lines\n"
        + "# between the two markers are what Daoris's git carries, rewritten from tools.json. What follows them is kept.\n";

    private static readonly IReadOnlyDictionary<string, string> Ssh =
        new Dictionary<string, string> { ["core.sshCommand"] = "C:/Windows/System32/OpenSSH/ssh.exe" };

    public GitGlobalFileTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(User);
    }

    public void Dispose()
    {
        GitSeed.Discard(_root);
    }

    private void ToolsFile(string json) => File.WriteAllText(DaorisHome.Entry(Home, HomeEntry.Tools), json);

    private string Touch(params string[] parts)
    {
        var file = Path.Combine([User, .. parts]);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "[user]\n\tname = Fixture\n");
        return file;
    }

    // ── The file's text ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_persons_files_come_first_by_their_paths_and_the_keys_between_the_markers_after_them()
    {
        Assert.Equal(
            Header
            + "[include]\n\tpath = \"X:/daoris/user/.config/git/config\"\n\tpath = \"X:/daoris/user/.gitconfig\"\n"
            + "# daoris: begin\n[core]\n\tsshCommand = \"C:/Windows/System32/OpenSSH/ssh.exe\"\n# daoris: end\n",
            GitGlobalFile.Text(["X:/daoris/user/.config/git/config", "X:\\daoris\\user\\.gitconfig"], Ssh, existing: null));
    }

    [Fact]
    public void With_no_file_of_the_persons_there_is_no_include_and_a_value_is_quoted_as_git_reads_it()
    {
        var quoted = new Dictionary<string, string> { ["core.sshCommand"] = "\"C:\\Program Files\\OpenSSH\\ssh.exe\" -v" };

        Assert.Equal(
            Header + "# daoris: begin\n[core]\n\tsshCommand = \"\\\"C:\\\\Program Files\\\\OpenSSH\\\\ssh.exe\\\" -v\"\n# daoris: end\n",
            GitGlobalFile.Text([], quoted, existing: null));
    }

    [Fact]
    public void A_childs_global_writes_are_kept_and_one_written_between_the_markers_moves_after_them()
    {
        var written = GitGlobalFile.Text(["X:/daoris/user/.gitconfig"], Ssh, existing: null);
        // `git config --global core.autocrlf false` adds to the [core] git finds, Daoris's; `user.name` gets a section of its own after.
        var child = written.Replace("ssh.exe\"\n# daoris: end\n", "ssh.exe\"\n\tautocrlf = false\n# daoris: end\n", StringComparison.Ordinal)
            + "[user]\n\tname = Child\n";

        Assert.Equal(
            Header + "# daoris: begin\n# daoris: end\n[core]\n\tautocrlf = false\n[user]\n\tname = Child\n",
            GitGlobalFile.Text([], new Dictionary<string, string>(), child));
    }

    // ── Which of the person's files ────────────────────────────────────────────────────────────────

    [Fact]
    public void The_persons_files_are_included_in_gits_order_and_only_where_they_exist()
    {
        var variables = new Dictionary<string, string?> { ["HOME"] = User };
        string? Variable(string name) => variables.GetValueOrDefault(name);

        Assert.Empty(GitGlobalFile.Includes(Home, Variable));

        var dot = Touch(".gitconfig");
        Assert.Equal([dot], GitGlobalFile.Includes(Home, Variable));

        var xdg = Touch(".config", "git", "config");
        Assert.Equal([xdg, dot], GitGlobalFile.Includes(Home, Variable));

        var config = Path.Combine(_root, "xdg");
        variables["XDG_CONFIG_HOME"] = config;
        Assert.Equal([dot], GitGlobalFile.Includes(Home, Variable));

        variables.Remove("HOME");
        variables["USERPROFILE"] = User;
        Assert.Equal([dot], GitGlobalFile.Includes(Home, Variable));
    }

    [Fact]
    public void A_global_file_the_person_names_is_included_instead_and_Daoriss_own_is_never_included()
    {
        var dot = Touch(".gitconfig");
        var named = Touch("work.gitconfig");
        var variables = new Dictionary<string, string?> { ["HOME"] = User, [GitGlobalFile.Variable] = named };
        string? Variable(string name) => variables.GetValueOrDefault(name);

        Assert.Equal([named], GitGlobalFile.Includes(Home, Variable));

        variables[GitGlobalFile.Variable] = GitGlobalFile.PathOf(Home);
        Assert.Equal([dot], GitGlobalFile.Includes(Home, Variable));
    }

    // ── What it carries, and to whom ───────────────────────────────────────────────────────────────

    [Fact]
    public void Where_the_ssh_door_is_laid_out_the_file_does_not_carry_the_ssh_command()
    {
        ToolsFile("""{ "git": { "core.sshCommand": "C:/Windows/System32/OpenSSH/ssh.exe" } }""");
        Assert.Equal(Ssh, GitGlobalFile.Carried(Tools.Read(Home), Home));

        File.WriteAllText(Path.Combine(_root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        var bin = Path.Combine([_root, .. Tools.InstallBinLayout]);
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, GitGlobalFile.SshDoor), "");

        Assert.Empty(GitGlobalFile.Carried(Tools.Read(Home), Home));
    }

    private ProcessStartInfo Handed()
    {
        var info = new ProcessStartInfo("x");
        info.Environment.Remove(GitGlobalFile.Variable);
        info.Environment.Remove("XDG_CONFIG_HOME");
        info.Environment["HOME"] = User;
        Tools.Hand(info, Tools.Read(Home), Home);
        return info;
    }

    [Fact]
    public void A_start_is_handed_the_file_only_where_git_carries_a_key()
    {
        Assert.False(Handed().Environment.ContainsKey(GitGlobalFile.Variable));
        Assert.False(File.Exists(GitGlobalFile.PathOf(Home)));

        var dot = Touch(".gitconfig");
        ToolsFile("""{ "git": { "core.sshCommand": "C:/Windows/System32/OpenSSH/ssh.exe" } }""");
        var info = Handed();

        Assert.Equal(GitGlobalFile.PathOf(Home), info.Environment[GitGlobalFile.Variable]);
        Assert.Equal(GitGlobalFile.Text([dot], Ssh, existing: null), File.ReadAllText(GitGlobalFile.PathOf(Home)));
    }

    [Fact]
    public async Task A_git_known_older_than_2_32_is_not_handed_the_file_and_one_at_2_32_is()
    {
        var stub = Path.Combine(_root, "old", "git.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(stub)!);
        File.WriteAllText(stub, "");
        ToolsFile($$"""{ "tools": { "git": { "use": "file", "file": {{System.Text.Json.JsonSerializer.Serialize(stub)}} } }, "git": { "core.sshCommand": "ssh" } }""");

        await GitVersions.OfAsync(stub, (_, _) => Task.FromResult<string?>("git version 2.31.8\n"), CancellationToken.None);
        Assert.False(Handed().Environment.ContainsKey(GitGlobalFile.Variable));

        File.WriteAllText(stub, "newer");
        await GitVersions.OfAsync(stub, (_, _) => Task.FromResult<string?>("git version 2.32.0\n"), CancellationToken.None);
        Assert.True(Handed().Environment.ContainsKey(GitGlobalFile.Variable));
    }

    // ── A setting written ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_setting_is_refused_on_an_older_git_naming_its_floor_and_written_on_a_newer()
    {
        var stub = Path.Combine(_root, "named", "git.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(stub)!);
        File.WriteAllText(stub, "");
        ToolsFile($$"""{ "tools": { "git": { "use": "file", "file": {{System.Text.Json.JsonSerializer.Serialize(stub)}} } } }""");
        var before = File.ReadAllText(DaorisHome.Entry(Home, HomeEntry.Tools));

        var refused = await Assert.ThrowsAsync<ToolRefusal>(() => ToolChoices.SetGitAsync(
            Home, "core.sshCommand", "ssh", (_, _) => Task.FromResult<string?>("git version 2.30.1\n"), CancellationToken.None));

        Assert.Equal(ToolRefusal.GitTooOld, refused.Check);
        Assert.Contains("2.30.1", refused.Message, StringComparison.Ordinal);
        Assert.Contains("2.32", refused.Message, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(DaorisHome.Entry(Home, HomeEntry.Tools)));

        // Clearing one is never refused: an older git loses nothing by it.
        await ToolChoices.SetGitAsync(Home, "core.sshCommand", null, (_, _) => Task.FromResult<string?>("git version 2.30.1\n"), CancellationToken.None);

        File.WriteAllText(stub, "newer");
        var version = await ToolChoices.SetGitAsync(
            Home, "core.sshCommand", "C:/Windows/System32/OpenSSH/ssh.exe", (_, _) => Task.FromResult<string?>("git version 2.53.0.windows.1\n"),
            CancellationToken.None);

        Assert.Equal(new Version(2, 53, 0), version);
        Assert.Equal(Ssh, Tools.Read(Home).Git);
        Assert.Contains("\"tools\"", File.ReadAllText(DaorisHome.Entry(Home, HomeEntry.Tools)), StringComparison.Ordinal);

        await ToolChoices.SetGitAsync(Home, "core.sshCommand", null, (_, _) => Task.FromResult<string?>(null), CancellationToken.None);
        Assert.Empty(Tools.Read(Home).Git);
    }

    [Fact]
    public async Task A_key_off_the_allow_list_is_refused_and_nothing_is_written()
    {
        var refused = await Assert.ThrowsAsync<DriverException>(() => ToolChoices.SetGitAsync(
            Home, "credential.helper", "manager", (_, _) => Task.FromResult<string?>("git version 2.53.0\n"), CancellationToken.None));

        Assert.Contains("core.sshCommand", refused.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(DaorisHome.Entry(Home, HomeEntry.Tools)));
    }
}
