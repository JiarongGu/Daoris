using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>When Daoris's install of an npm lockfile may leave a repository's lifecycle scripts out (the tree-pool design §35.6).</summary>
public sealed class InstallScriptsTests
{
    private const string Plain = """
        { "packages": { "": { "name": "root" }, "node_modules/left": { "resolved": "https://registry.npmjs.org/left/-/left-1.0.0.tgz" } } }
        """;

    private const string Marked = """
        { "packages": { "": {}, "node_modules/esbuild": { "hasInstallScript": true, "resolved": "https://registry.npmjs.org/esbuild/-/esbuild-1.0.0.tgz" } } }
        """;

    private const string FromGit = """
        { "packages": { "": {}, "node_modules/tool": { "resolved": "git+ssh://git@example.com/owner/tool.git#0123456789abcdef" } } }
        """;

    [Fact]
    public void A_lockfile_marking_no_install_script_and_no_git_entry_may_be_installed_without_scripts()
    {
        Assert.Equal("npm ci --ignore-scripts", InstallScripts.Rewritten("npm ci", Plain));
    }

    [Fact]
    public void A_package_marked_with_an_install_script_keeps_the_install_s_scripts()
    {
        Assert.Equal("npm ci", InstallScripts.Rewritten("npm ci", Marked));
    }

    [Fact]
    public void A_git_dependency_keeps_the_install_s_scripts_since_npm_runs_its_prepare_unmarked()
    {
        Assert.Equal("npm ci", InstallScripts.Rewritten("npm ci", FromGit));
        Assert.Equal("npm ci", InstallScripts.Rewritten("npm ci", """{ "packages": { "node_modules/x": { "resolved": "github:owner/x#abc" } } }"""));
    }

    [Fact]
    public void A_package_npm_leaves_out_on_this_machine_runs_no_script_here_so_the_install_leaves_scripts_out()
    {
        // fsevents' shape, which a test runner brings into a lockfile: optional, for one system, its script marked. Read as a script
        // this install runs, it kept the install's own scripts, whose writes beside the folder kept it from the store at every open.
        Assert.Equal("npm ci --ignore-scripts", InstallScripts.Rewritten("npm ci", OnlyOn(Elsewhere)));
        Assert.Equal("npm ci --ignore-scripts", InstallScripts.Rewritten("npm ci", OnlyOn("!" + Here)));
    }

    [Fact]
    public void A_package_marked_with_a_script_for_this_machine_keeps_the_install_s_scripts()
    {
        Assert.Equal("npm ci", InstallScripts.Rewritten("npm ci", OnlyOn(Here)));
        Assert.Equal("npm ci", InstallScripts.Rewritten("npm ci", OnlyOn("!" + Elsewhere)));
    }

    // npm's own names for the systems, as `process.platform` says them.
    private static string Here => OperatingSystem.IsWindows() ? "win32" : OperatingSystem.IsMacOS() ? "darwin" : "linux";

    private static string Elsewhere => Here == "darwin" ? "win32" : "darwin";

    private static string OnlyOn(string os) =>
        $$"""{ "packages": { "": {}, "node_modules/watcher": { "optional": true, "os": ["{{os}}"], "hasInstallScript": true, "resolved": "https://registry.npmjs.org/watcher/-/watcher-1.0.0.tgz" } } }""";

    [Fact]
    public void A_lockfile_that_is_missing_or_does_not_read_and_an_install_that_is_not_npm_ci_stay_as_written()
    {
        Assert.Equal("npm ci", InstallScripts.Rewritten("npm ci", null));
        Assert.Equal("npm ci", InstallScripts.Rewritten("npm ci", "{ not json"));
        Assert.Equal("pnpm install --frozen-lockfile", InstallScripts.Rewritten("pnpm install --frozen-lockfile", Plain));
        Assert.Equal("npm ci --ignore-scripts", InstallScripts.Rewritten("npm ci --ignore-scripts", Plain));
    }
}
