using Daoris.Contracts.Knowledge;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>The knowledge reads: what this host is, what it holds of each repository, and its searches.</summary>
public sealed class KnowledgeRoutes(HostWiring host) : IRouteModule
{
    // Each mark is a protocol this host keeps, at its version; a client keeps no mark the host does not say, and sends what a
    // mark gates only to a host that says it. `settled`: a key the line settled answers `Settled` to a say or hand asking with
    // `settled: true`. `opens` 2: the keyed open moves the words its `moves` names in its own step. `head`: the say door refuses
    // a record its line left `continued`, naming the head. `journal`: the change stream answers the journal's family and place
    // to a reader asking with `journal=1`. `answers`: one door for solicited answers. `presses`: a say naming `question` is a
    // person's press, checked against the question on file.
    private static readonly IReadOnlyDictionary<string, int> Marks = new Dictionary<string, int>
    {
        ["settled"] = 1, ["opens"] = 2, ["head"] = 1, ["journal"] = 1, ["answers"] = 1, ["presses"] = 1,
    };

    private KnowledgeService Knowledge => host.Service.Service;

    public void Map(RouteMap routes)
    {
        routes.Map<ReadStatus, StatusResponse>(Status);
        routes.Map<ReadProtocol, ProtocolResponse>((_, _, _) => Task.FromResult<Answer<ProtocolResponse>>(new ProtocolResponse(host.Run, Marks)));
        routes.Map<ListRepositories, RepositoryResponse[]>(RepositoriesAsync);
        routes.Map<Search, HitResponse[]>(SearchAsync);
        routes.Map<ReadEntry, EntryResponse>(EntryAsync);
        routes.Map<ListEntries, EntryResponse[]>(async (request, _, ct) => (await Knowledge.LocalEntriesAsync(request.Repository, ct)).Select(ToEntry).ToArray());
        routes.Map<FindConvergence, ConvergenceResponse[]>(ConvergenceAsync);
    }

    private Task<Answer<StatusResponse>> Status(ReadStatus request, HttpContext http, CancellationToken ct) =>
        Task.FromResult<Answer<StatusResponse>>(new StatusResponse(
            Semantic: host.Service.SemanticEnabled,
            Tier: host.Service.SemanticEnabled ? "lexical + semantic" : "lexical only",
            Note: host.Service.SemanticEnabled
                ? null
                : $"Set {ServiceOptions.ModelVariable} to enable semantic recall — it is what finds two "
                  + "repositories that reached the same conclusion in different words.",
            // The shell hands its modules and the page the key only once the host it started proves it holds it, so a
            // process that took the port first never learns it.
            Proof: string.IsNullOrEmpty(request.Prove) ? null : host.PersonKey?.Prove(request.Prove),
            Home: host.Home,
            Version: HostVersion.Stamped,
            Closure: true,
            CloseOutcome: true,
            CloseRemote: true));

    private async Task<Answer<RepositoryResponse[]>> RepositoriesAsync(ListRepositories request, HttpContext http, CancellationToken ct) =>
        (await Knowledge.SummarizeAsync(request.Workspace, ct))
            .Select(r => new RepositoryResponse(
                r.Repository, r.Total, r.Local, r.Canonical, r.Workspace,
                r.Fed is null ? null : new ProvenanceResponse(r.Fed.Commit, r.Fed.ShortCommit, r.Fed.CommittedAt, r.Fed.Branch, r.Fed.Origin),
                r.IndexedAt, r.IndexedCommit))
            .ToArray();

    private async Task<Answer<HitResponse[]>> SearchAsync(Search request, HttpContext http, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Q)) return Refusal.BadRequest("q is required");
        if (KindsOrRefusal(request.Kinds) is { Refusal: { } refused }) return Refusal.BadRequest(refused);

        var answer = await Knowledge.AnswerAsync(new KnowledgeQuery(request.Q)
        {
            Kinds = KnowledgeQuery.ParseKinds(request.Kinds),
            Repositories = KnowledgeQuery.ParseSet(request.Repositories),
            Provenance = (request.LocalOnly ?? true) ? Provenance.Local : null,
            Limit = Math.Clamp(request.Limit ?? 20, 1, 100),
            Workspace = request.Workspace,
        }, ct);

        // A header is ASCII, so the failure's own words are not sent here: the sentence is the status door's and the refresh's.
        http.Response.Headers["x-daoris-tier"] = answer.Tier;
        return answer.Hits
            .Select(h => new HitResponse(
                h.Entry.Id, h.Entry.Repository, h.Entry.Kind.ToString(), h.Entry.Title,
                h.Entry.RelativePath, h.Excerpt, h.Score, h.Entry.Workspace,
                h.Entry.Lines?.First, h.Entry.Lines?.Last, h.ExcerptLine))
            .ToArray();
    }

    private async Task<Answer<EntryResponse>> EntryAsync(ReadEntry request, HttpContext http, CancellationToken ct) =>
        await Knowledge.FindAsync(request.Id, ct) is { } entry
            ? ToEntry(entry)
            : Refusal.NotFound($"no entry with id '{request.Id}'");

    private async Task<Answer<ConvergenceResponse[]>> ConvergenceAsync(FindConvergence request, HttpContext http, CancellationToken ct)
    {
        if (KindsOrRefusal(request.Kinds) is { Refusal: { } refused }) return Refusal.BadRequest(refused);

        var found = await Knowledge.FindConvergenceAsync(
            new ConvergenceOptions(
                Math.Clamp(request.MinimumSimilarity ?? 0.82, 0, 1), KnowledgeQuery.ParseKinds(request.Kinds),
                Math.Clamp(request.Limit ?? 25, 1, 100), request.Workspace),
            ct);
        return found
            .Select(c => new ConvergenceResponse(
                c.Method.ToString(), c.Similarity, c.Repositories,
                c.Entries.Select(e => new ConvergenceEntryResponse(e.Id, e.Repository, e.Kind.ToString(), e.Title, e.RelativePath)).ToList(),
                SuggestionFor(c)))
            .ToArray();
    }

    // A kind nobody has is the caller's mistake, said back as a 400 rather than widened to every kind.
    private static (IReadOnlySet<EntryKind>? Kinds, string? Refusal) KindsOrRefusal(string? kinds)
    {
        try
        {
            return (KnowledgeQuery.ParseKinds(kinds), null);
        }
        catch (ArgumentException refused)
        {
            return (null, refused.Message);
        }
    }

    // A convergence is a prompt to look, so the suggestion says what to read and where the change goes, never "apply this":
    // doctrine that appeared without anyone choosing it is the failure this project exists to prevent.
    private static string SuggestionFor(ConvergenceCandidate candidate) => candidate.Method switch
    {
        ConvergenceMethod.Identical =>
            "The same document in more than one repository. If it should be doctrine, promote one copy with "
            + "`daoris upstream <file>` from the repository that owns it, then let the others sync.",
        ConvergenceMethod.Restatement =>
            "A copy that has drifted. Read both, decide which wording is right, and promote that one with "
            + "`daoris upstream <file>`.",
        _ =>
            "The same lesson in different words — the case no text comparison finds. Read both: what they "
            + "share may be canonical, and what differs is usually each repository's own and must stay local.",
    };

    private static EntryResponse ToEntry(KnowledgeEntry entry) => new(
        entry.Id, entry.Repository, entry.Kind.ToString(), entry.Provenance.ToString(),
        entry.Title, entry.RelativePath, entry.Body, entry.Anchor, entry.Workspace,
        entry.Lines?.First, entry.Lines?.Last);
}
