using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The analysis over fixture repositories, one per toolchain, from their signatures alone: no install, no build, nothing in the
/// tree written. Each ignored top gets its kind, a folder no source names is <c>unknown</c>, and another repository, a linked
/// worktree or a bare repository is held by the floor whatever a layer says.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class FileAnalysisTests : IDisposable
{
    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "file-analysis", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_scratch);

    private string Write(string repository, string relative, string content = "x")
    {
        var path = Path.Combine(_scratch, repository, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private async Task<string> Git(string repository, params string[] arguments)
    {
        var (code, stdout, stderr) = await WorkingTree.GitAsync(Path.Combine(_scratch, repository), arguments, CancellationToken.None);
        Assert.True(code == 0, $"git {string.Join(' ', arguments)}: {stderr}");
        return stdout.Trim();
    }

    private async Task<string> Commit(string repository)
    {
        var root = Path.Combine(_scratch, repository);
        if (!Directory.Exists(Path.Combine(root, ".git"))) await Git(repository, "init", "-q", "-b", "main");
        await Git(repository, "add", "-A");
        await Git(repository, "-c", "user.name=t", "-c", "user.email=t@example.invalid", "-c", "commit.gpgsign=false", "commit", "-q", "-m", "seed");
        return await Git(repository, "rev-parse", "HEAD");
    }

    /// <summary>The analysis read, the tree's every file and its write time the same before and after it.</summary>
    private async Task<(FileReading Reading, Dictionary<string, FileKind?> Kinds)> Read(string repository, string commit)
    {
        var root = Path.Combine(_scratch, repository);
        var before = Snapshot(root);
        var reading = await FileAnalysis.ReadAsync(root, commit, FileSignatures.Shipped, [], CancellationToken.None);
        Assert.Equal(before, Snapshot(root));

        var rules = FileRules.Of(reading.Found.Rules, [], PersonFilesRead.Empty, repository, reading.Signature);
        var kinds = reading.Tops.Concat(reading.Found.Rules.SelectMany(rule => rule.Paths.Select(GitPattern.LiteralOf)).OfType<string>()).Distinct(StringComparer.Ordinal)
            .ToDictionary(path => path, path => rules.For(path, reading.Floor).Kind, StringComparer.Ordinal);
        return (reading, kinds);
    }

    private static string Snapshot(string root) => string.Join('\n', Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .Order(StringComparer.Ordinal).Select(path => $"{Path.GetRelativePath(root, path)} {new FileInfo(path).Length} {File.GetLastWriteTimeUtc(path).Ticks}"));

    private static FileRule Found(FileReading reading, string path) => reading.Found.Rules.Single(rule => rule.Paths.Contains("/" + path));

    [Fact]
    public async Task An_npm_workspace_s_dependency_is_found_beside_its_lockfile_with_its_members_and_a_stray_folder_is_unknown()
    {
        Write("npm", ".gitignore", "node_modules/\nstray/\n");
        Write("npm", "package.json", """{ "name": "root", "workspaces": ["packages/*"], "scripts": { "prepare": "node tools/hook.mjs" } }""");
        Write("npm", "package-lock.json", """{ "lockfileVersion": 3, "packages": { "": {} } }""");
        Write("npm", "packages/a/package.json", """{ "name": "a" }""");
        var commit = await Commit("npm");
        Write("npm", "node_modules/left-pad/index.js");
        Write("npm", "stray/notes.txt");

        var (reading, kinds) = await Read("npm", commit);

        Assert.Equal(FileKind.Dependency, kinds["node_modules/"]);
        Assert.Equal(FileKind.Unknown, kinds["stray/"]);
        var dependency = Found(reading, "node_modules/");
        Assert.Equal("npm ci", dependency.Install);
        Assert.Contains("/packages/*/node_modules/", dependency.Paths);
        Assert.Empty(dependency.Key!);
        Assert.Contains(dependency.Evidence, said => said.Contains("package-lock.json", StringComparison.Ordinal));
        Assert.Contains(dependency.Evidence, said => said.Contains("prepare", StringComparison.Ordinal));
        Assert.Equal(new FileSize(1, 1, More: false), dependency.Size);
        Assert.DoesNotContain(reading.Found.Rules, rule => rule.Paths.Any(path => path.StartsWith("/stray/", StringComparison.Ordinal) && path != "/stray/"));
    }

    [Fact]
    public async Task A_pnpm_dependency_is_keyed_by_its_lockfile_workspace_and_patches_and_nothing_is_installed()
    {
        Write("pnpm", ".gitignore", "node_modules/\n");
        Write("pnpm", "package.json", """{ "name": "root" }""");
        Write("pnpm", "pnpm-lock.yaml", "lockfileVersion: '9.0'\n");
        Write("pnpm", "patches/left-pad.patch", "--- a\n+++ b\n");
        var commit = await Commit("pnpm");

        var (reading, kinds) = await Read("pnpm", commit);

        Assert.Equal(FileKind.Dependency, kinds["node_modules/"]);
        var dependency = Found(reading, "node_modules/");
        Assert.Equal("pnpm install --frozen-lockfile", dependency.Install);
        Assert.Equal(["pnpm-lock.yaml", "package.json", "pnpm-workspace.yaml", ".npmrc", "patches/**"], dependency.Key);
        Assert.Null(dependency.Size);
        Assert.False(Directory.Exists(Path.Combine(_scratch, "pnpm", "node_modules")));
    }

    [Fact]
    public async Task A_pnpm_workspace_s_members_are_named_in_the_root_dependency_from_pnpm_workspace_yaml_at_the_commit()
    {
        Write("pnpm-members", ".gitignore", "node_modules/\n");
        Write("pnpm-members", "package.json", """{ "name": "root" }""");
        Write("pnpm-members", "pnpm-lock.yaml", "lockfileVersion: '9.0'\n");
        Write("pnpm-members", "pnpm-workspace.yaml", "# the members\npackages:\n  - 'packages/*'\n  - \"apps/web\"\n  - ./tools/cli/\n  - '!packages/legacy'\ncatalog:\n  left-pad: 1.3.0\n");
        var commit = await Commit("pnpm-members");
        File.WriteAllText(Path.Combine(_scratch, "pnpm-members", "pnpm-workspace.yaml"), "packages:\n  - 'working-file-only/*'\n");

        var (reading, _) = await Read("pnpm-members", commit);

        var dependency = Found(reading, "node_modules/");
        Assert.Equal(["/node_modules/", "/packages/*/node_modules/", "/apps/web/node_modules/", "/tools/cli/node_modules/"], dependency.Paths);
        Assert.DoesNotContain(dependency.Paths, path => path.Contains("legacy", StringComparison.Ordinal) || path.Contains("working-file-only", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_pnpm_workspace_below_the_top_and_one_whose_file_names_no_packages_name_members_beside_their_own_folder_only()
    {
        Write("pnpm-nested", ".gitignore", "node_modules/\n");
        Write("pnpm-nested", "web/package.json", """{ "name": "web" }""");
        Write("pnpm-nested", "web/pnpm-lock.yaml", "lockfileVersion: '9.0'\n");
        Write("pnpm-nested", "web/pnpm-workspace.yaml", "packages: [ 'libs/*', \"ui\" ]\n");
        Write("pnpm-nested", "plain/package.json", """{ "name": "plain" }""");
        Write("pnpm-nested", "plain/pnpm-lock.yaml", "lockfileVersion: '9.0'\n");
        Write("pnpm-nested", "plain/pnpm-workspace.yaml", "onlyBuiltDependencies:\n  - esbuild\n");
        var commit = await Commit("pnpm-nested");

        var (reading, _) = await Read("pnpm-nested", commit);

        Assert.Equal(["/web/node_modules/", "/web/libs/*/node_modules/", "/web/ui/node_modules/"], Found(reading, "web/node_modules/").Paths);
        Assert.Equal(["/plain/node_modules/"], Found(reading, "plain/node_modules/").Paths);
    }

    [Fact]
    public async Task An_npm_workspace_given_as_an_object_names_its_members_without_a_leading_dot_slash_or_a_negation()
    {
        Write("npm-object", ".gitignore", "node_modules/\n");
        Write("npm-object", "package.json", """{ "name": "root", "workspaces": { "packages": ["apps/*", "./tools/cli", "!apps/legacy"] } }""");
        Write("npm-object", "package-lock.json", """{ "lockfileVersion": 3, "packages": { "": {} } }""");
        var commit = await Commit("npm-object");

        var (reading, _) = await Read("npm-object", commit);

        Assert.Equal(["/node_modules/", "/apps/*/node_modules/", "/tools/cli/node_modules/"], Found(reading, "node_modules/").Paths);
    }

    [Fact]
    public async Task A_dotnet_project_s_obj_is_an_intermediate_its_bin_an_output_and_a_tracked_entry_point_stays_git_s()
    {
        Write("dotnet", ".gitignore", "bin/\nobj/\n!src/Cli/bin/\nsrc/Cli/bin/Debug/\n");
        Write("dotnet", "src/Api/Api.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        Write("dotnet", "src/Cli/Cli.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        Write("dotnet", "src/Cli/bin/cli.mjs", "#!/usr/bin/env node\n");
        var commit = await Commit("dotnet");
        Write("dotnet", "src/Api/bin/Debug/Api.dll");
        Write("dotnet", "src/Api/obj/project.assets.json");
        Write("dotnet", "src/Cli/bin/Debug/Cli.dll");

        var (reading, kinds) = await Read("dotnet", commit);

        Assert.Equal(FileKind.Intermediate, kinds["src/Api/obj/"]);
        Assert.Equal(FileKind.Output, kinds["src/Api/bin/"]);
        Assert.Equal(FileKind.Output, kinds["src/Cli/bin/Debug/"]);
        Assert.Equal(["dotnet --version"], Found(reading, "src/Api/obj/").Toolchain);
        Assert.DoesNotContain(reading.Found.Rules, rule => rule.Paths.Contains("/src/Cli/bin/"));
        Assert.Null(FileRules.Of([new FileRule(FileKind.Output, ["src/Cli/bin/"])], [], PersonFilesRead.Empty, "dotnet").For("src/Cli/bin/", reading.Floor).Kind);
    }

    [Fact]
    public async Task A_dotnet_bin_is_found_a_base_its_signature_s_build_releases_and_set_aside_where_a_found_script_runs_it_without_building()
    {
        Write("based", ".gitignore", "bin/\nobj/\n");
        Write("based", "src/Api/Api.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        Write("based", "src/Cli/Cli.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        Write("based", "scripts/serve.sh", "#!/bin/sh\n./src/Cli/bin/Debug/net10.0/Cli.exe --serve\n");
        var commit = await Commit("based");
        Write("based", "src/Api/bin/Debug/Api.dll");
        Write("based", "src/Cli/bin/Debug/Cli.dll");

        var (reading, _) = await Read("based", commit);

        var api = Found(reading, "src/Api/bin/");
        Assert.Equal(("base", "dotnet build -c {configuration}"), (api.Take, api.Builds));
        Assert.Null(api.Refuses);
        var cli = Found(reading, "src/Cli/bin/");
        Assert.Equal("aside", cli.Take);
        Assert.Contains(cli.Evidence, said => said.Contains("scripts/serve.sh", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cargo_s_target_is_an_intermediate_until_a_script_runs_it_without_building()
    {
        Write("cargo", ".gitignore", "target/\n");
        Write("cargo", "Cargo.toml", "[package]\nname = \"app\"\n");
        var plain = await Commit("cargo");
        Write("cargo", "target/debug/app");

        Assert.Equal(FileKind.Intermediate, (await Read("cargo", plain)).Kinds["target/"]);

        Write("cargo", "scripts/serve.sh", "#!/bin/sh\n./target/release/app --serve\n");
        var running = await Commit("cargo");
        var (reading, kinds) = await Read("cargo", running);

        Assert.Equal(FileKind.Output, kinds["target/"]);
        var target = Found(reading, "target/");
        Assert.Equal(FileKind.Intermediate, target.Other);
        Assert.Contains(target.Evidence, said => said.Contains("scripts/serve.sh", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_uv_venv_is_a_bound_dependency_keyed_by_the_system_interpreter_and_its_caches_never_reach_into_it()
    {
        Write("uv", ".gitignore", ".venv/\n__pycache__/\n.pytest_cache/\n");
        Write("uv", "pyproject.toml", "[project]\nname = \"app\"\n");
        Write("uv", "uv.lock", "version = 1\n");
        Write("uv", "app/main.py", "print(1)\n");
        var commit = await Commit("uv");
        Write("uv", ".venv/lib/__pycache__/site.pyc");
        Write("uv", "app/__pycache__/main.pyc");
        Write("uv", ".pytest_cache/v/cache/nodeids");

        var (reading, kinds) = await Read("uv", commit);

        Assert.Equal(FileKind.Dependency, kinds[".venv/"]);
        Assert.Equal(FileKind.Cache, kinds["app/__pycache__/"]);
        Assert.Equal(FileKind.Cache, kinds[".pytest_cache/"]);
        var venv = Found(reading, ".venv/");
        Assert.True(venv.Bound);
        Assert.Equal("uv sync --locked", venv.Install);
        Assert.Contains("uv python find --system", venv.Toolchain!);
        Assert.DoesNotContain(kinds.Keys, path => path.StartsWith(".venv/", StringComparison.Ordinal) && path != ".venv/");
    }

    [Fact]
    public async Task A_bare_repository_a_linked_worktree_a_nested_repository_and_a_candidate_git_cannot_read_are_held()
    {
        Write("held", ".gitignore", "cache/\n.claude/worktrees/\nvendor/\nodd/\n");
        Write("held", "README.md", "held\n");
        var commit = await Commit("held");
        Directory.CreateDirectory(Path.Combine(_scratch, "held", "cache"));
        await Git("held", "init", "-q", "--bare", "cache/mirror.git");
        await Git("held", "push", "-q", "cache/mirror.git", "main:unpushed");
        await Git("held", "worktree", "add", "-q", ".claude/worktrees/w1", "-b", "w1");
        Write("held", "vendor/lib/.git", "gitdir: elsewhere\n");
        Write("held", "odd/HEAD", "not a ref\n");
        Directory.CreateDirectory(Path.Combine(_scratch, "held", "odd", "objects"));
        Directory.CreateDirectory(Path.Combine(_scratch, "held", "odd", "refs"));

        var (reading, kinds) = await Read("held", commit);

        Assert.Equal(FileKind.Repository, kinds["cache/"]);
        Assert.Equal(FileKind.Repository, kinds[".claude/worktrees/"]);
        Assert.Equal(FileKind.Repository, kinds["vendor/"]);
        Assert.Equal(FileKind.Repository, kinds["odd/"]);
        var overridden = FileRules.Of([], [new FileRule(FileKind.Scratch, ["cache/"])], PersonFiles.Parse("""{ "machine": { "rules": [ { "kind": "output", "paths": ["cache/"] } ] } }"""), "held")
            .For("cache/", reading.Floor);
        Assert.Equal((FileKind.Repository, FileLayer.Floor), (overridden.Kind, overridden.Layer));
    }

    [Fact]
    public async Task The_verb_says_each_top_s_rule_and_its_layer_and_refuses_a_write_from_a_session_s_terminal()
    {
        Write("verb", ".gitignore", "node_modules/\nout/\n");
        Write("verb", "package-lock.json", """{ "lockfileVersion": 3, "packages": { "": {} } }""");
        Write("verb", "package.json", """{ "name": "verb" }""");
        Write("verb", RepositoryFilesReader.FileName, """{ "rules": [ { "kind": "output", "paths": ["out/"] } ] }""");
        var commit = await Commit("verb");
        Write("verb", "out/app.js");
        var home = Path.Combine(_scratch, "home");
        Directory.CreateDirectory(home);

        var said = await FilesCommand.SaidAsync(home, Path.Combine(_scratch, "verb"), "verb", commit, CancellationToken.None);

        Assert.Contains(said, line => line.StartsWith("  node_modules/  dependency · by the analysis", StringComparison.Ordinal));
        Assert.Contains(said, line => line.StartsWith("  out/  output · by the repository", StringComparison.Ordinal) && line.Contains("below: the analysis unknown", StringComparison.Ordinal));
        Assert.Contains("never read", said[0], StringComparison.Ordinal);

        var error = new StringWriter();
        Assert.Equal(1, await FilesCommand.RunAsync(["verb", "set", "scratch", "tmp/"], home, Path.Combine(_scratch, "verb"), session: true, TextWriter.Null, error, CancellationToken.None));
        Assert.Contains("DAORIS_SESSION_ID", error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(PersonFiles.PathOf(home)));
    }

    [Fact]
    public async Task The_repository_s_layer_is_read_at_the_line_s_tip_and_a_working_file_s_change_acts_on_nothing()
    {
        Write("layer", ".gitignore", "out/\n");
        Write("layer", RepositoryFilesReader.FileName, """{ "rules": [ { "kind": "output", "paths": ["out/"] } ] }""");
        var commit = await Commit("layer");
        Write("layer", RepositoryFilesReader.FileName, """{ "rules": [ { "kind": "scratch", "paths": ["out/"] } ] }""");

        var read = await RepositoryFilesReader.AtAsync(Path.Combine(_scratch, "layer"), commit, CancellationToken.None);

        Assert.Empty(read.Problems);
        Assert.Equal(FileKind.Output, Assert.Single(read.Rules).Kind);
    }
}
