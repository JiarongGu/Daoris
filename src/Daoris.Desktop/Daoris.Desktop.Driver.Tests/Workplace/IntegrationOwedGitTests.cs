using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1h (the integration design §5.8): the look's read of the declarations owed acceptance, against a real checkout. A line's
/// committed <c>daoris.gates.json</c> that declares an integration is owed while nothing is accepted (<c>unaccepted</c>) or another blob is
/// (<c>changed</c>), owes nothing once its blob is accepted, and a line that holds none, or one that declares no <c>integration</c>, owes
/// nothing. Git is asked once for a tip that moved, and not for one that did not.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class IntegrationOwedGitTests : IAsyncLifetime
{
    private const string Declares = """{ "gates": [], "integration": { "by": "daoris", "scratch": ["bin/**"] } }""";
    private const string Declaring = """{ "gates": [], "integration": { "by": "daoris", "scratch": ["obj/**"] } }""";
    private const string Without = """{ "gates": [] }""";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-integration-owed-" + Guid.NewGuid().ToString("N")[..8]);

    private string Root => Path.Combine(_home, "engine");

    private readonly DateTimeOffset _now = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Root);
        Assert.Equal(0, (await GitFixture.RunAsync(Root, "init", "-q", "-b", "main")).ExitCode);
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        return Task.CompletedTask;
    }

    private async Task<string> CommitAsync(string? declaration)
    {
        var file = Path.Combine(Root, "daoris.gates.json");
        if (declaration is null) File.Delete(file);
        else await File.WriteAllTextAsync(file, declaration);
        await File.WriteAllTextAsync(Path.Combine(Root, "moved.txt"), Guid.NewGuid().ToString("N"));
        await GitFixture.RunAsync(Root, "add", "-A");
        Assert.Equal(0, (await GitFixture.RunAsync(Root, "-c", "user.name=t", "-c", "user.email=t@example.invalid", "commit", "-q", "-m", "next")).ExitCode);
        return (await GitFixture.GitAsync(Root, "rev-parse", "HEAD:daoris.gates.json")).Trim() is { Length: 40 } blob && declaration is not null ? blob : "none";
    }

    private RepoView[] Registry => [new RepoView("engine", true, Root, "aurora")];

    private static DriverConfig Choices(string? accepted = null) =>
        accepted is null ? DriverConfig.Empty.WithLine("engine", "main") : DriverConfig.Empty.WithLine("engine", "main").WithIntegrationGates("engine", accepted);

    /// <summary>A declaration committed on the line and accepted by no one is owed as <c>unaccepted</c>, with the repository, workspace, line and blob.</summary>
    [Fact]
    public async Task A_declared_integration_nobody_accepted_is_owed_as_unaccepted()
    {
        var blob = await CommitAsync(Declares);
        var owed = new IntegrationOwed();

        await owed.LookAsync(Choices(), Registry, _now, CancellationToken.None);

        var each = Assert.Single(owed.Owed);
        Assert.Equal(("engine", "aurora", "main", blob, IntegrationAcceptance.Unaccepted, _now), (each.Repository, each.Workspace, each.Line, each.Blob, each.State, each.Since));
    }

    /// <summary>The tip's blob accepted, the repository owes nothing; and the next look, with no git, ends the row.</summary>
    [Fact]
    public async Task An_accepted_blob_owes_nothing_and_the_acceptance_ends_the_row_at_the_next_look_with_no_git()
    {
        var blob = await CommitAsync(Declares);
        var owed = new IntegrationOwed();
        await owed.LookAsync(Choices(), Registry, _now, CancellationToken.None);
        Assert.Single(owed.Owed);

        await owed.LookAsync(Choices(blob), Registry, _now.AddMinutes(1), CancellationToken.None);

        Assert.Empty(owed.Owed);
        Assert.Equal(0, owed.GitReads);
    }

    /// <summary>A blob that is not the accepted one is <c>changed</c>, since when it was first read, and a tip that moved to the accepted blob owes nothing.</summary>
    [Fact]
    public async Task A_declaration_changed_since_the_acceptance_is_owed_as_changed_and_keeps_its_since_across_looks()
    {
        var accepted = await CommitAsync(Declares);
        var changed = await CommitAsync(Declaring);
        var owed = new IntegrationOwed();

        await owed.LookAsync(Choices(accepted), Registry, _now, CancellationToken.None);
        await owed.LookAsync(Choices(accepted), Registry, _now.AddMinutes(5), CancellationToken.None);

        var each = Assert.Single(owed.Owed);
        Assert.Equal((IntegrationAcceptance.Changed, changed, _now), (each.State, each.Blob, each.Since));
    }

    /// <summary>A line that holds no declaration, or one that declares no integration, owes nothing whether or not something was accepted.</summary>
    [Fact]
    public async Task A_line_with_no_declaration_or_no_integration_owes_nothing()
    {
        await CommitAsync(null);
        var owed = new IntegrationOwed();
        await owed.LookAsync(Choices(), Registry, _now, CancellationToken.None);
        Assert.Empty(owed.Owed);

        await CommitAsync(Without);
        await owed.LookAsync(Choices(), Registry, _now, CancellationToken.None);
        Assert.Empty(owed.Owed);

        await CommitAsync(Declares);
        await owed.LookAsync(Choices(), Registry, _now, CancellationToken.None);
        Assert.Single(owed.Owed);

        // The declaration withdrawn: nothing accepted before changes that, only the line's tip.
        await CommitAsync(Without);
        await owed.LookAsync(Choices("0123456789abcdef0123456789abcdef01234567"), Registry, _now, CancellationToken.None);
        Assert.Empty(owed.Owed);
    }

    /// <summary>Git is asked when the line's tip moved and not when it did not; a repository with no checkout here is not read.</summary>
    [Fact]
    public async Task Git_is_asked_only_where_the_tip_moved_and_never_for_a_repository_with_no_checkout()
    {
        await CommitAsync(Declares);
        var owed = new IntegrationOwed();

        await owed.LookAsync(Choices(), Registry, _now, CancellationToken.None);
        Assert.True(owed.GitReads > 0);
        await owed.LookAsync(Choices(), Registry, _now.AddMinutes(1), CancellationToken.None);
        Assert.Equal(0, owed.GitReads);

        await CommitAsync(Declares);
        await owed.LookAsync(Choices(), Registry, _now.AddMinutes(2), CancellationToken.None);
        // The moved tip carries a blob seen before: the blob is asked for, its text is not.
        Assert.Equal(1, owed.GitReads);

        await owed.LookAsync(Choices(), [new RepoView("engine", true, null, "aurora"), new RepoView("web", true, Path.Combine(_home, "absent"), "aurora")],
            _now.AddMinutes(3), CancellationToken.None);
        Assert.Empty(owed.Owed);
        Assert.Equal(0, owed.GitReads);
    }
}
