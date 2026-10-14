using Daoris.Knowledge;
using Lyntai.Inference;
using Lyntai.Memory;

namespace Daoris.Service.Tests;

/// <summary>
/// SEM3 (D123): a long entry's tail must reach a vector. The semantic tier embedded an entry's title and
/// the first 2,000 characters of its body and dropped the rest without a word, and an embedder cuts
/// whatever passes its own window the same way — so a search for something a long decision says near its
/// end could not find it by meaning, however close the meaning was.
/// </summary>
/// <remarks>
/// The vectors here are the deterministic stand-in's (<see cref="DimensionEmbedder"/>): one dimension per
/// group of words, so what a vector carries is countable. The fact's group is "certificate"/"credential";
/// the filler's is the build vocabulary. A query for "credential" shares no word with any entry, so the
/// lexical half answers nothing and the order is the semantic half's alone. The distractor mentions the
/// fact's meaning once among six build words (cosine 1/√37), which is what the long entry has to beat.
/// </remarks>
public class LongEntryTests
{
    private static readonly string[] Fact = ["certificate", "credential"];
    private static readonly string[] Build = ["pipeline", "build", "stage", "deploy", "artifact", "runner"];

    /// <summary>Numbered, so one paragraph's passage is told from another's.</summary>
    private static string Filler(int step) =>
        $"Step {step}: the pipeline runs every build stage in order, and a stage that fails stops the pipeline before anything ships to anyone.\n\n";

    private static string Fillers(int count) => string.Concat(Enumerable.Range(1, count).Select(Filler));

    private const string TailFact =
        "Renew the signing certificate a month before it lapses: a lapsed certificate fails every install without a word.";

    /// <summary>A long knowledge document whose one statement of the fact sits past 2,000 characters.</summary>
    private static KnowledgeEntry Handbook(int fillers = 20) => new(
        "alpha", EntryKind.Knowledge, Provenance.Local, "Release handbook",
        Fillers(fillers) + TailFact,
        ".claude/knowledge/release-handbook.md");

    private static readonly KnowledgeEntry Distractor = new(
        "beta", EntryKind.Decision, Provenance.Local, "Signed installers",
        "Every deploy stage hands the artifact to a runner that signs it with the release certificate before the pipeline builds the installer.",
        "docs/DECISIONS.md", "D7");

    /// <summary>A source whose entries a test may change between refreshes, as a repository's files change.</summary>
    private sealed class Entries(params KnowledgeEntry[] entries) : IKnowledgeSource
    {
        public KnowledgeEntry[] Now { get; set; } = entries;

        public string Name => "fixture";

        public Task<IReadOnlyList<KnowledgeEntry>> ReadAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeEntry>>(Now);
    }

    /// <summary>
    /// What an embedder does at its context: reads the first <paramref name="context"/> characters of each
    /// text and says nothing about the rest (as Ollama's embed endpoint does by default).
    /// </summary>
    private sealed class CuttingEmbedder(IVectorProvider inner, int context) : IVectorProvider
    {
        public string Id => "test-cutting";

        public ProviderCapabilities Capabilities => inner.Capabilities;

        public Task<VectorResponse> CallAsync(VectorRequest request, CancellationToken ct = default) =>
            inner.CallAsync(request with { Texts = request.Texts.Select(t => t.Length <= context ? t : t[..context]).ToList() }, ct);
    }

    /// <summary>An embedder that answers until told to fail, as an endpoint goes down between refreshes.</summary>
    private sealed class FailingWhenTold(IVectorProvider inner) : IVectorProvider
    {
        public bool Down { get; set; }

        public string Id => "test-failing";

        public ProviderCapabilities Capabilities => inner.Capabilities;

        public Task<VectorResponse> CallAsync(VectorRequest request, CancellationToken ct = default) =>
            Down && request.Role == EmbeddingRole.Document
                ? Task.FromResult(VectorResponse.Failure(ProviderVerdict.Failed, "the endpoint went away"))
                : inner.CallAsync(request, ct);
    }

    private static KnowledgeService Service(params KnowledgeEntry[] entries) =>
        Service(new Entries(entries), new DimensionEmbedder(Fact, Build));

    private static KnowledgeService Service(
        IKnowledgeSource source, IVectorProvider embedder, int window = EntryPieces.DefaultWindow)
    {
        var store = new InMemoryKnowledgeStore();
        var vectors = new InMemoryVectorStore();
        return new KnowledgeService(
            store,
            new HybridKnowledgeSearch(new LexicalKnowledgeSearch(store), new SemanticKnowledgeSearch(store, embedder, vectors)),
            source,
            DisclosurePolicy.LocalOnly,
            embedder,
            vectors,
            embedWindow: window);
    }

