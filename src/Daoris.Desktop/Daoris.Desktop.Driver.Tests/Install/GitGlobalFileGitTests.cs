using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TOOLS6's file read by this machine's real git (D121 §2.5, the git-auth design §4.1): asked <c>config --show-origin core.sshCommand</c>
/// in a scratch repository, the key is Daoris's file's; the person's own global file is read through its include, and one gone since
/// is read as nothing; a repository's own key still wins; and a child's <c>git config --global</c> writes Daoris's file, never the
/// person's, and is kept by the next rewrite.
/// </summary>
/// <remarks>Each git is handed the tools' environment as every child is, with a scratch home of the test's own and no system file; its
/// scratch is the repository's <c>_fixtures/</c>.</remarks>
public sealed class GitGlobalFileGitTests : IDisposable
{
    private const string Ssh = "C:/Windows/System32/OpenSSH/ssh.exe";

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "git-global-file-git", Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "data");

    private string User => Path.Combine(_root, "user");

    private string Repo => Path.Combine(_root, "engine");

    private string Persons => Path.Combine(User, ".gitconfig");

    public GitGlobalFileGitTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(User);
        Directory.CreateDirectory(Repo);
        File.WriteAllText(Persons, "[user]\n\tname = Fixture\n");
        File.WriteAllText(DaorisHome.Entry(Home, HomeEntry.Tools), $$"""{ "git": { "core.sshCommand": "{{Ssh}}" } }""");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private async Task<(int Exit, string Out)> GitAsync(params string[] arguments)
    {
        var info = new ProcessStartInfo("git")
        {
            UseShellExecute = false, WorkingDirectory = Repo, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        foreach (var name in new[] { GitGlobalFile.Variable, "XDG_CONFIG_HOME", "HOMEDRIVE", "HOMEPATH" }) info.Environment.Remove(name);
        info.Environment["HOME"] = User;
        info.Environment["USERPROFILE"] = User;
        info.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        Tools.Hand(info, Tools.Read(Home), Home);
        Assert.Equal(GitGlobalFile.PathOf(Home), info.Environment[GitGlobalFile.Variable]);
        using var process = Process.Start(TestGit.Past(info))!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, ((await output) + (await error)).Trim());
    }

    [Fact]
    public async Task Git_reads_the_ssh_command_from_Daoriss_file_and_the_persons_own_through_its_include()
    {
        Assert.Equal(0, (await GitAsync("init", "-q", "-b", "main")).Exit);

        var origin = await GitAsync("config", "--show-origin", "--get", "core.sshCommand");
        Assert.Equal(0, origin.Exit);
        // git quotes a path holding a backslash: `file:"X:\\…\\global.gitconfig"`.
        var (from, value) = (origin.Out.Split('\t')[0].Replace("\\\\", "/").Replace('\\', '/'), origin.Out.Split('\t')[^1]);
        Assert.EndsWith("profiles/git/global.gitconfig", from.TrimEnd('"'), StringComparison.Ordinal);
        Assert.Equal(Ssh, value);
        Assert.Equal((0, "Fixture"), await GitAsync("config", "--get", "user.name"));
    }

    [Fact]
    public async Task An_include_whose_file_has_gone_is_read_as_nothing()
    {
        Assert.Equal(0, (await GitAsync("init", "-q", "-b", "main")).Exit);
        File.Delete(Persons);
        // Daoris's file still names it until the next start rewrites it, and git reads it as nothing.
        Assert.Contains(Persons.Replace('\\', '/'), File.ReadAllText(GitGlobalFile.PathOf(Home)), StringComparison.Ordinal);

        var info = new ProcessStartInfo("git") { UseShellExecute = false, WorkingDirectory = Repo, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "config", "--get", "core.sshCommand" }) info.ArgumentList.Add(argument);
        info.Environment[GitGlobalFile.Variable] = GitGlobalFile.PathOf(Home);
        info.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        using var process = Process.Start(TestGit.Past(info))!;
        var said = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(process.ExitCode == 0, error);
        Assert.Equal(Ssh, said.Trim());

        Assert.Equal(1, (await GitAsync("config", "--get", "user.name")).Exit);
        Assert.DoesNotContain(Persons.Replace('\\', '/'), File.ReadAllText(GitGlobalFile.PathOf(Home)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_repositorys_own_ssh_command_still_wins()
    {
        Assert.Equal(0, (await GitAsync("init", "-q", "-b", "main")).Exit);
        Assert.Equal(0, (await GitAsync("config", "core.sshCommand", "ssh -i deploy")).Exit);

        var origin = await GitAsync("config", "--show-origin", "--get", "core.sshCommand");
        Assert.EndsWith(".git/config\tssh -i deploy", origin.Out.Replace('\\', '/'), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_childs_global_write_lands_in_Daoriss_file_never_the_persons_and_is_kept()
    {
        Assert.Equal(0, (await GitAsync("init", "-q", "-b", "main")).Exit);
        var before = File.ReadAllText(Persons);

        Assert.Equal(0, (await GitAsync("config", "--global", "user.email", "child@example.invalid")).Exit);
        Assert.Equal(0, (await GitAsync("config", "--global", "core.autocrlf", "false")).Exit);

        Assert.Equal(before, File.ReadAllText(Persons));
        Assert.Equal((0, "child@example.invalid"), await GitAsync("config", "--get", "user.email"));
        Assert.Equal((0, "false"), await GitAsync("config", "--get", "core.autocrlf"));
        var written = File.ReadAllText(GitGlobalFile.PathOf(Home));
        var end = written.IndexOf(GitGlobalFile.End, StringComparison.Ordinal);
        Assert.Contains("child@example.invalid", written[end..], StringComparison.Ordinal);
        Assert.Contains("autocrlf = false", written[end..], StringComparison.Ordinal);
    }
}
