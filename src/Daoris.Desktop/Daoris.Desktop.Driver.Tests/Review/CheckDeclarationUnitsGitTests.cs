using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a check runs under, held by units against a real checkout: under restricted access a work whose own change to
/// <c>daoris.gates.json</c> changes one gate's command holds only the runs that read that gate; the others run under the accepted
/// declaration as before, and a path, a time or words held nothing at all.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class CheckDeclarationUnitsGitTests : IAsyncLifetime
{
    private const string Line = """{ "gates": [ { "name": "build", "run": "dotnet build", "paths": ["src/"] }, { "name": "tools", "run": "npm test" } ], "reached": { "run": "node reached.mjs" } }""";

    private readonly string _folder = Path.Combine(TestHomes.Root, "daoris-check-units-" + Guid.NewGuid().ToString("N")[..8]);

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_folder);
        Assert.Equal(0, (await GitFixture.RunAsync(_folder, "init", "-q", "-b", "main")).ExitCode);
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        return Task.CompletedTask;
    }

    private async Task<(string Commit, string Blob)> CommitAsync(string declaration)
    {
        await File.WriteAllTextAsync(Path.Combine(_folder, "daoris.gates.json"), declaration);
        await File.WriteAllTextAsync(Path.Combine(_folder, "moved.txt"), Guid.NewGuid().ToString("N"));
        await GitFixture.RunAsync(_folder, "add", "-A");
        Assert.Equal(0, (await GitFixture.RunAsync(_folder, "-c", "user.name=t", "-c", "user.email=t@example.invalid", "commit", "-q", "-m", "next")).ExitCode);
        return ((await GitFixture.GitAsync(_folder, "rev-parse", "HEAD")).Trim(), (await GitFixture.GitAsync(_folder, "rev-parse", "HEAD:daoris.gates.json")).Trim());
    }

    private static Task<string?> Main(CancellationToken ct) => Task.FromResult<string?>("main");

    private async Task<CheckDeclaration> RestrictedAsync(string commit, string accepted, IReadOnlyCollection<string>? gates, bool reached = false) =>
        await CheckDeclaration.OfAsync(_folder, commit, accepted, DeclarationMode.Restricted, Main, DeclarationUnits.Run(gates, reached), CancellationToken.None);

    /// <summary>
    /// The work, cut from the line, changed the command of <c>build</c>: the run of <c>tools</c> alone is checked as accepted, the
    /// run of <c>build</c>, and a run of every gate, hold as changed naming it, and the accepted text is what a run is given.
    /// </summary>
    [Fact]
    public async Task A_changed_gate_holds_only_the_runs_that_read_it()
    {
        var (_, accepted) = await CommitAsync(Line);
        await GitFixture.RunAsync(_folder, "switch", "-q", "-c", "work");
        var (work, _) = await CommitAsync(Line.Replace("dotnet build", "dotnet build -c Release", StringComparison.Ordinal));

        var tools = await RestrictedAsync(work, accepted, ["tools"]);
        Assert.Null(tools.Problem);
        Assert.Null(tools.Held);
        Assert.Equal(Line, tools.Text);

        var build = await RestrictedAsync(work, accepted, ["build"]);
        Assert.NotNull(build.Held);
        Assert.Contains("build", build.Held);

        Assert.NotNull((await RestrictedAsync(work, accepted, null)).Held);
        Assert.NotNull((await RestrictedAsync(work, accepted, ["tools"], reached: false)).Text);
    }

    /// <summary>A path, a timeout or words changed in the work holds no run of any gate: the same acceptance as before, unit by unit.</summary>
    [Fact]
    public async Task A_changed_path_or_words_holds_no_run()
    {
        var (_, accepted) = await CommitAsync(Line);
        await GitFixture.RunAsync(_folder, "switch", "-q", "-c", "work");
        var (work, _) = await CommitAsync(Line.Replace("\"src/\"", "\"src/\", \"lib/\"", StringComparison.Ordinal).Replace("{ \"name\": \"tools\"", "{ \"_why\": \"words\", \"name\": \"tools\"", StringComparison.Ordinal));

        Assert.Null((await RestrictedAsync(work, accepted, null)).Held);
        Assert.Null((await RestrictedAsync(work, accepted, ["build"])).Held);
    }

    /// <summary>The hand-back run reads <c>reached</c>, and a gate's run does not.</summary>
    [Fact]
    public async Task A_changed_reached_command_holds_the_hand_back_run_and_no_gates_run()
    {
        var (_, accepted) = await CommitAsync(Line);
        await GitFixture.RunAsync(_folder, "switch", "-q", "-c", "work");
        var (work, _) = await CommitAsync(Line.Replace("node reached.mjs", "node reached.mjs --all", StringComparison.Ordinal));

        Assert.Null((await RestrictedAsync(work, accepted, ["build"])).Held);
        Assert.NotNull((await RestrictedAsync(work, accepted, ["build"], reached: true)).Held);
    }

    /// <summary>A change the line carries and the work has not merged yet is the line's, not the work's: no run of the work is held for it.</summary>
    [Fact]
    public async Task A_change_the_line_made_after_the_work_was_cut_holds_no_run_of_the_work()
    {
        var (cut, first) = await CommitAsync(Line);
        await GitFixture.RunAsync(_folder, "switch", "-q", "-c", "work");
        await GitFixture.RunAsync(_folder, "switch", "-q", "main");
        var (_, second) = await CommitAsync(Line.Replace("dotnet build", "dotnet build -c Release", StringComparison.Ordinal));
        await GitFixture.RunAsync(_folder, "switch", "-q", "work");

        Assert.Equal(first, (await GitFixture.GitAsync(_folder, "rev-parse", $"{cut}:daoris.gates.json")).Trim());
        Assert.Null((await RestrictedAsync(cut, second, null)).Held);
    }

    /// <summary>Under full access the work's own declaration runs, whatever it changed.</summary>
    [Fact]
    public async Task Full_access_runs_the_works_own_declaration()
    {
        var (_, accepted) = await CommitAsync(Line);
        var (work, _) = await CommitAsync(Line.Replace("dotnet build", "dotnet build -c Release", StringComparison.Ordinal));

        var under = await CheckDeclaration.OfAsync(_folder, work, accepted, DeclarationMode.Full, Main, DeclarationUnits.Run(["build"], false), CancellationToken.None);

        Assert.Null(under.Held);
        Assert.Contains("-c Release", under.Text);
    }
}
