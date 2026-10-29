using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1f part f1 (D207, the integration design §4.5, §5.3, §11 row f): how a red candidate's failures are judged, the driver's twin
/// of the merge queue's judge (<c>placeOnPrefixes</c>, <c>judgeFailure</c>, <c>candidateOutcome</c>, <c>confirmStep</c>,
/// <c>confirmReadings</c>, <c>memoryStep</c>, <c>recheckDue</c>, <c>noteRecheck</c> and the known red's standing). Held cell for cell to ONE
/// table, <c>fixtures/failure-judging.json</c>, which <c>src/Daoris.Cli/tool-test/integration-judge.test.ts</c> holds the tool to, row for
/// row and sentence for sentence, so the integration step Daoris runs gives the verdict the queue gives on the same runs. The runs, the
/// admission and the clock are values here: no process, no file. A sighting is never a pass.
/// </summary>
public sealed class FailureJudgeTests
{
    private static readonly JsonElement Table = JsonDocument.Parse(File.ReadAllText(Path.Combine(
        WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "failure-judging.json"))).RootElement;

    private static readonly IReadOnlyDictionary<string, FailureSignature> Signatures =
        Table.GetProperty("signatures").EnumerateObject().ToDictionary(each => each.Name, each => FailureSignature.FromJson(each.Value)!, StringComparer.Ordinal);

    private static TheoryData<int> Indexes(string section)
    {
        var data = new TheoryData<int>();
        for (var at = 0; at < Table.GetProperty(section).GetArrayLength(); at += 1) data.Add(at);
        return data;
    }

    public static TheoryData<int> PlacementRows() => Indexes("placements");

    public static TheoryData<int> JudgementRows() => Indexes("judgements");

    public static TheoryData<int> ReadingRows() => Indexes("readings");

    public static TheoryData<int> StepRows() => Indexes("steps");

    public static TheoryData<int> MemoryRows() => Indexes("memory");

    public static TheoryData<int, int> OutcomeRows()
    {
        var data = new TheoryData<int, int>();
        var rows = Table.GetProperty("outcomes");
        for (var at = 0; at < rows.GetArrayLength(); at += 1)
        {
            for (var policy = 0; policy < rows[at].GetProperty("policies").GetArrayLength(); policy += 1) data.Add(at, policy);
        }

        return data;
    }

    public static TheoryData<int> DueRows() => Rows(Table.GetProperty("rechecks").GetProperty("due"));

    public static TheoryData<int> NoteRows() => Rows(Table.GetProperty("rechecks").GetProperty("notes"));

    public static TheoryData<int> StandingRows() => Indexes("standing");

    private static TheoryData<int> Rows(JsonElement section)
    {
        var data = new TheoryData<int>();
        for (var at = 0; at < section.GetArrayLength(); at += 1) data.Add(at);
        return data;
    }

