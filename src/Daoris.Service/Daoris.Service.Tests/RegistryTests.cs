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
}
