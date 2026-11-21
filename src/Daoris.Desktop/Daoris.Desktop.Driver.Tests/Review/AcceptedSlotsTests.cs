using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A repository's <c>heavySlots</c> is read from the declaration the person accepted, for every caller (a check, an integration's gated
/// line, a session's <c>run-heavy</c>), never from a branch's own file: a branch that raises it gets the line's count. A real git
/// repository in scratch, the branch's file differing from the accepted blob; the slots are the home's lock files.
/// </summary>
public sealed class AcceptedSlotsTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "accepted-slots", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => ScratchRemoval.Remove(_folder);

    private string Checkout => Path.Combine(_folder, "engine");

    /// <summary>A repository whose line holds a declaration of <paramref name="line"/> and whose checkout's own file then says <paramref name="branch"/>; the line's blob.</summary>
    private async Task<string> RepositoryAsync(string line, string branch)
    {
        Directory.CreateDirectory(Checkout);
        await GitFixture.GitAsync(Checkout, "init", "-q");
        await GitFixture.GitAsync(Checkout, "config", "user.email", "t@example.test");
        await GitFixture.GitAsync(Checkout, "config", "user.name", "t");
        var file = Path.Combine(Checkout, "daoris.gates.json");
        File.WriteAllText(file, line);
        await GitFixture.GitAsync(Checkout, "add", "daoris.gates.json");
        await GitFixture.GitAsync(Checkout, "commit", "-q", "-m", "line");
        var blob = (await GitFixture.GitAsync(Checkout, "rev-parse", "HEAD:daoris.gates.json")).Trim();
        File.WriteAllText(file, branch);
        return blob;
    }

    /// <summary>🔴 A branch's own file that raises <c>heavySlots</c> gets the line's count: two runs of the repository, the second waits.</summary>
    [Fact]
    public async Task A_branch_that_raises_its_own_heavy_slots_gets_the_lines_count()
    {
        var blob = await RepositoryAsync("""{ "heavySlots": 1 }""", """{ "heavySlots": 4 }""");
        var key = HeavySlot.KeyOf(Checkout)!;
        var plenty = new CheckRules(null, 16, 4, () => new MemoryReading(64L << 30, 64L << 30));
        var declared = await AcceptedSlots.OfBlobAsync(Checkout, blob, CancellationToken.None);

        HeavyAsk Ask(string session) => new(key, "engine", declared,
            () => new HeavyHolder(Environment.ProcessId, 1, Checkout, ["build"], Now, session, null) { Now = new HeavyGateNow("build", 1, Now) });

        var home = Path.Combine(_folder, "home");
        Directory.CreateDirectory(home);
        var (first, _) = CheckAdmission.TryTake(home, plenty, Ask("s-1"), (_, _) => true);
        var (second, wait) = CheckAdmission.TryTake(home, plenty, Ask("s-2"), (_, _) => true);
        using (first)
        {
            Assert.Equal(1, declared);
            Assert.NotNull(first);
            Assert.Null(second);
            Assert.Equal(CheckWait.Repository, wait!.Code);
        }
    }

    [Fact]
    public async Task A_line_that_declares_none_or_holds_no_declaration_says_nothing_whatever_the_branch_declares()
    {
        var blob = await RepositoryAsync("""{ "gates": [] }""", """{ "heavySlots": 4 }""");

        Assert.Null(await AcceptedSlots.OfBlobAsync(Checkout, blob, CancellationToken.None));
        Assert.Null(await AcceptedSlots.OfBlobAsync(Checkout, IntegrationAcceptance.None, CancellationToken.None));
        Assert.Null(await AcceptedSlots.OfBlobAsync(Checkout, null, CancellationToken.None));
        Assert.Null(await AcceptedSlots.OfBlobAsync(Checkout, "not-a-blob", CancellationToken.None));
    }

    /// <summary>A change of <c>heavySlots</c> is a change of the declaration that waits for the person: never kept across, and said by its key.</summary>
    [Fact]
    public void A_change_of_heavy_slots_holds_a_works_runs_as_a_declaration_change_and_is_said_by_its_key()
    {
        const string accepted = """{ "heavySlots": 1, "gates": [] }""";
        const string raised = """{ "heavySlots": 4, "gates": [] }""";

        Assert.False(DeclarationCommands.Keeps(accepted, raised));
        Assert.Equal(["heavySlots"], DeclarationChange.Between(accepted, raised).Other);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("""{ "gates": [] }""", null)]
    [InlineData("""{ "heavySlots": 2 }""", 2)]
    [InlineData("""{ "heavySlots": 0 }""", 1)]
    [InlineData("""{ "heavySlots": "two" }""", 1)]
    [InlineData("not json", null)]
    public void A_declarations_slots_read_from_its_text(string? text, int? slots) => Assert.Equal(slots, HeavyGates.SlotsIn(text));
}
