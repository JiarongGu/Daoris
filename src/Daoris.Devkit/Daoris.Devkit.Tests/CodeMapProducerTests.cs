using Daoris.Devkit;

namespace Daoris.Devkit.Tests;

/// <summary>
/// The tool producer of a repository's code map (MAP3c, <c>docs/2026-09-23-map-design.md</c> §3): the
/// modules are the repository's projects and packages, and the dependencies it draws are the ones its
/// project files declare — exact, with no compiler. What the files do not say is a person's, and kept.
/// </summary>
public sealed class CodeMapProducerTests : IDisposable
{
    private readonly Fixture _fx = new("codemap");

    public void Dispose() => _fx.Dispose();

    private sealed class FakeGit(params string[] files) : IGit
    {
        public IReadOnlyList<string> StagedFiles() => files;

        public IReadOnlyList<string> TrackedFiles() => files;
    }

    private ProducedMap Produce(params string[] tracked) => CodeMapProducer.Produce(_fx.Path, new FakeGit(tracked));

    private void Project(string relative, string references = "", string? description = null) => _fx.Write(relative, $"""
        <Project Sdk="Microsoft.NET.Sdk">
          {(description is null ? "" : $"<PropertyGroup><Description>{description}</Description></PropertyGroup>")}
          <ItemGroup>{references}</ItemGroup>
        </Project>
        """);

    [Fact]
    public void A_csharp_project_is_a_module_and_its_project_references_are_its_dependencies()
    {
        Project("src/Core/Core.csproj");
        Project("src/Http/Http.csproj", """<ProjectReference Include="..\Core\Core.csproj" />""");
        Project("src/Tests/Tests.csproj", """<ProjectReference Include="../Core/Core.csproj;../Http/Http.csproj" />""");

        var map = Produce("src/Core/Core.csproj", "src/Http/Http.csproj", "src/Tests/Tests.csproj");

        Assert.Null(map.Problem);
        Assert.Equal(
            [("Core", "src/Core"), ("Http", "src/Http"), ("Tests", "src/Tests")],
            map.Modules.Select(m => (m.Id, m.Path)));
        Assert.Equal(
            [("Http", "Core", "project"), ("Tests", "Core", "project"), ("Tests", "Http", "project")],
            map.Dependencies.Select(d => (d.From, d.To, d.Kind)));
    }

    /// <summary>
    /// A package is named by its package name, and what it depends on is drawn only where that is one
    /// of the repository's own packages — an npm registry dependency is not a module of this repository.
    /// The root package is the repository itself, not a module of it.
    /// </summary>
    [Fact]
    public void An_npm_package_is_a_module_named_by_its_package_and_depends_on_the_repositorys_own()
    {
        _fx.Write("package.json", """{ "name": "the-workspace", "workspaces": ["packages/a"] }""");
        _fx.Write("packages/a/package.json",
            """{ "name": "@scope/a", "description": "the first", "dependencies": { "@scope/b": "1.0.0", "left-pad": "^1" }, "devDependencies": { "@scope/b": "1.0.0" } }""");
        _fx.Write("packages/b/package.json", """{ "name": "@scope/b" }""");

        var map = Produce("package.json", "packages/a/package.json", "packages/b/package.json");

        Assert.Null(map.Problem);
        Assert.Equal([("@scope/a", "packages/a", "the first"), ("@scope/b", "packages/b", "")],
            map.Modules.Select(m => (m.Id, m.Path, m.Summary)));
        var dependency = Assert.Single(map.Dependencies);
        Assert.Equal(("@scope/a", "@scope/b", "package"), (dependency.From, dependency.To, dependency.Kind));
    }

    /// <summary>
    /// The project's own description is the summary, one line. Where the project declares none, the
    /// line a person wrote in the map is kept — the tool never erases what only a person could say.
    /// </summary>
    [Fact]
    public void A_description_is_the_summary_and_where_there_is_none_the_maps_own_line_is_kept()
    {
        Project("src/Core/Core.csproj", description: "the heart,\n  in two lines");
        Project("src/Http/Http.csproj");
        _fx.Write("docs/code-map.json", """
            { "version": 1,
              "modules": [{ "id": "Http", "path": "src/Http", "summary": "the door, said by a person" },
                          { "id": "Core", "path": "src/Core", "summary": "overridden by the project" }],
              "dependencies": [] }
            """);

        var map = Produce("src/Core/Core.csproj", "src/Http/Http.csproj");

        Assert.Equal(["the heart, in two lines", "the door, said by a person"], map.Modules.Select(m => m.Summary));
    }

