using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1r part r2 over real git (the integration design §17.7, §4.3): the gates a choice's door reads of a repository's accepted
/// integration declaration, only where the accepted blob is the declaration at the line's tip. A declaration accepted and naming a gate names
/// it; none accepted, one the line changed since, or one naming no gate names none; a repository with no checkout here is not read, never
/// passed.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class IntegrateAcceptedGitTests : LandedFixture
{
    private const string Declared = """{ "gates": [ { "name": "unit", "run": "node check.mjs" }, { "name": "lint", "run": "node lint.mjs" } ] }""" + "\n";

    private const string NoGate = """{ "gates": [] }""" + "\n";

    private static async Task<string> BlobAsync(string root) => (await GitAsync(root, "rev-parse", "HEAD:daoris.gates.json")).Trim();

    [Fact]
    public async Task The_accepted_declaration_at_the_lines_tip_names_its_gates()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "daoris.gates.json", Declared, "the declaration");
        var config = DriverConfig.Empty.WithLine("engine", "main").WithIntegrationGates("engine", await BlobAsync(root));

        var read = await IntegrateChoice.ReadAcceptedAsync(config, "engine", "aurora", root, CancellationToken.None);

        Assert.Null(read.Unread);
        Assert.Equal(["unit", "lint"], read.Names);
    }

    [Fact]
    public async Task A_declaration_naming_no_gate_none_accepted_and_one_the_line_changed_since_each_name_none()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "daoris.gates.json", NoGate, "a declaration with no gate");
        var accepted = DriverConfig.Empty.WithLine("engine", "main").WithIntegrationGates("engine", await BlobAsync(root));
        Assert.Empty((await IntegrateChoice.ReadAcceptedAsync(accepted, "engine", "aurora", root, CancellationToken.None)).Names!);

        // Nothing accepted.
        Assert.Empty((await IntegrateChoice.ReadAcceptedAsync(DriverConfig.Empty.WithLine("engine", "main"), "engine", "aurora", root, CancellationToken.None)).Names!);

        // The line's declaration changed after the owner accepted: it waits for the owner, so no gate is named.
        await CommitAsync(root, "daoris.gates.json", Declared, "the declaration changed");
        var stale = await IntegrateChoice.ReadAcceptedAsync(accepted, "engine", "aurora", root, CancellationToken.None);
        Assert.Null(stale.Unread);
        Assert.Empty(stale.Names!);
    }

    [Fact]
    public async Task A_repository_with_no_checkout_here_is_not_read_never_passed()
    {
        var config = DriverConfig.Empty.WithLine("engine", "main");

        var none = await IntegrateChoice.ReadAcceptedAsync(config, "engine", "aurora", null, CancellationToken.None);
        var gone = await IntegrateChoice.ReadAcceptedAsync(config, "engine", "aurora", Path.Combine(Scratch, "nowhere"), CancellationToken.None);

        Assert.Null(none.Names);
        Assert.Contains("has no checkout here", none.Unread);
        Assert.Equal(none, gone);
    }
}
