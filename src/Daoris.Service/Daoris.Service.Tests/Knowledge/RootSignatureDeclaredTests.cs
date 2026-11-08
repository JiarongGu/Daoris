using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>A root's signature and its uncommitted answer cover the records the manifest declares outside the folders always walked.</summary>
public sealed class RootSignatureDeclaredTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"daoris-declared-{Guid.NewGuid():N}");

    public RootSignatureDeclaredTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "records", "decisions"));
        Directory.CreateDirectory(Path.Combine(_root, "rooms", "web"));
        File.WriteAllText(Path.Combine(_root, "records", "decisions", "D1.md"), "# D1\n\nChosen.\n");
        File.WriteAllText(Path.Combine(_root, "rooms", "web", "AGENTS.md"), "# Web\n\nA room.\n");
        File.WriteAllText(
            Path.Combine(_root, "daoris.json"),
            """{ "rooms": ["rooms/web"], "documents": { "decisions": "records/decisions" } }""");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void A_declared_decisions_folder_outside_docs_moves_the_signature_when_a_record_in_it_is_written()
    {
        var before = RootSignature.Of(_root);
        File.SetLastWriteTimeUtc(Path.Combine(_root, "records", "decisions", "D1.md"), DateTime.UtcNow.AddDays(1));

        Assert.NotEqual(before, RootSignature.Of(_root));
    }

    [Fact]
    public void A_rooms_instructions_move_the_signature_when_written()
    {
        var before = RootSignature.Of(_root);
        File.SetLastWriteTimeUtc(Path.Combine(_root, "rooms", "web", "AGENTS.md"), DateTime.UtcNow.AddDays(1));

        Assert.NotEqual(before, RootSignature.Of(_root));
    }

    [Fact]
    public void The_uncommitted_answer_asks_of_the_declared_records_and_the_rooms()
    {
        var places = UncommittedRecords.Folders(_root, documents: null, index: null);

        Assert.Contains("records/decisions", places);
        Assert.Contains("rooms/web/AGENTS.md", places);
    }
}
