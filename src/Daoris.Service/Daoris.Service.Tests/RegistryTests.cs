using Daoris.Knowledge;

namespace Daoris.Service.Tests;

public sealed class RegistryTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-registry-" + Guid.NewGuid().ToString("N")[..8]);

    private void Repo(string name, string? manifest)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        if (manifest is not null) File.WriteAllText(Path.Combine(dir, "daoris.json"), manifest);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private IReadOnlyList<Registration> Read(Dictionary<string, int>? counts = null) =>
        new Registry(_root).Read(counts ?? []);

    [Fact]
    public void A_declared_domain_is_what_an_asker_reads()
    {
        Repo("Cognition", """
            {
              "source": "s", "packs": ["dotnet-library"],
              "domain": {
                "summary": "The LLM cognition layer.",
                "owns": ["provider adapters", "routing"],
                "accepts": ["a new provider", "a failing case"]
              }
            }
            """);

        var entry = Assert.Single(Read());

        Assert.True(entry.Adopted);
        Assert.True(entry.Registered);
        Assert.Equal("The LLM cognition layer.", entry.Summary);
        Assert.Equal(["provider adapters", "routing"], entry.Owns);
        Assert.Equal(["dotnet-library"], entry.Packs);
    }

    /// <summary>
    /// The scanned path honours the manifest's remote declaration too (D47 §4) — a repository that
    /// never ran `connect` still speaks through its manifest. Knowledge without join is narrowed to
    /// local here as well: the CLI refuses that manifest, but this scanner reads manifests the CLI
    /// never validated.
    /// </summary>
    [Fact]
    public void The_remote_declaration_is_scanned_from_the_manifest()
    {
        Repo("Joined", """{ "source": "s", "packs": [], "remote": { "join": true, "knowledge": true } }""");
        Repo("Orphaned", """{ "source": "s", "packs": [], "remote": { "knowledge": true } }""");
        Repo("Silent", """{ "source": "s", "packs": [] }""");

        var all = Read();
        var joined = all.Single(r => r.Repository == "Joined");
        var orphaned = all.Single(r => r.Repository == "Orphaned");
        var silent = all.Single(r => r.Repository == "Silent");

        Assert.True(joined.Joined);
        Assert.True(joined.SharesKnowledge);
        Assert.False(orphaned.Joined);
        Assert.False(orphaned.SharesKnowledge);
        Assert.False(silent.Joined);
        Assert.False(silent.SharesKnowledge);
    }

    /// <summary>
    /// Adoption is the gate on being addressed; declaring a domain is not. A repository that has adopted
    /// but said nothing is still reachable — it simply tells an asker less, and the asker is told that.
    /// </summary>
    [Fact]
    public void An_adopted_repository_with_no_domain_is_addressable_but_not_registered()
    {
        Repo("Quiet", """{ "source": "s", "packs": [] }""");

        var entry = Assert.Single(Read());

        Assert.True(entry.Adopted);
        Assert.False(entry.Registered);
    }

    /// <summary>
    /// "Who cannot be asked yet" is the same question as "who can". A silent omission reads as the
    /// repository not existing at all.
    /// </summary>
    [Fact]
    public void A_repository_that_has_not_adopted_is_listed_and_marked()
    {
        Repo("Stranger", null);

        var entry = Assert.Single(Read());

        Assert.False(entry.Adopted);
        Assert.False(entry.Registered);
    }

    /// <summary>A broken manifest is that repository's problem; it is not a reason to drop it off the map.</summary>
    [Fact]
    public void An_unparseable_manifest_still_appears_as_adopted()
    {
        Repo("Broken", "{ not json");

        var entry = Assert.Single(Read());

        Assert.True(entry.Adopted);
        Assert.Null(entry.Summary);
    }

    [Fact]
    public void Entry_counts_come_from_the_index()
    {
        Repo("Counted", """{ "source": "s" }""");

        Assert.Equal(42, Assert.Single(Read(new() { ["Counted"] = 42 })).Entries);
    }

    /// <summary>
    /// A scanned repository's root is its directory — which is how, in local mode, the driver knows
    /// where to spawn even for a repository that never ran `connect` (D46).
    /// </summary>
    [Fact]
    public void A_scanned_repository_carries_its_root()
    {
        Repo("Cognition", """{ "source": "s" }""");

        Assert.Equal(Path.Combine(_root, "Cognition"), Assert.Single(Read()).Root);
    }

    /// <summary>
    /// A pushed declaration wins on what the repository SAID; the scanned root survives when the push
    /// carried none — a declaration should not cost the driver the path it already knew.
    /// </summary>
    [Fact]
    public void A_pushed_registration_without_a_root_keeps_the_scanned_one()
    {
        Repo("Cognition", """{ "source": "s" }""");
        var registry = new Registry(_root);
        registry.Register(new Registration(
            "Cognition", Adopted: true, "The LLM cognition layer.",
            Owns: ["routing"], Accepts: [], Packs: [], Entries: 0));

        var entry = Assert.Single(registry.Read(new Dictionary<string, int>()));

        Assert.Equal("The LLM cognition layer.", entry.Summary);
        Assert.Equal(Path.Combine(_root, "Cognition"), entry.Root);
    }

    /// <summary>`connect` knows the real root — including one the scan could never find.</summary>
    [Fact]
    public void A_pushed_root_is_kept_for_a_repository_the_scan_cannot_see()
    {
        var registry = new Registry(_root);
        registry.Register(new Registration(
            "Elsewhere", Adopted: true, "Lives outside the knowledge root.",
            Owns: ["itself"], Accepts: [], Packs: [], Entries: 0, Root: "D:/other/Elsewhere"));

        Assert.Equal("D:/other/Elsewhere", Assert.Single(registry.Read(new Dictionary<string, int>())).Root);
    }
}