    /// <summary>
    /// The tool owns the kinds it derives; any other kind is a person's (a service called over HTTP,
    /// say) and is kept while both its ends are still modules. A derived kind is never kept from the
    /// file: a reference that was removed from the project is gone from the map.
    /// </summary>
    [Fact]
    public void A_dependency_of_a_kind_the_tool_does_not_derive_is_kept_while_both_ends_remain()
    {
        Project("src/Core/Core.csproj");
        Project("src/Http/Http.csproj");
        _fx.Write("src/Web/package.json", """{ "name": "web" }""");
        _fx.Write("docs/code-map.json", """
            { "version": 1, "modules": [],
              "dependencies": [
                { "from": "web", "to": "Http", "kind": "http" },
                { "from": "web", "to": "Gone", "kind": "http" },
                { "from": "Http", "to": "Core", "kind": "project" } ] }
            """);

        var map = Produce("src/Core/Core.csproj", "src/Http/Http.csproj", "src/Web/package.json");

        var kept = Assert.Single(map.Dependencies);
        Assert.Equal(("web", "Http", "http"), (kept.From, kept.To, kept.Kind));
    }

    /// <summary>A reference to a project this repository does not track is said, never drawn — the reader would refuse a dependency naming no module.</summary>
    [Fact]
    public void A_reference_outside_the_tracked_projects_is_reported_and_not_drawn()
    {
        Project("src/Http/Http.csproj", """<ProjectReference Include="..\..\..\elsewhere\Lib\Lib.csproj" /><ProjectReference Include="$(Shared)\X.csproj" />""");

        var map = Produce("src/Http/Http.csproj");

        Assert.Null(map.Problem);
        Assert.Empty(map.Dependencies);
        Assert.Equal(2, map.Notes.Count);
        Assert.All(map.Notes, note => Assert.Contains("src/Http/Http.csproj", note));
    }

    [Fact]
    public void Two_modules_that_would_share_an_id_are_refused_naming_both()
    {
        Project("src/a/Core.csproj");
        Project("src/b/Core.csproj");

        var map = Produce("src/a/Core.csproj", "src/b/Core.csproj");

        Assert.NotNull(map.Problem);
        Assert.Contains("src/a/Core.csproj", map.Problem);
        Assert.Contains("src/b/Core.csproj", map.Problem);
        Assert.Null(map.Text);
    }

    /// <summary>Past the reader's bound it is no longer a picture, and the tool never writes a file its judge would refuse.</summary>
    [Fact]
    public void More_modules_than_the_reader_takes_is_refused_not_written()
    {
        var tracked = new List<string>();
        for (var i = 0; i <= CodeMapProducer.MaxModules; i++)
        {
            _fx.Write($"p/{i}/package.json", $$"""{ "name": "p{{i}}" }""");
            tracked.Add($"p/{i}/package.json");
        }

        var map = Produce([.. tracked]);

        Assert.Contains($"{CodeMapProducer.MaxModules}", map.Problem);
        Assert.Null(map.Text);
    }

    /// <summary>
    /// A file whose summaries and hand-drawn dependencies the tool cannot read would lose them if it
    /// were rewritten, so it is refused instead — fixed or deleted by a person, then run again.
    /// </summary>
    [Fact]
    public void A_map_the_devkit_cannot_read_is_refused_rather_than_overwritten()
    {
        Project("src/Core/Core.csproj");
        _fx.Write("docs/code-map.json", "{ half a map");

        var map = Produce("src/Core/Core.csproj");

        Assert.Contains("docs/code-map.json", map.Problem);
        Assert.Contains("delete", map.Problem);
    }