    /// <summary>
    /// The measurement: the fact sits only past the cut, and a search for its meaning must put the
    /// entry that says it first. Before SEM3 its one vector held build words only (cosine 0), and the
    /// distractor that mentions the meaning in passing took the place.
    /// </summary>
    [Fact]
    public async Task A_fact_past_the_old_cut_is_found_by_meaning()
    {
        var handbook = Handbook();
        Assert.True(handbook.Body.IndexOf("certificate", StringComparison.Ordinal) > 2000);
        var service = Service(handbook, Distractor);
        await service.RefreshAsync();

        var answer = await service.AnswerAsync(new KnowledgeQuery("credential") { Limit = 1 });

        Assert.Equal("lexical+semantic", answer.Tier);
        Assert.Equal(handbook.Id, Assert.Single(answer.Hits).Entry.Id);
    }

    /// <summary>
    /// The embedder's own cut, which Daoris cannot see: an embedder whose context is 600 characters reads
    /// the opening of each text and drops the rest without a word. The window the deployment states is what
    /// makes every piece fit it (D24): unstated, the fact at character 900 is still lost; stated, it is found.
    /// </summary>
    [Fact]
    public async Task A_window_the_deployment_states_keeps_every_piece_inside_its_embedders_context()
    {
        var handbook = Handbook(fillers: 7);
        Assert.InRange(handbook.Body.IndexOf("certificate", StringComparison.Ordinal), 601, 2000);
        var query = new KnowledgeQuery("credential") { Limit = 1 };

        var unstated = Service(new Entries(handbook, Distractor), new CuttingEmbedder(new DimensionEmbedder(Fact, Build), 600));
        await unstated.RefreshAsync();
        Assert.Equal(Distractor.Id, Assert.Single((await unstated.AnswerAsync(query)).Hits).Entry.Id);

        var stated = Service(new Entries(handbook, Distractor), new CuttingEmbedder(new DimensionEmbedder(Fact, Build), 600), window: 600);
        await stated.RefreshAsync();
        Assert.Equal(handbook.Id, Assert.Single((await stated.AnswerAsync(query)).Hits).Entry.Id);
    }

    /// <summary>Nothing is silent: the refresh says how many entries became more than one vector, and at what window.</summary>
    [Fact]
    public async Task The_refresh_says_how_many_entries_were_split()
    {
        var service = Service(Handbook(), Distractor);

        var report = await service.RefreshAsync();

        var embedded = Assert.IsType<EmbeddingReport>(report.Embedded);
        Assert.Equal(2, embedded.Entries);
        Assert.Equal(1, embedded.Split);
        Assert.Equal(EntryPieces.Of(Handbook(), EntryPieces.DefaultWindow).Count + 1, embedded.Pieces);
        Assert.True(embedded.Pieces > 2);
        Assert.Equal(EntryPieces.DefaultWindow, embedded.Window);
    }

    [Fact]
    public async Task With_no_embedder_the_refresh_reports_no_embedding()
    {
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(store, new LexicalKnowledgeSearch(store), new Entries(Handbook()));

        var report = await service.RefreshAsync();

        Assert.Null(report.Embedded);
        Assert.Null(report.SemanticError);
    }

    /// <summary>
    /// An entry is several vectors and a hit names it once, at its best piece — and a long entry whose
    /// pieces fill every place one read returns does not cost the answer the other entries.
    /// </summary>
    [Fact]
    public async Task A_hit_names_its_entry_once_however_many_of_its_pieces_match()
    {
        var everywhere = new KnowledgeEntry(
            "alpha", EntryKind.Decision, Provenance.Local, "Certificates",
            string.Join("\n\n", Enumerable.Range(1, 400).Select(i => $"Note {i}: the certificate is renewed by hand, every time, by whoever is on call.")),
            "docs/DECISIONS.md", "D9");
        var others = Enumerable.Range(1, 3).Select(i => new KnowledgeEntry(
                $"repo{i}", EntryKind.Fix, Provenance.Local, $"Fix {i}",
                "A build stage failed when the certificate in the pipeline's runner had lapsed.", "docs/FIX-LOG.md", $"F{i}"))
            .ToArray();
        var service = Service(new Entries([everywhere, .. others]), new DimensionEmbedder(Fact, Build), window: 300);

        var report = await service.RefreshAsync();
        var answer = await service.AnswerAsync(new KnowledgeQuery("credential") { Limit = 4 });

        // The hybrid asks its semantic half for 30, which reads 120 places at first: the long entry's
        // pieces must outnumber them, or the read further is never needed.
        Assert.True(report.Embedded!.Pieces > 120, $"the fixture must outnumber one read's places, got {report.Embedded.Pieces}");
        Assert.Equal(4, answer.Hits.Count);
        Assert.Equal(answer.Hits.Count, answer.Hits.Select(h => h.Entry.Id).Distinct().Count());
        Assert.Equal(everywhere.Id, answer.Hits[0].Entry.Id);
    }

