using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COAGENT1b (the co-agent design §7.1): a piece's box under the home's <c>partners</c> entry. The ask is written once and never
/// again; the state carries every side effect's identity and is read back whole; the facts and the delivery receipt round-trip; an
/// unreadable file is told apart from an absent one, which a restart reads as uncertain.
/// </summary>
public sealed class PartnerFilesTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-partner-files-" + Guid.NewGuid().ToString("N")[..8]);

    public PartnerFilesTests() => Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Partners));

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void The_box_lives_under_the_homes_partners_entry_beside_the_helpers()
    {
        Assert.Same(HomeEntry.Partners, HomeEntry.Named("partners"));
        Assert.Equal((HomeCategory.Data, "data/partners"), (HomeEntry.Partners.Category, HomeEntry.Partners.New));
        Assert.Equal(HomeEntry.Table.ToList().IndexOf(HomeEntry.Helpers) + 1, HomeEntry.Table.ToList().IndexOf(HomeEntry.Partners));
    }

    [Fact]
    public void An_ask_is_written_once_and_read_back_and_never_written_again()
    {
        var files = new PartnerFiles(_home);
        var ask = new PartnerAsk("p1", "lead", "Make the window seven days.", ["src"], "codex-acp", "abc123", ["src/a.ts"], PartnerAsk.ByPerson, At);

        Assert.True(files.WriteAsk(ask));
        Assert.False(files.WriteAsk(ask with { Piece = "Something else." }));

        var read = Assert.Single(files.Asks());
        Assert.Equal((ask.Id, ask.Asker, ask.Piece, ask.Agent, ask.Base, ask.AskedBy, ask.At), (read.Id, read.Asker, read.Piece, read.Agent, read.Base, read.AskedBy, read.At));
        Assert.Equal(ask.Paths, read.Paths);
        Assert.Equal(ask.Uncommitted, read.Uncommitted);
    }

    [Fact]
    public void A_state_carries_each_side_effects_identity_whole()
    {
        var files = new PartnerFiles(_home);
        var state = new PartnerState("p1", PartnerStates.Imported, null, At)
        {
            Copy = "p-p1", Partner = "s9", Agent = "codex-acp", Account = "work", Job = "daoris-piece-p1", End = PartnerEnds.OutOfTime,
            Kept = At.AddMinutes(-1), Commit = "def456", CopyRemoved = true, TakenBy = "aaa111",
        };

        files.WriteState(state);

        Assert.Equal(state, files.ReadState("p1"));
        Assert.False(files.Unreadable("p1", "state"));
    }

    [Fact]
    public void An_unreadable_state_is_told_apart_from_an_absent_one()
    {
        var files = new PartnerFiles(_home);
        Assert.Null(files.ReadState("p1"));
        Assert.False(files.Unreadable("p1", "state"));

        File.WriteAllText(files.PathOf("p1", "state"), "{ half");

        Assert.Null(files.ReadState("p1"));
        Assert.True(files.Unreadable("p1", "state"));
    }

    [Fact]
    public void The_facts_and_the_receipt_round_trip_and_an_answer_is_read_as_partner_answer_writes_it()
    {
        var files = new PartnerFiles(_home);
        var facts = new PartnerFacts("p1", "def456", ["src/a.ts", "README.md"], ["README.md"], [new("debug.log", PieceImport.WhyIgnored)], At);
        files.WriteFacts(facts);
        var read = files.ReadFacts("p1")!;
        Assert.Equal((facts.Commit, facts.At), (read.Commit, read.At));
        Assert.Equal(facts.Files, read.Files);
        Assert.Equal(facts.Beyond, read.Beyond);
        Assert.Equal(facts.LeftOut, read.LeftOut);

        var receipt = PartnerDelivery.Claim(null, "p1", "lead", At);
        files.WriteReceipt(receipt);
        Assert.Equal(receipt, files.ReadReceipt("p1"));

        File.WriteAllText(files.PathOf("p1", "answer"), "{\n  \"id\": \"p1\",\n  \"summary\": \"Done.\",\n  \"left\": null,\n  \"at\": \"2026-10-22T12:00:00.0000000+00:00\"\n}\n");
        Assert.Equal(new PartnerAnswer("p1", "Done.", null, At), files.ReadAnswer("p1"));
    }
}
