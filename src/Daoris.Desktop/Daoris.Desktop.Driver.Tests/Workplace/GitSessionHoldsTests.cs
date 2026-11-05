using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A session holds nothing (D241 point 7, the git design §3.1 G5): the census of what a session's start is handed, which carries no
/// key path, token or grant, its <c>gh</c> and <c>az</c> folders pointed at empty ones of Daoris's, and the harness's rule that denies
/// reading <c>profiles/git/</c>. Runs in the agent-ports collection, since it sets the process's own variables and reads the workplace.
/// </summary>
[Collection("agent operation ports")]
public sealed class GitSessionHoldsTests : IDisposable
{
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "git-session-holds", Guid.NewGuid().ToString("N")[..8]);

    public GitSessionHoldsTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private SessionTarget Target() => new("q1", "Title", "Body", "person", "engine", _home, "http://localhost") { Home = _home, Session = "s1a2b3c4" };

    /// <summary>Every name a credential of git's or a hosted service's travels under, which no session's start may carry.</summary>
    private static readonly string[] Credentials =
        [.. GitWiring.SessionTokens, GitRouting.GrantVariable, "DAORIS_HOST_TOKEN"];

    [Fact]
    public void A_sessions_start_carries_no_key_path_token_or_grant_whatever_the_desktop_inherited()
    {
        var folders = new[] { GitSessionHolds.GhFolder, GitSessionHolds.AzFolder };
        var names = Credentials.Concat(folders).ToArray();
        var before = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var name in Credentials) Environment.SetEnvironmentVariable(name, "inherited-secret");
            foreach (var name in folders) Environment.SetEnvironmentVariable(name, Path.Combine(_home, "persons-own"));

            var info = Shell.Start(new ProcessRecipe("stand-command", []), Target());

            // No token, no grant.
            Assert.All(Credentials, name => Assert.False(info.Environment.ContainsKey(name), $"{name} reached a session's start"));
            // No path into the folders Daoris keeps its keys in, in any value.
            var keys = DaorisHome.Entry(_home, HomeEntry.GitKeys);
            var profile = Path.GetDirectoryName(keys)!;
            Assert.DoesNotContain(info.Environment, pair => pair.Value is { } value
                && (value.Contains(keys, StringComparison.OrdinalIgnoreCase) || value.Contains(keys.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)
                    || value.Contains(Path.Combine(profile, "tokens"), StringComparison.OrdinalIgnoreCase)));
            Assert.DoesNotContain(info.Environment, pair => pair.Value == "inherited-secret");
        }
        finally
        {
            foreach (var (name, value) in before) Environment.SetEnvironmentVariable(name, value);
        }
    }

    [Fact]
    public void Its_gh_and_az_configuration_folders_are_empty_ones_of_daorises_never_the_persons()
    {
        var before = Environment.GetEnvironmentVariable(GitSessionHolds.GhFolder);
        try
        {
            Environment.SetEnvironmentVariable(GitSessionHolds.GhFolder, Path.Combine(_home, "persons-own"));

            var info = Shell.Start(new ProcessRecipe("stand-command", []), Target());

            var gh = info.Environment[GitSessionHolds.GhFolder]!;
            var az = info.Environment[GitSessionHolds.AzFolder]!;
            Assert.Equal(GitSessionHolds.Empty(_home, "gh"), gh);
            Assert.Equal(GitSessionHolds.Empty(_home, "az"), az);
            Assert.NotEqual(gh, az);
            Assert.True(Directory.Exists(gh) && !Directory.EnumerateFileSystemEntries(gh).Any());
            Assert.True(Directory.Exists(az) && !Directory.EnumerateFileSystemEntries(az).Any());
            Assert.StartsWith(Path.GetFullPath(_home), Path.GetFullPath(gh), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable(GitSessionHolds.GhFolder, before);
        }
    }

    [Fact]
    public void The_harness_denies_reading_what_daoris_keeps_for_git_in_the_one_absolute_form_it_takes()
    {
        var rule = GitSessionHolds.ProfileDeny(_home);

        Assert.StartsWith("Read(//", rule);
        Assert.EndsWith("profiles/git/**)", rule.Replace('\\', '/'));
        Assert.Equal(PermissionRules.ReadRule(Path.GetDirectoryName(DaorisHome.Entry(_home, HomeEntry.GitKeys))!), rule);
    }
}
