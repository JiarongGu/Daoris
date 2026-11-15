using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>What the freshness classes stand on: a scratch folder holding a small served checkout, a service reading it and a clock a test moves.</summary>
public abstract class FreshnessCheckout() : ScratchFolder("daoris-fresh-")
{
    protected string Checkout()
    {
        Write("served/daoris.json", """{ "source": "s" }""");
        Write("served/docs/design.md", "# The probe lock\n\n## Why a lock\n\nOne probe at a time.\n");
        Write("served/.git/HEAD", "ref: refs/heads/main\n");
        Write("served/.git/refs/heads/main", "0000000000000000000000000000000000000000\n");
        return Path.Combine(_root, "served");
    }

    protected static KnowledgeService Serve(string checkout, CheckoutFreshness? freshness, TimeProvider time)
    {
        var store = new InMemoryKnowledgeStore();
        return new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new FileSystemKnowledgeSource([checkout], new RepositoryScanner(documents: "docs")),
            rereadAfter: TimeSpan.FromMinutes(1), time: time, freshness: freshness);
    }

    protected static async Task<bool> Finds(KnowledgeService service) =>
        (await service.SearchAsync(new KnowledgeQuery("zeppelins"))).Any(hit => hit.Entry.Title == "Zeppelins");

    protected sealed class SteppedTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
