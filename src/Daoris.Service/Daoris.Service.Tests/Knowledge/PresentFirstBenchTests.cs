using System.Text.Json;
using Daoris.Knowledge;
using Xunit.Abstractions;

namespace Daoris.Service.Tests;

/// <summary>
/// The read-path bench's questions (<c>tools/read-path-bench-questions.json</c>) asked of this repository as it is,
/// through the deployment a workspace's own server is: one checkout, its <c>docs/</c> a section each, its index,
/// SQLite, words only. A present question's area document answers first for most of them, and a history question's
/// record is still within its first three. Each run writes its scorecard, the words' own rank beside it.
/// </summary>
public sealed class PresentFirstBenchTests(PresentFirstBenchTests.Checkout checkout, ITestOutputHelper output)
    : IClassFixture<PresentFirstBenchTests.Checkout>
{
    /// <summary>Of the twelve present questions, how many must find their area first.</summary>
    private const int PresentFirstAtLeast = 8;

    /// <summary>Of the twelve history questions, how many must find their record within three.</summary>
    private const int HistoryWithinThreeAtLeast = 9;

    /// <summary>This checkout, read once for every question.</summary>
    public sealed class Checkout : IAsyncLifetime
    {
        private readonly string _scratch = Path.Combine(
            Path.GetTempPath(), "daoris-present-bench-" + Guid.NewGuid().ToString("N")[..8]);

        private ComposedService? _composed;

        public KnowledgeService Service => _composed!.Service;

        public JsonElement Questions { get; private set; }

        public async Task InitializeAsync()
        {
            var root = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(root, "tools", "read-path-bench-questions.json")))
            {
                root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("no checkout holds the bench's questions");
            }

            Questions = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tools", "read-path-bench-questions.json"))).RootElement.Clone();
            Directory.CreateDirectory(_scratch);
            _composed = await ServiceFactory.CreateAsync(new ServiceOptions(
                _scratch, Path.Combine(_scratch, "knowledge.db"), Repository: root, Documents: "docs", Index: "docs/index"));
        }

        public async Task DisposeAsync()
        {
            if (_composed is not null) await _composed.DisposeAsync();
            SqlitePools.Release(Path.Combine(_scratch, "knowledge.db"));
            if (Directory.Exists(_scratch)) Directory.Delete(_scratch, recursive: true);
        }
    }

    private static readonly IReadOnlySet<EntryKind> EveryKind = Enum.GetValues<EntryKind>().ToHashSet();

    private async Task<(int First, int WithinThree, int WordsFirst)> ScoreAsync(string set)
    {
        var (first, withinThree, wordsFirst) = (0, 0, 0);
        foreach (var item in checkout.Questions.GetProperty(set).EnumerateArray())
        {
            var question = item.GetProperty("question").GetString()!;
            var right = item.GetProperty("answer").EnumerateArray().Select(path => path.GetString()).ToHashSet();
            // Local only, as knowledge_search asks unless told otherwise.
            var hits = await checkout.Service.SearchAsync(new KnowledgeQuery(question) { Provenance = Provenance.Local, Limit = 10 });
            var words = await checkout.Service.SearchAsync(
                new KnowledgeQuery(question) { Kinds = EveryKind, Provenance = Provenance.Local, Limit = 10 });

            var place = hits.Select((hit, at) => (hit, at)).FirstOrDefault(found => right.Contains(found.hit.Entry.RelativePath)).at;
            var found = hits.Any(hit => right.Contains(hit.Entry.RelativePath));
            if (found && place == 0) first++;
            if (found && place < 3) withinThree++;
            if (words.Count > 0 && right.Contains(words[0].Entry.RelativePath)) wordsFirst++;
            output.WriteLine($"{(found ? place + 1 : 0),2} | {question} | first: {(hits.Count > 0 ? hits[0].Entry.RelativePath : "-")}"
                           + $" | words first: {(words.Count > 0 ? words[0].Entry.RelativePath : "-")}");
        }

        output.WriteLine($"{set}: first {first}, within three {withinThree}; the words' own rank first {wordsFirst}");
        return (first, withinThree, wordsFirst);
    }

    [Fact]
    public async Task Most_present_questions_find_their_area_first()
    {
        var (first, _, _) = await ScoreAsync("present");

        Assert.True(first >= PresentFirstAtLeast, $"{first} present questions found their area first");
    }

    [Fact]
    public async Task History_questions_still_find_their_record_within_three()
    {
        var (_, withinThree, _) = await ScoreAsync("history");

        Assert.True(withinThree >= HistoryWithinThreeAtLeast, $"{withinThree} history questions found their record within three");
    }
}