    private static string? Text(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static JsonNode? Node(JsonElement element) => JsonNode.Parse(element.GetRawText());

    private static JsonArray Strings(IEnumerable<string> values) => new([.. values.Select(value => (JsonNode?)JsonValue.Create(value))]);

    private static JsonArray Numbers(IEnumerable<int> values) => new([.. values.Select(value => (JsonNode?)JsonValue.Create(value))]);

    private static IReadOnlyList<int> Ints(JsonElement values) => [.. values.EnumerateArray().Select(each => each.GetInt32())];

    /// <summary>A failure as the table names it: by its signature's key; a signature that names a gate names no test.</summary>
    private static JudgedFailure FailureOf(string key)
    {
        var signature = Signatures[key];
        return signature.Gate is { } gate
            ? new JudgedFailure(FailureNames.Gate, gate, null, signature)
            : new JudgedFailure(FailureNames.Test, "driver", signature.Test, signature);
    }

    private static string NameOf(FailureSignature? signature) => Signatures.First(each => each.Value.Key == signature?.Key).Key;

    /// <summary>A prefix answer as the table spells it: <c>pass</c>, <c>fail:&lt;signature&gt;</c> or <c>cannot:&lt;signature&gt;</c>.</summary>
    private static PrefixOutcome Answer(string text)
    {
        if (text == "pass") return PrefixOutcome.Passed;
        var parts = text.Split(':');
        return parts[0] == "fail" ? PrefixOutcome.Failed(Signatures[parts[1]]) : PrefixOutcome.Cannot(Signatures[parts[1]]);
    }

    private static string Word(JudgeOutcome outcome) => outcome.ToString().ToLowerInvariant();

    private static string RunText(JudgeRun run)
    {
        var result = run.Outcome.Result.ToString().ToLowerInvariant();
        var signature = run.Outcome.Signature is null ? "" : ":" + NameOf(run.Outcome.Signature);
        return $"{(run.Alone ? "alone" : "")}{run.Prefix}:{result}{signature}";
    }

    [Theory]
    [MemberData(nameof(PlacementRows))]
    public void A_placement_on_the_prefixes_is_the_shared_tables(int at)
    {
        var row = Table.GetProperty("placements")[at];
        var placed = FailureJudge.PlaceOnPrefixes([.. row.GetProperty("seq").EnumerateArray().Select(each => Answer(each.GetString()!))], Signatures[Text(row, "signature")!]);
        var projected = new JsonObject
        {
            ["outcome"] = placed.Pinned ? "pinned" : "unresolved",
            ["member"] = placed.Pinned ? placed.Member : null,
            ["interacting"] = Numbers(placed.Interacting),
            ["monotonic"] = placed.Pinned ? placed.Monotonic : null,
            ["why"] = placed.Why,
        };
        Assert.True(JsonNode.DeepEquals(Node(row.GetProperty("expect")), projected), $"{Text(row, "name")}: {projected.ToJsonString()}");
    }

    private static ConfirmReading? ReadingOf(JsonElement confirm) => new(Text(confirm, "result"), Text(confirm, "why"));

    [Theory]
    [MemberData(nameof(JudgementRows))]
    public void A_failure_is_judged_as_the_shared_table_says(int at)
    {
        var row = Table.GetProperty("judgements")[at];
        var name = Text(row, "name");
        var seen = new Dictionary<int, int>();
        var confirmAsked = 0;
        var prefixes = row.GetProperty("prefixes");
        var second = row.TryGetProperty("second", out var overrides) ? overrides : default;
        var context = new JudgeContext(row.GetProperty("members").GetInt32(), (_, k) =>
        {
            seen[k] = seen.GetValueOrDefault(k) + 1;
            var again = seen[k] > 1 && second.ValueKind == JsonValueKind.Object && second.TryGetProperty(k.ToString(), out var replaced);
            var text = again ? second.GetProperty(k.ToString()).GetString() : k < prefixes.GetArrayLength() ? prefixes[k].GetString() : null;
            Assert.True(text is not null, $"{name}: the prefix {k} was run, and the table gives it no answer");
            return Answer(text!);
        })
        {
            Alone = row.TryGetProperty("alone", out var alone) ? (_, _) => Answer(alone.GetString()!) : null,
            Known = row.TryGetProperty("known", out var known) ? new KnownRed(known.GetProperty("expired").GetBoolean(), Text(known, "lastSeen")) : null,
            BaseCommit = Text(row, "baseCommit"),
            Confirm = row.TryGetProperty("confirm", out var confirm) ? () =>
            {
                confirmAsked += 1;
                return ReadingOf(confirm);
            }
            : null,
        };
        var judged = FailureJudge.JudgeFailure(FailureOf(Text(row, "failure")!), context);
        var projected = new JsonObject
        {
            ["outcome"] = Word(judged.Outcome),
            ["member"] = judged.Member,
            ["with"] = Numbers(judged.With),
            ["interacting"] = Numbers(judged.Interacting),
            ["why"] = judged.Why,
            ["said"] = judged.Said,
            ["runs"] = Strings(judged.Runs.Select(RunText)),
            ["confirmAsked"] = confirmAsked,
        };
        Assert.True(JsonNode.DeepEquals(Node(row.GetProperty("expect")), projected), $"{name}: {projected.ToJsonString()}");
    }

    private static QueueMember[] Members(int count) => [.. Enumerable.Range(1, count).Select(each => new QueueMember($"b{each}", $"t{each}".PadRight(40, '0')))];

    private static JudgeOutcome OutcomeOf(string word) => Enum.Parse<JudgeOutcome>(word, ignoreCase: true);

    [Theory]
    [MemberData(nameof(OutcomeRows))]
    public void A_candidates_outcome_is_the_shared_tables_under_each_policy(int at, int policyAt)
    {
        var row = Table.GetProperty("outcomes")[at];
        var chosen = row.GetProperty("policies")[policyAt];
        var policy = chosen.ValueKind == JsonValueKind.Null ? null : chosen.GetString();
        var judgements = row.GetProperty("judgements").EnumerateArray().Select(each =>
        {
            var judged = each.GetProperty("judged");
            var outcome = OutcomeOf(Text(judged, "outcome")!);
            return new JudgedEntry(FailureOf(Text(each, "failure")!), new Judged(outcome, [])
            {
                Member = judged.TryGetProperty("member", out var member) ? member.GetInt32() : null,
                With = judged.TryGetProperty("with", out var with) ? Ints(with) : [],
                Interacting = judged.TryGetProperty("interacting", out var interacting) ? Ints(interacting) : [],
                Why = Text(judged, "why"),
                Said = Text(judged, "said"),
                Confirm = new ConfirmReading(Word(outcome)),
            });
        }).ToList();
        var outcome = CandidateJudgement.Of(judgements, Members(row.GetProperty("members").GetInt32()), policy);
        var projected = outcome.Action == CandidateAction.Stop
            ? new JsonObject { ["action"] = "stop", ["failure"] = outcome.Failure!.Label, ["why"] = outcome.Why, ["said"] = outcome.Said }
            : new JsonObject
            {
                ["action"] = outcome.Action.Word(),
                ["retain"] = Strings(outcome.Retain.Select(each => each.Branch)),
                ["handedBack"] = new JsonArray([.. outcome.HandedBack.Select(each => (JsonNode?)new JsonObject
                {
                    ["branch"] = each.Member.Branch, ["how"] = each.How, ["failure"] = each.Failure, ["with"] = Strings(each.With), ["interacting"] = Strings(each.Interacting),
                    ["why"] = each.How == "unresolved" ? each.Why : null,
                })]),
                ["inherited"] = Strings(outcome.Inherited.Select(each => each.Label)),
                ["flakes"] = Strings(outcome.Flakes.Select(each => each.Label)),
                ["sightings"] = Strings(outcome.Sightings.Select(each => each.Failure.Label)),
                ["inconclusive"] = Strings(outcome.Inconclusive.Select(each => each.Label)),
            };
        if (outcome.Action == CandidateAction.Hold)
        {
            projected["on"] = outcome.On;
            projected["why"] = outcome.Why;
        }

        if (outcome.Action == CandidateAction.Land) projected["passedOnConfirm"] = Strings(outcome.PassedOnConfirm);
        Assert.True(JsonNode.DeepEquals(Node(row.GetProperty("expect")), projected), $"{Text(row, "name")} ({policy ?? "no policy"}): {projected.ToJsonString()}");
        // A sighting is never a pass: a candidate with one, or an inconclusive reading, is never green.
        if (judgements.Any(each => each.Judged.Outcome is JudgeOutcome.Unconfirmed or JudgeOutcome.Inconclusive)) Assert.NotEqual(CandidateAction.Green, outcome.Action);
    }

    [Theory]
    [MemberData(nameof(ReadingRows))]
    public void A_gates_failures_are_read_by_its_confirm_run_as_the_shared_table_says(int at)
    {
        var row = Table.GetProperty("readings")[at];
        ConfirmAnswer? confirm = null;
        if (row.GetProperty("confirm").ValueKind == JsonValueKind.Object)
        {
            var given = row.GetProperty("confirm");
            confirm = new ConfirmAnswer(Text(given, "result")!, Text(given, "why")!, null, null)
            {
                Failures = given.TryGetProperty("failures", out var failures) ? [.. failures.EnumerateArray().Select(each => FailureOf(each.GetString()!))] : null,
            };
        }

        var read = FailureJudge.ConfirmReadings(confirm, [.. row.GetProperty("failures").EnumerateArray().Select(each => FailureOf(each.GetString()!))]);
        var projected = new JsonArray([.. read.Select(each => (JsonNode?)new JsonObject { ["failure"] = each.Failure.Label, ["result"] = each.Confirm.Result, ["why"] = each.Confirm.Why })]);
        Assert.True(JsonNode.DeepEquals(Node(row.GetProperty("expect")), projected), $"{Text(row, "name")}: {projected.ToJsonString()}");
    }

    [Theory]
    [MemberData(nameof(StepRows))]
    public void A_confirm_run_is_taken_as_the_shared_table_says(int at)
    {
        var row = Table.GetProperty("steps")[at];
        var take = row.GetProperty("take");
        var run = row.GetProperty("run");
        var expect = row.GetProperty("expect");
        var halted = Text(row, "halted");
        bool taken = false, ran = false, ended = false;
        WholeTake Take(string what, Func<bool> stopped)
        {
            taken = true;
            if (!take.GetProperty("held").GetBoolean())
            {
                return new WholeTake(false, null, take.TryGetProperty("dropped", out var dropped) && dropped.GetBoolean(), Text(take, "why"), new WholeEvidence(false, []));
            }

            return new WholeTake(true, () =>
            {
                ended = true;
                return new WholeEvidence(take.TryGetProperty("conclusive", out var conclusive) && conclusive.GetBoolean(), Text(take, "doubt") is { } doubt ? [new WholeDoubt("beat", doubt)] : [], 1, ["p-1"]);
            });
        }

        GateRerun? Run()
        {
            ran = true;
            if (Text(run, "throws") is { } message) throw new InvalidOperationException(message);
            return new GateRerun(Text(run, "result"), Text(run, "why"))
            {
                Failures = run.TryGetProperty("failures", out var given)
                    ? [.. given.EnumerateArray().Select(each => each.ValueKind == JsonValueKind.Null ? null : FailureOf(each.GetString()!))]
                    : null,
                Composition = run.TryGetProperty("composition", out var composition)
                    ? new ConfirmComposition([.. composition.GetProperty("units").EnumerateArray().Select(each => each.GetString()!)], Text(composition, "digest"))
                    : null,
            };
        }

        bool Halted() => halted == "before" || (halted == "afterRun" && ran);
        if (Text(expect, "thrown") is { } thrownMessage)
        {
            var thrown = Assert.Throws<InvalidOperationException>(() => FailureJudge.ConfirmStep(Take, Run, Halted));
            Assert.Equal(thrownMessage, thrown.Message);
            Assert.Equal((expect.GetProperty("taken").GetBoolean(), expect.GetProperty("ran").GetBoolean(), expect.GetProperty("ended").GetBoolean()), (taken, ran, ended));
            return;
        }

        var answer = FailureJudge.ConfirmStep(Take, Run, Halted);
        var projected = new JsonObject
        {
            ["result"] = answer.Result,
            ["why"] = answer.Why,
            ["taken"] = taken,
            ["ran"] = ran,
            ["ended"] = ended,
            ["composition"] = answer.Composition is { } kept
                ? new JsonObject { ["units"] = kept.Units is null ? null : Strings(kept.Units), ["digest"] = kept.Digest }
                : null,
            ["failures"] = Strings((answer.Failures ?? []).Select(each => each.Label)),
            ["thrown"] = null,
            ["error"] = answer.Error?.Message,
        };
        Assert.True(JsonNode.DeepEquals(Node(expect), projected), $"{Text(row, "name")}: {projected.ToJsonString()}");
    }

    [Theory]
    [MemberData(nameof(MemoryRows))]
    public void A_death_of_memory_is_stepped_as_the_shared_table_says(int at)
    {
        var row = Table.GetProperty("memory")[at];
        var attempts = row.GetProperty("attempts").EnumerateArray()
            .Select(each => new MemoryAttempt(each.GetProperty("died").GetBoolean(), each.TryGetProperty("peakGb", out var peak) ? peak.GetDouble() : null)).ToList();
        var need = row.GetProperty("needGb");
        var step = FailureJudge.MemoryStep(attempts, row.GetProperty("afterHold").GetBoolean(), need.ValueKind == JsonValueKind.Null ? null : need.GetDouble());
        var projected = new JsonObject { ["next"] = step.Next, ["note"] = step.Note };
        Assert.True(JsonNode.DeepEquals(Node(row.GetProperty("expect")), projected), $"{Text(row, "name")}: {projected.ToJsonString()}");
    }

    private static DateTimeOffset At(string text) => DateTimeOffset.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

    private static QueueStop? StopOf(JsonElement stop) => stop.ValueKind != JsonValueKind.Object
        ? null
        : new QueueStop(Text(stop, "at")!, stop.TryGetProperty("rechecks", out var rechecks)
            ? [.. rechecks.EnumerateArray().Select(each => new QueueRecheck(Text(each, "at")!, each.TryGetProperty("passed", out var passed) && passed.GetBoolean(), Text(each, "by") ?? ""))]
            : []);

    [Theory]
    [MemberData(nameof(DueRows))]
    public void A_stopped_queue_is_due_its_recheck_as_the_shared_table_says(int at)
    {
        var row = Table.GetProperty("rechecks").GetProperty("due")[at];
        Assert.Equal((Text(row, "name"), row.GetProperty("due").GetBoolean()), (Text(row, "name"), FailureJudge.RecheckDue(StopOf(row.GetProperty("stop")), At(Text(row, "now")!))));
    }

    [Theory]
    [MemberData(nameof(NoteRows))]
    public void A_stop_rechecked_is_lifted_or_kept_as_the_shared_table_says(int at)
    {
        var row = Table.GetProperty("rechecks").GetProperty("notes")[at];
        var recheck = row.GetProperty("recheck");
        var noted = FailureJudge.NoteRecheck(StopOf(row.GetProperty("stop"))!, Text(recheck, "at")!, recheck.GetProperty("passed").GetBoolean(), Text(recheck, "by")!);
        JsonNode? projected = noted is null
            ? null
            : new JsonObject
            {
                ["at"] = noted.At,
                ["rechecks"] = new JsonArray([.. noted.Rechecks.Select(each => (JsonNode?)new JsonObject { ["at"] = each.At, ["passed"] = each.Passed, ["by"] = each.By })]),
            };
        Assert.True(JsonNode.DeepEquals(Node(row.GetProperty("expect")), projected), $"{Text(row, "name")}: {projected?.ToJsonString()}");
    }

    [Theory]
    [MemberData(nameof(StandingRows))]
    public void A_known_reds_standing_is_the_shared_tables(int at)
    {
        var row = Table.GetProperty("standing")[at];
        var standing = FailureJudge.StandingOf(Text(row, "since"), At(Text(row, "now")!));
        Assert.Equal((Text(row, "name"), row.GetProperty("expired").GetBoolean(), row.GetProperty("warn").GetBoolean()), (Text(row, "name"), standing.Expired, standing.Warn));
    }

    [Fact]
    public void A_stop_whose_time_does_not_read_is_never_due_and_a_first_sighting_that_does_not_read_never_expires()
    {
        Assert.False(FailureJudge.RecheckDue(new QueueStop("soon", []), DateTimeOffset.UnixEpoch.AddYears(60)));
        Assert.Equal(new KnownRedStanding(false, false), FailureJudge.StandingOf("sometime", DateTimeOffset.UnixEpoch.AddYears(60)));
        Assert.Equal(new KnownRedStanding(false, false), FailureJudge.StandingOf(null, DateTimeOffset.UnixEpoch.AddYears(60)));
    }

    [Fact]
    public void The_table_holds_its_rows_the_known_red_expiry_and_a_sighting_among_them()
    {
        Assert.True(Table.GetProperty("judgements").GetArrayLength() >= 25 && Table.GetProperty("outcomes").GetArrayLength() >= 20 && Table.GetProperty("steps").GetArrayLength() >= 14);
        var judgements = Table.GetProperty("judgements").EnumerateArray().ToList();
        Assert.Contains(judgements, row => row.TryGetProperty("known", out var known) && known.GetProperty("expired").GetBoolean() && row.GetProperty("expect").GetProperty("outcome").GetString() == "stop");
        Assert.Contains(judgements, row => row.TryGetProperty("confirm", out var confirm) && Text(confirm, "result") == "unconfirmed");
    }
}
