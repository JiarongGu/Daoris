using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// REVIEWENV1a (D154 point 2, the review-environment design §1.1–§1.3, §1.7): the review rule, <c>reviews</c> and
/// <c>workspaceReviews</c> in <c>driver.json</c> — read, refused, resolved, said and edited. Nothing here starts a process.
/// </summary>
/// <remarks>
/// The driver's half of a TWIN with the CLI's <c>reviews.ts</c>: both are held, cell for cell, to ONE table, the CLI's
/// <c>test/fixtures/review-rules.json</c>, which <c>driverconfig.test.ts</c> reads too. A row changed on one side alone is the
/// other side's failure.
/// </remarks>
public sealed class ReviewRulesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-review-rules-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    public static TheoryData<string, string, string, string?, string?, string?> ReadRows() => Rows(
        "read", row => (Cell(row, 0)!, Cell(row, 1)!, Cell(row, 2)!, Cell(row, 3), Cell(row, 4), Cell(row, 5)));

    [Theory]
    [MemberData(nameof(ReadRows))]
    public void A_review_rule_resolves_as_the_cli_resolves_it(
        string name, string file, string repository, string? workspace, string? source, string? rule)
    {
        var resolved = ReviewRules.Resolve(DriverConfig.Parse(file), repository, workspace);

        Assert.True(source == resolved?.Source, $"{name}: from {resolved?.Source ?? "nothing"}");
        Assert.True(Same(rule, resolved is null ? null : ReviewRules.ToJson(resolved.Rule)), $"{name}: the rule");
    }

    public static TheoryData<string, string, string?> ProblemRows() => Rows(
        "problems", row => (Cell(row, 0)!, Cell(row, 1)!, Cell(row, 2)));

    [Theory]
    [MemberData(nameof(ProblemRows))]
    public void A_review_rule_s_first_problem_is_the_cli_s_in_its_words(string name, string value, string? problem)
    {
        using var document = JsonDocument.Parse(value);

        Assert.True(problem == ReviewRules.Read(document.RootElement).Problem, $"{name}: {ReviewRules.Read(document.RootElement).Problem}");
    }

    public static TheoryData<string, string, string[]> SaysRows() => Rows(
        "says", row => (Cell(row, 0)!, Cell(row, 1)!, row[2].EnumerateArray().Select(each => each.GetString()!).ToArray()));

    [Theory]
    [MemberData(nameof(SaysRows))]
    public void Each_door_says_what_a_rule_lets_a_step_do_in_the_cli_s_words(string name, string rule, string[] sentences)
    {
        using var document = JsonDocument.Parse(rule);
        var read = ReviewRules.Read(document.RootElement);

        Assert.True(read.Problem is null, $"{name}: {read.Problem}");
        Assert.Equal(sentences, ReviewRules.Says(read.Rule!));
    }

    public static TheoryData<string, string, string, string?, string?> EditRows() => Rows(
        "edits", row => (Cell(row, 0)!, Cell(row, 1)!, Cell(row, 2)!, Cell(row, 3), Cell(row, 4)));

    [Theory]
    [MemberData(nameof(EditRows))]
    public void A_review_edit_writes_what_the_cli_writes_or_refuses_in_its_words(
        string name, string file, string edit, string? after, string? refusal)
    {
        var config = DriverConfig.Parse(file);
        var change = Edit(edit);
        if (refusal is not null)
        {
            Assert.Equal(refusal, Assert.Throws<DriverException>(() => ReviewRules.Apply(config, change)).Message);
            return;
        }

        var written = JsonNode.Parse(ReviewRules.Apply(config, change).ToJson())!.AsObject();
        var maps = new JsonObject();
        foreach (var key in new[] { "reviews", "workspaceReviews" })
        {
            if (written[key] is { } map) maps[key] = map.DeepClone();
        }

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(after!), maps), $"{name}: {maps.ToJsonString()}");
    }

    public static TheoryData<string, string[], string, bool> ProcedureRows() => Rows(
        "procedures", row => (Cell(row, 0)!, row[1].EnumerateArray().Select(each => each.GetString()!).ToArray(), Cell(row, 2)!, row[3].GetBoolean()));

    [Theory]
    [MemberData(nameof(ProcedureRows))]
    public void A_checkout_holds_a_procedure_only_as_a_regular_file_at_that_path(string name, string[] files, string procedure, bool holds)
    {
        foreach (var file in files)
        {
            var path = Path.Combine(_root, file.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "# how\n");
        }

        Assert.True(holds == ReviewRules.Holds(_root, procedure), name);
    }

    /// <summary>The file a door writes is the file the table reads: written only once set, and read back the same.</summary>
    [Fact]
    public void A_rule_is_written_only_once_set_and_read_back_as_written()
    {
        Assert.DoesNotContain("reviews", DriverConfig.Empty.ToJson(), StringComparison.Ordinal);

        var config = ReviewRules.Apply(DriverConfig.Empty, new ReviewEdit
        {
            Repository = "storefront",
            Put = new ReviewSpelled("dev", "local", "README.md", "http://localhost:4200", "npm run serve"),
            Required = true,
        });
        config = ReviewRules.Apply(config, new ReviewEdit { Repository = "media-api", None = true });
        config = ReviewRules.Apply(config, new ReviewEdit { Workspace = "work", Put = new ReviewSpelled("dev", "deployed", "README.md") });

        var again = DriverConfig.Parse(config.ToJson());
        Assert.Equal(ReviewRules.ToJson(config.Reviews["storefront"]), ReviewRules.ToJson(again.Reviews["storefront"]));
        Assert.True(again.Reviews["MEDIA-API"].IsNone);
        Assert.Equal("""{"environments":[{"name":"dev","kind":"deployed","procedure":"README.md"}]}""", ReviewRules.ToJson(again.WorkspaceReviews["work"]));
    }

    /// <summary>
    /// The room's cell (design §1.7–§1.8): what the intake's and Ask Daoris's room shows of the rule standing for a repository,
    /// and <c>no review environment</c> where nothing is set.
    /// </summary>
    [Fact]
    public void The_room_names_each_environment_whether_work_waits_and_where_it_was_set()
    {
        Assert.Equal("no review environment", ReviewRules.RoomCell(null));
        Assert.Equal("no review environment (set for it)", ReviewRules.RoomCell(new ResolvedReview(ReviewRule.None, ReviewSource.Repository)));
        Assert.Equal(
            "`local` (local, `http://localhost:4200`, by `README.md`, runs `npm run serve`); `dev` (deployed, by `docs/deploying-to-dev.md`), "
            + "the first the default; work waits for the person's review before it lands (its workspace's rule)",
            ReviewRules.RoomCell(new ResolvedReview(
                new ReviewRule([new("local", "local", "README.md", "http://localhost:4200", "npm run serve"), new("dev", "deployed", "docs/deploying-to-dev.md")], Required: true),
                ReviewSource.Workspace)));
        Assert.Equal(
            "`dev` (deployed, by `README.md`), the first the default; shown when a task asks (set for it)",
            ReviewRules.RoomCell(new ResolvedReview(new ReviewRule([new("dev", "deployed", "README.md")]), ReviewSource.Repository)));
    }

    /// <summary>A put reaches the one repository it names, or each of its workspace's with a checkout here, matched in any case.</summary>
    [Fact]
    public void A_put_names_each_checkout_it_reaches_that_lacks_the_procedure()
    {
        Directory.CreateDirectory(Path.Combine(_root, "storefront"));
        File.WriteAllText(Path.Combine(_root, "storefront", "README.md"), "# how\n");
        Directory.CreateDirectory(Path.Combine(_root, "media-api"));
        (string, string?, string?)[] checkouts =
        [
            ("storefront", "work", Path.Combine(_root, "storefront")),
            ("media-api", "Work", Path.Combine(_root, "media-api")),
            ("elsewhere", "home", Path.Combine(_root, "elsewhere")),
            ("nowhere", "work", null),
        ];

        Assert.Equal(["media-api"], ReviewRules.Lacking(checkouts, new ReviewEdit { Workspace = "work" }, "README.md"));
        Assert.Empty(ReviewRules.Lacking(checkouts, new ReviewEdit { Repository = "StoreFront" }, "README.md"));
        Assert.Equal(["storefront"], ReviewRules.Lacking(checkouts, new ReviewEdit { Repository = "storefront" }, "docs/deploying-to-dev.md"));
    }

    /// <summary>An edit as the table spells it: each field read as the CLI's `applyReviewEdit` reads it, a field of another type absent.</summary>
    private static ReviewEdit Edit(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        string? Text(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        bool True(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

        return new ReviewEdit
        {
            Repository = Text(root, "repository"),
            Workspace = Text(root, "workspace"),
            Put = root.TryGetProperty("put", out var put) && put.ValueKind == JsonValueKind.Object
                ? new ReviewSpelled(Text(put, "name"), Text(put, "kind"), Text(put, "procedure"), Text(put, "address"), Text(put, "run"))
                : null,
            Required = root.TryGetProperty("required", out var required) && required.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? required.GetBoolean()
                : null,
            None = True("none"),
            Drop = Text(root, "drop"),
            Clear = True("clear"),
        };
    }

    private static bool Same(string? expected, string? actual) =>
        expected is null || actual is null ? expected == actual : JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual));

    private static string? Cell(JsonElement row, int at) => row[at].ValueKind == JsonValueKind.Null ? null : row[at].GetString();

    private static TheoryData<T1, T2, T3> Rows<T1, T2, T3>(string kind, Func<JsonElement, (T1, T2, T3)> cells)
    {
        var data = new TheoryData<T1, T2, T3>();
        foreach (var row in Table(kind)) data.Add(cells(row).Item1, cells(row).Item2, cells(row).Item3);
        return data;
    }

    private static TheoryData<T1, T2, T3, T4> Rows<T1, T2, T3, T4>(string kind, Func<JsonElement, (T1, T2, T3, T4)> cells)
    {
        var data = new TheoryData<T1, T2, T3, T4>();
        foreach (var row in Table(kind))
        {
            var (a, b, c, d) = cells(row);
            data.Add(a, b, c, d);
        }

        return data;
    }

    private static TheoryData<T1, T2, T3, T4, T5> Rows<T1, T2, T3, T4, T5>(string kind, Func<JsonElement, (T1, T2, T3, T4, T5)> cells)
    {
        var data = new TheoryData<T1, T2, T3, T4, T5>();
        foreach (var row in Table(kind))
        {
            var (a, b, c, d, e) = cells(row);
            data.Add(a, b, c, d, e);
        }

        return data;
    }

    private static TheoryData<T1, T2, T3, T4, T5, T6> Rows<T1, T2, T3, T4, T5, T6>(string kind, Func<JsonElement, (T1, T2, T3, T4, T5, T6)> cells)
    {
        var data = new TheoryData<T1, T2, T3, T4, T5, T6>();
        foreach (var row in Table(kind))
        {
            var (a, b, c, d, e, f) = cells(row);
            data.Add(a, b, c, d, e, f);
        }

        return data;
    }

    /// <summary>One kind of the shared table's rows, as the CLI's fixture holds them.</summary>
    private static IReadOnlyList<JsonElement> Table(string kind)
    {
        var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Cli", "test", "fixtures", "review-rules.json")));
        return [.. document.RootElement.GetProperty(kind).EnumerateArray().Select(row => row.Clone())];
    }
}
