using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// A present question is searched in the present first, asked of a miniature checkout through the deployment a
/// workspace's own server is (one checkout, its <c>docs/</c> a section each, SQLite, words only). The rule is a line in
/// an area document under a heading that shares none of the question's words; the competition is history titled with
/// them: a decision, a filed design, a fix and a finished task.
/// </summary>
public sealed class PresentFirstTests : IDisposable
{
    private const string PresentQuestion = "what does the comment gate refuse";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-present-first-" + Guid.NewGuid().ToString("N")[..8]);

    private ComposedService? _composed;

    public void Dispose()
    {
        _composed?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        SqlitePools.Release(Path.Combine(_root, "knowledge.db"));
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private void Write(string relative, string content)
    {
        var file = Path.Combine(_root, "family", "served", relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    private void Record()
    {
        Write("daoris.json", """
            { "source": "s", "documents": { "areas": "docs/areas", "decisions": "docs/decisions",
              "fixes": "docs/FIX-LOG.md", "archive": "docs/task-archive.md" } }
            """);
        Write("docs/areas/gates.md", """
            # Gates

            ## Its rules

            1. A gate reports what it found and the file it found it in.
            2. The comment gate refuses a comment that tells history: a row id, a date, or what changed. (from D10)
            """);
        Write("docs/decisions/D10.md", """
            ## D10 — The comment gate refuses a comment that tells history (2026-09-01)

            **Decision (COMMENTGATE1).** The comment gate refuses a comment naming a row id or a date: the comment gate
            reads what a change adds, and refuses history there.
            """);
        Write("docs/archive/2026-09-01-comment-gate-design.md", """
            # The comment gate: what it refuses

            ## What the comment gate refuses

            The comment gate refuses a comment that names a row, a date or a change. What the gate refuses is history.
            """);
        Write("docs/2026-10-01-gate-report-design.md", """
            # Gate reports

            ## How a gate reports

            A gate's report names the file, the line and what the gate refuses there.
            """);
        Write("docs/FIX-LOG.md", """
            # Fix log

            ## 2026-09-02 - The comment gate refused a comment that quoted a date in a test name (GATEFIX1)

            **Root cause.** The comment gate read a test's name as a comment, and refused the date it carried.
            """);
        Write("docs/task-archive.md", """
            # Task archive

            ## COMMENTGATE1 — the comment gate (2026-09-01)

            > Outcome: the comment gate refuses a comment that tells history, in what a change adds.
            """);
        for (var n = 200; n < 240; n++)
        {
            Write($"docs/decisions/D{n}.md",
                $"## D{n} — The record of part {n} (2026-09-01)\n\n**Decision.** What was chosen for part {n} is "
                + $"written here, with the reasons the record gives for it and the options it weighed.\n");
        }
    }

    private async Task<KnowledgeService> ServiceAsync()
    {
        if (_composed is not null) return _composed.Service;
        Record();
        var checkout = Path.Combine(_root, "family", "served");
        _composed = await ServiceFactory.CreateAsync(new ServiceOptions(
            Path.Combine(_root, "family"), Path.Combine(_root, "knowledge.db"), Repository: checkout, Documents: "docs"));
        return _composed.Service;
    }

    private async Task<IReadOnlyList<KnowledgeEntry>> AskAsync(string question, string? kinds = null, int limit = 10) =>
        [.. (await (await ServiceAsync()).SearchAsync(new KnowledgeQuery(question)
        {
            Kinds = KnowledgeQuery.ParseKinds(kinds), Limit = limit,
        })).Select(hit => hit.Entry)];

    /// <summary>
    /// The area's rule answers first, though every history entry is titled with the question's words, at any limit:
    /// the rest keeps a third of the places at most.
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(1)]
    public async Task A_present_question_answers_the_area_rule_first(int limit)
    {
        var first = (await AskAsync(PresentQuestion, limit: limit))[0];

        Assert.Equal("docs/areas/gates.md", first.RelativePath);
    }

    /// <summary>History still answers, after every present entry, so a question the words misread is not left empty.</summary>
    [Fact]
    public async Task A_present_question_answers_history_after_the_present()
    {
        var present = (await AskAsync(PresentQuestion)).Select(entry => PresentFirst.TenseOf(entry) == EntryTense.Present).ToList();

        Assert.Contains(false, present);
        Assert.Equal(present.OrderBy(isPresent => !isPresent), present);
    }

    /// <summary>A design under the archive folder is history; one still at the documents' top is dated; the area present.</summary>
    [Fact]
    public async Task A_filed_design_is_history_a_live_design_dated_and_an_area_present()
    {
        var hits = await AskAsync(PresentQuestion);

        Assert.Equal(EntryTense.History, PresentFirst.TenseOf(hits.First(entry => entry.RelativePath.StartsWith("docs/archive/"))));
        Assert.Equal(EntryTense.Dated, PresentFirst.TenseOf(hits.First(entry => entry.RelativePath == "docs/2026-10-01-gate-report-design.md")));
        Assert.Equal(EntryTense.Present, PresentFirst.TenseOf(hits.First(entry => entry.RelativePath == "docs/areas/gates.md")));
    }

    /// <summary>A question about history ranks every kind as the words do, so its record answers first.</summary>
    [Theory]
    [InlineData("why does the comment gate refuse history")]
    [InlineData("D10 the comment gate")]
    [InlineData("COMMENTGATE1")]
    public async Task A_question_about_history_answers_history_first(string question)
    {
        Assert.Equal(EntryTense.History, PresentFirst.TenseOf((await AskAsync(question))[0]));
    }

    /// <summary>Kinds named are an explicit scope: the search answers them as asked.</summary>
    [Fact]
    public async Task Named_kinds_are_answered_as_asked()
    {
        var hits = await AskAsync(PresentQuestion, "decision");

        Assert.Equal("docs/decisions/D10.md", hits[0].RelativePath);
    }

    [Theory]
    [InlineData("what does the comment gate refuse", false)]
    [InlineData("what happens when a plugin's apiVersion is newer", false)]
    [InlineData("how does the store use FTS", false)]
    [InlineData("why is the archive one file", true)]
    [InlineData("When did the old workflow retire", true)]
    [InlineData("what decided the probe lock", true)]
    [InlineData("D228", true)]
    [InlineData("WORKFLOWCLEAN1 outcome", true)]
    [InlineData("FOUNDATION1d-i", true)]
    [InlineData("the root cause of the sign-out", true)]
    public void Which_questions_ask_about_history(string question, bool asks)
    {
        Assert.Equal(asks, PresentFirst.AsksHistory(question));
    }

    /// <summary>Each hit says whether it is the present or history, and the answer says the present came first.</summary>
    [Fact]
    public async Task The_tool_says_which_each_hit_is()
    {
        var service = await ServiceAsync();
        var tools = new KnowledgeSearchTools(service, new WorkspaceScope(service, new AmbientWorkspace(Path.GetTempPath())));

        var text = await tools.SearchAsync(PresentQuestion);

        Assert.Contains("· present ·", text);
        Assert.Contains("· dated ·", text);
        Assert.Contains("· history ·", text);
        Assert.Contains("The present came first", text);
    }
}