    /// <summary>A hit found in a later piece shows that piece's passage, so a reader can see why it matched.</summary>
    [Fact]
    public async Task A_hit_found_in_a_later_piece_shows_that_passage()
    {
        var handbook = Handbook();
        var service = Service(handbook, Distractor);
        await service.RefreshAsync();

        var hit = Assert.Single((await service.AnswerAsync(new KnowledgeQuery("credential") { Limit = 1 })).Hits);

        var last = EntryPieces.Of(handbook, EntryPieces.DefaultWindow)[^1];
        Assert.True(last.Start > 0);
        Assert.Equal("…" + Text.Excerpt(handbook.Body[last.Start..]), hit.Excerpt);
        Assert.DoesNotContain("Step 1:", hit.Excerpt);
    }

    /// <summary>
    /// An entry is several ids now, so a refresh replaces the collection whole: an edited entry's old
    /// pieces must not be found for words it no longer has.
    /// </summary>
    [Fact]
    public async Task An_edited_entrys_old_pieces_leave_with_it()
    {
        var source = new Entries(Handbook(), Distractor);
        var service = Service(source, new DimensionEmbedder(Fact, Build));
        await service.RefreshAsync();

        // Shorter now, and without the fact: one piece where there were two, so the old second is left over
        // unless the refresh replaces the collection rather than writing into it.
        source.Now = [Handbook() with { Body = Fillers(3) }, Distractor];
        await service.RefreshAsync();

        var hit = Assert.Single((await service.AnswerAsync(new KnowledgeQuery("credential") { Limit = 1 })).Hits);
        Assert.Equal(Distractor.Id, hit.Entry.Id);
    }

    /// <summary>
    /// Every piece is embedded before the collection changes, so an embedder that fails during a refresh
    /// leaves what the last one made: the semantic half still answers, and the refresh says why it did not
    /// embed.
    /// </summary>
    [Fact]
    public async Task An_embedder_that_fails_during_a_refresh_leaves_the_last_vectors()
    {
        var embedder = new FailingWhenTold(new DimensionEmbedder(Fact, Build));
        var service = Service(new Entries(Handbook(), Distractor), embedder);
        await service.RefreshAsync();

        embedder.Down = true;
        var report = await service.RefreshAsync();
        var answer = await service.AnswerAsync(new KnowledgeQuery("credential") { Limit = 1 });

        Assert.Contains("the endpoint went away", report.SemanticError);
        Assert.Null(report.Embedded);
        Assert.Equal(Handbook().Id, Assert.Single(answer.Hits).Entry.Id);
    }

    /// <summary>
    /// Convergence compares a seed with every piece of its neighbours, and names each entry once (D123).
    /// Both entries are long, so whichever the detector takes as the seed, the other is many vectors.
    /// </summary>
    [Fact]
    public async Task Convergence_names_a_long_neighbour_once()
    {
        var seed = new KnowledgeEntry(
            "beta", EntryKind.Decision, Provenance.Local, "Credentials lapse",
            string.Join("\n\n", Enumerable.Range(1, 40).Select(i => $"Remark {i}: a credential has to be renewed before it lapses, or nothing installs.")),
            "docs/DECISIONS.md", "D3");
        var neighbour = new KnowledgeEntry(
            "alpha", EntryKind.Knowledge, Provenance.Local, "Certificates",
            string.Join("\n\n", Enumerable.Range(1, 40).Select(i => $"Note {i}: the certificate is renewed by hand, every time, by whoever is on call.")),
            ".claude/knowledge/certificates.md");
        var store = new InMemoryKnowledgeStore();
        await store.ReplaceRepositoryAsync("alpha", [neighbour]);
        await store.ReplaceRepositoryAsync("beta", [seed]);
        var embedder = new DimensionEmbedder(Fact);
        var vectors = new InMemoryVectorStore();
        var report = await SemanticKnowledgeSearch.IndexAsync(await store.AllAsync(), embedder, vectors, window: 300);

        var found = await new ConvergenceDetector(store, embedder, vectors, window: 300).FindAsync(new ConvergenceOptions(0.5));

        Assert.True(report.Pieces > 10);
        var candidate = Assert.Single(found);
        Assert.Equal(ConvergenceMethod.Convergent, candidate.Method);
        Assert.Equal(2, candidate.Entries.Count);
        Assert.Equal(["alpha", "beta"], candidate.Repositories);
    }
}
