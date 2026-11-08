using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using Lyntai.Inference;
using Lyntai.Memory;

namespace Daoris.Service.Tests;

/// <summary>A convergence answer from a service whose embedder failed says so, and never reads as nothing converging.</summary>
public sealed class ConvergenceFailureToolTests
{
    private sealed class DownEmbedder : IVectorProvider
    {
        public string Id => "test-down";

        public ProviderCapabilities Capabilities { get; } = new()
        {
            Accepts = [ProviderKinds.Text],
            Produces = [ProviderKinds.Vector],
            Operations = [ProviderOperation.Complete],
        };

        public Task<VectorResponse> CallAsync(VectorRequest request, CancellationToken ct = default) =>
            Task.FromResult(VectorResponse.Failure(ProviderVerdict.Failed, "The endpoint is down."));
    }

    private sealed class Entries : IKnowledgeSource
    {
        public string Name => "fixture";

        public Task<IReadOnlyList<KnowledgeEntry>> ReadAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeEntry>>([
                new("alpha", EntryKind.Knowledge, Provenance.Local, "shell", "Reading files through the terminal prompts every time.", ".claude/knowledge/shell.md"),
                new("beta", EntryKind.Knowledge, Provenance.Local, "tools", "Dedicated readers integrate with approvals.", ".claude/knowledge/tools.md"),
            ]);
    }

    [Fact]
    public async Task A_down_embedder_is_named_where_nothing_was_found()
    {
        var store = new InMemoryKnowledgeStore();
        var embedder = new DownEmbedder();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new Entries(), DisclosurePolicy.LocalOnly, embedder, new InMemoryVectorStore());
        await service.RefreshAsync();
        var tools = new KnowledgeSearchTools(service, new WorkspaceScope(service, new AmbientWorkspace(Path.Combine(Path.GetTempPath(), "daoris-down-alpha"))));

        var answer = await tools.ConvergenceAsync();

        Assert.Contains("did not run", answer);
        Assert.Contains("The endpoint is down", answer);
    }
}