    /// <summary>
    /// Written the same way every time: modules by path, dependencies by their ends, two-space indent,
    /// LF, a final newline. So a second run over an unchanged repository writes an identical file, and
    /// a change to a project file is a small diff a reviewer can read.
    /// </summary>
    [Fact]
    public void The_file_is_written_the_same_way_every_time()
    {
        Project("src/Z/Z.csproj", """<ProjectReference Include="..\A\A.csproj" />""");
        Project("src/A/A.csproj");

        var first = Produce("src/Z/Z.csproj", "src/A/A.csproj");
        CodeMapProducer.Write(_fx.Path, first);
        var second = Produce("src/A/A.csproj", "src/Z/Z.csproj");

        Assert.Equal(first.Text, second.Text);
        Assert.Equal(first.Text, File.ReadAllText(_fx.Absolute("docs/code-map.json")));
        Assert.DoesNotContain("\r", first.Text);
        Assert.EndsWith("}\n", first.Text);
        Assert.Contains("\n  \"modules\": [\n", first.Text);
        Assert.True(first.Text!.IndexOf("\"A\"", StringComparison.Ordinal) < first.Text.IndexOf("\"Z\"", StringComparison.Ordinal));
    }

    /// <summary>
    /// A summary is written as it reads — an apostrophe, a 中文 line — not escaped for an HTML page this
    /// file is never embedded in. The file is reviewed as a diff, and `host's` is not a line a
    /// reviewer reads.
    /// </summary>
    [Fact]
    public void A_summary_is_written_as_it_reads()
    {
        _fx.Write("web/package.json", """{ "name": "web", "description": "the host's page — 平台" }""");

        var text = Produce("web/package.json").Text!;

        Assert.Contains("\"the host's page — 平台\"", text);
    }

    /// <summary>It writes where the reader reads: an existing root map stays at the root; otherwise `docs/code-map.json`.</summary>
    [Fact]
    public void It_writes_where_the_reader_would_read()
    {
        Project("src/A/A.csproj");
        Assert.Equal("docs/code-map.json", Produce("src/A/A.csproj").File);

        _fx.Write("code-map.json", """{ "version": 1, "modules": [], "dependencies": [] }""");
        Assert.Equal("code-map.json", Produce("src/A/A.csproj").File);
    }

    /// <summary>
    /// `--check` is a FACT (D54): the committed map either is what the project files say or it is not.
    /// Stale, it names what differs and the command that fixes it, and it writes nothing.
    /// </summary>
    [Fact]
    public void Check_names_what_differs_and_writes_nothing()
    {
        Project("src/A/A.csproj");
        CodeMapProducer.Write(_fx.Path, Produce("src/A/A.csproj"));
        Assert.True(CodeMapProducer.Check(_fx.Path, Produce("src/A/A.csproj")).Fresh);

        Project("src/B/B.csproj", """<ProjectReference Include="..\A\A.csproj" />""");
        var before = File.ReadAllText(_fx.Absolute("docs/code-map.json"));
        var check = CodeMapProducer.Check(_fx.Path, Produce("src/A/A.csproj", "src/B/B.csproj"));

        Assert.False(check.Fresh);
        Assert.Contains("module `B`", check.Detail);
        Assert.Contains("B → A (project)", check.Detail);
        Assert.Contains("daoris-devkit map", check.Detail);
        Assert.Equal(before, File.ReadAllText(_fx.Absolute("docs/code-map.json")));
    }

    [Fact]
    public void Check_says_so_when_there_is_no_map_yet()
    {
        Project("src/A/A.csproj");

        var check = CodeMapProducer.Check(_fx.Path, Produce("src/A/A.csproj"));

        Assert.False(check.Fresh);
        Assert.Contains("no code map yet", check.Detail);
    }

    /// <summary>
    /// 🔴 The twin. The service's <c>CodeMapReader</c> judges what this writes, and the devkit is a
    /// separate artefact that shares no code with it (a tool a .NET repository runs must not carry the
    /// service). So the rules the producer restates — where the file lives, the bounds — are held to the
    /// reader's by reading the reader's own source: change one side and this fails.
    /// </summary>
    [Fact]
    public void Its_rules_are_the_readers()
    {
        var reader = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Service", "Daoris.Service.Core", "CodeMap.cs"));

        Assert.Contains($"Candidates = [{string.Join(", ", CodeMapProducer.Candidates.Select(c => $"\"{c}\""))}]", reader);
        Assert.Contains($"MaxModules = {CodeMapProducer.MaxModules};", reader);
        Assert.Contains($"MaxDependencies = {CodeMapProducer.MaxDependencies};", reader);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("could not find the repository root");
    }
}
