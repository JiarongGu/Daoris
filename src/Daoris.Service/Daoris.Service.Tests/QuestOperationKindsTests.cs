using System.Text.Json;
using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// QUESTOP1: a kind of quest operation is not one enum value. It is placed in each of the places
/// <c>.claude/knowledge/quest-operations.md</c> names: the replay's rule, the log's payload and the cache, the wire and
/// its shape sentence, the rebase's rule. EVID1a's <c>evidenced</c> went into eight of them, and nothing tied them
/// together. Every theory here runs over every value of <see cref="QuestOperationKind"/>, so a kind added to the enum and
/// missed in one place fails here, naming the place.
/// </summary>
public sealed class QuestOperationKindsTests
{
    public static TheoryData<QuestOperationKind> Kinds() => new(Enum.GetValues<QuestOperationKind>());

    private const string Guide = ".claude/knowledge/quest-operations.md";
    private const string Id = "abcdefabcdef";
    private const string Commit = "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-07T09:00:00Z");

    private static DateTimeOffset At(long sequence) => Now.AddMinutes(sequence);

    /// <summary>
    /// The quest as asked: a requirement naming evidence, so a done can wait on it, a set-up step's environment, so a set-up
    /// and a review's verdict have a quest to apply to, and all else a publish carries.
    /// </summary>
    private static readonly Quest Asked = new(Id, "ask #a1b2c3", "reports", "Write the report", "Through the bridge.", QuestStatus.Open, null, Now, Now)
    {
        Links = ["https://tickets.example/T-1"],
        Attachments = [new QuestAttachment("trace.log", new string('a', 64), 300)],
        Then = [new QuestStep("checker", "Verify {parent}", "Open it and look.") { SetUpIn = "dev" }],
        Review = new QuestReview("local", "run it locally against dev data"),
        SetUpIn = "local",
        PublishedBy = "s1a2b3c4",
        Lanes = ["web"],
        Requirements =
        [
            new QuestRequirement("write the report", "The report is in docs/report.md.")
            {
                Evidence = [new QuestEvidence("docs/report.md")],
            },
        ],
        Short = "The report",
    };

    private static QuestOperation Op(QuestOperationKind kind, long sequence, string machine = "m1") =>
        new(Id, kind, machine, sequence, At(sequence));

    /// <summary>One operation of each kind, as its verb makes it: every field the kind carries, set.</summary>
    private static readonly Dictionary<QuestOperationKind, Func<long, QuestOperation>> Samples = new()
    {
        [QuestOperationKind.Published] = s => Op(QuestOperationKind.Published, s) with
        {
            Published = Asked with { Filed = At(s), Updated = At(s) },
        },
        [QuestOperationKind.Taken] = s => Op(QuestOperationKind.Taken, s) with { Note = "The owner's session." },
        [QuestOperationKind.Done] = s => Op(QuestOperationKind.Done, s) with
        {
            Note = "Built.", Answers = [new QuestAnswer(1, Met: "It is in docs/report.md.")],
        },
        [QuestOperationKind.Declined] = s => Op(QuestOperationKind.Declined, s) with { Note = "Abandoned.", WhileOpen = true },
        [QuestOperationKind.Conflict] = s => Op(QuestOperationKind.Conflict, s, "m2") with
        {
            Note = "Another machine's take.", Attempted = QuestStatus.Taken,
        },
        [QuestOperationKind.Dismissed] = s => Op(QuestOperationKind.Dismissed, s) with { Dismisses = new QuestOperationRef("m2", 7) },
        [QuestOperationKind.Waited] = s => Op(QuestOperationKind.Waited, s) with { Note = "fedcbafedcba" },
        [QuestOperationKind.Deleted] = s => Op(QuestOperationKind.Deleted, s),
        [QuestOperationKind.Accepted] = s => Op(QuestOperationKind.Accepted, s),
        [QuestOperationKind.Evidenced] = s => Op(QuestOperationKind.Evidenced, s) with
        {
            Evidence = new QuestEvidenceVerdict(Commit, "session-end",
            [
                new QuestEvidenceRead(1, "docs/report.md", null, "found")
                {
                    Object = "fedcba9876543210fedcba9876543210fedcba98", Changed = true,
                },
            ])
            { Session = "s1a2b3c4" },
        },
        // A deployed set-up, so its address crosses as given: a local one leaves it behind on the wire, which
        // ReviewStepTests holds.
        [QuestOperationKind.SetUp] = s => Op(QuestOperationKind.SetUp, s) with
        {
            SetUp = new QuestSetUp(Commit)
            {
                Look = "https://dev.example.test/reports/7", Shows = "The report with the new setting on.",
                Again = "Open https://dev.example.test/reports/7 and turn on the setting.", Served = "dist/app",
                Run = "npm run serve:dev", Session = "s1a2b3c4",
            },
        },
        // The person's reviewed, on the set-up the states below show at sequence 4.
        [QuestOperationKind.Verdict] = s => Op(QuestOperationKind.Verdict, s) with
        {
            Verdict = new QuestReviewVerdict(Reviews.Reviewed)
            {
                SetUp = new QuestOperationRef("m1", 4), Commit = Commit, Words = "That is the setting.",
            },
        },
    };

    /// <summary>The kind's sample, or a failure saying where a new kind goes before it is given one.</summary>
    private static QuestOperation Sample(QuestOperationKind kind, long sequence)
    {
        Assert.True(Samples.TryGetValue(kind, out var made),
            $"No sample of `{kind}` here. A new kind goes in every place {Guide} names; then it is given a sample here, as its verb makes it.");
        return made!(sequence);
    }

    /// <summary>The histories each kind is tried on: no quest, then a quest at each point of its life.</summary>
    private static IReadOnlyList<(string State, IReadOnlyList<QuestOperation> History)> States()
    {
        var published = Sample(QuestOperationKind.Published, 1);
        var taken = Sample(QuestOperationKind.Taken, 2);
        return
        [
            ("no quest", []),
            ("open", [published]),
            ("taken", [published, taken]),
            ("done, waiting on its evidence", [published, taken, Sample(QuestOperationKind.Done, 3)]),
            ("done, departed", [published, taken, Departure()]),
            ("done, shown for review", [published, taken, Departure(), Sample(QuestOperationKind.SetUp, 4)]),
            ("declined", [published, Sample(QuestOperationKind.Declined, 2) with { WhileOpen = false }]),
        ];
    }

    /// <summary>A done departing from its requirement: held for the person's yes, and for a set-up step its review after it.</summary>
    private static QuestOperation Departure() => Sample(QuestOperationKind.Done, 3) with
    {
        Answers = [new QuestAnswer(1, null, Departed: "It went into the wiki instead.", Quote: "write the report")],
    };

    /// <summary>The histories the kind's sample applies to, each with the sample as it would follow them.</summary>
    private static IReadOnlyList<(string State, IReadOnlyList<QuestOperation> History, QuestOperation Operation)> Applying(
        QuestOperationKind kind) =>
        [.. States()
            .Select(each => (each.State, each.History, Operation: Sample(kind, each.History.Count + 1)))
            .Where(each => QuestLog.Applies(QuestLog.Replay(each.History), each.Operation))];

    private static readonly JsonSerializerOptions Whole = new() { IncludeFields = true };

    /// <summary>
    /// Everything a reader can see of a value, as one comparable string: a record's own equality compares its lists by
    /// reference, and this reads every property, so a field a new kind adds is compared without being named here.
    /// </summary>
    private static string Spelled(object value) => JsonSerializer.Serialize(value, value.GetType(), Whole);

    // ——— The replay's rule: QuestLog.Applies and QuestLog.Step, through QuestTransitions.

    /// <summary>
    /// Every kind applies to some quest, steps it without throwing, and moves its status only as the transition table
    /// says. A kind <see cref="QuestLog.Applies"/> has no rule for applies nowhere, so a remote and a rebase refuse it on
    /// every machine; one <see cref="QuestLog.Step"/> has no rule for is stepped as a move to a status it has none of.
    /// </summary>
    [Theory]
    [MemberData(nameof(Kinds))]
    public void The_replay_has_a_rule_for_every_kind_and_moves_status_only_through_the_table(QuestOperationKind kind)
    {
        var applying = Applying(kind);
        Assert.True(applying.Count > 0,
            $"`{kind}` applies to no quest: QuestLog.Applies has no rule for it ({Guide}, the replay's rule).");

        foreach (var (state, history, operation) in applying)
        {
            var before = QuestLog.Replay(history);
            Quest? after = null;
            var stepped = Record.Exception(() => after = QuestLog.Step(before, operation));
            Assert.True(stepped is null,
                $"`{kind}` on a quest {state}: QuestLog.Step has no rule for it ({stepped?.GetType().Name}; {Guide}, the replay's rule).");

            // A publish begins a quest and a delete ends one; every other kind moves its status only through the table.
            if (before is null || after is null) continue;
            Assert.Equal(QuestTransitions.Target(kind) ?? before.Status, after.Status);
        }
    }

    // ——— The wire: QuestWire.Write, QuestWire.Read and QuestWire.Shape.

    /// <summary>
    /// Every kind crosses the wire as it was made, both ways: a push, and a fetched page carrying its number. A field the
    /// kind carries that <see cref="QuestWire"/> does not write, or does not read back, is lost on the first sync.
    /// </summary>
    [Theory]
    [MemberData(nameof(Kinds))]
    public void Every_kind_crosses_the_wire_as_it_was_made(QuestOperationKind kind)
    {
        var operation = Sample(kind, 1);

        var pushed = QuestWire.ReadPush(QuestWire.Push(0, [operation]));
        Assert.True(pushed is not null, $"`{kind}` is not whole on the wire: QuestWire's reader refuses what its writer wrote ({Guide}, the wire).");
        Assert.Equal(Spelled(operation), Spelled(Assert.Single(pushed.Value.Operations)));

        var numbered = operation with { Number = 4 };
        var fetched = QuestWire.ReadPage(QuestWire.Page(new QuestFetch([numbered], 4, More: false)));
        Assert.Equal(Spelled(numbered), Spelled(Assert.Single(fetched!.Operations)));
    }

    /// <summary>
    /// The word <see cref="QuestWire.Shape"/> says each kind's own fields with. A kind that carries nothing beyond what
    /// every operation names has none. A rewording of the sentence keeps each word, or changes it here too.
    /// </summary>
    private static readonly Dictionary<QuestOperationKind, string> ShapeWords = new()
    {
        [QuestOperationKind.Published] = "publish",
        [QuestOperationKind.Done] = "done",
        [QuestOperationKind.Declined] = "decline",
        [QuestOperationKind.Conflict] = "conflict",
        [QuestOperationKind.Dismissed] = "dismissal",
        [QuestOperationKind.Evidenced] = "evidenced",
        [QuestOperationKind.SetUp] = "a set-up names",
        [QuestOperationKind.Verdict] = "a review's verdict",
    };

    /// <summary>
    /// What a kind requires on the wire, the shape sentence says: a remote answers a push that is not whole with that
    /// sentence alone, so a kind refused with only the fields every operation names, and not named in it, is refused
    /// with a sentence that does not say why.
    /// </summary>
    [Theory]
    [MemberData(nameof(Kinds))]
    public void What_a_kind_requires_on_the_wire_its_shape_sentence_says(QuestOperationKind kind)
    {
        var bare = $$"""
            { "base": 0, "operations": [{ "machine": "m1", "sequence": 1, "quest": "{{Id}}",
              "kind": "{{kind.ToString().ToLowerInvariant()}}", "at": "2026-10-07T09:00:00Z" }] }
            """;
        var requiresMore = QuestWire.ReadPush(bare) is null;

        if (ShapeWords.TryGetValue(kind, out var word))
        {
            Assert.True(QuestWire.Shape.Contains(word, StringComparison.Ordinal),
                $"QuestWire.Shape no longer says `{word}` for `{kind}` ({Guide}, the wire's shape sentence).");
        }
        else
        {
            Assert.False(requiresMore,
                $"`{kind}` requires more than every operation names, and QuestWire.Shape does not say what ({Guide}, the wire's shape sentence).");
        }
    }

    // ——— The log and the cache: QuestStore's payload, its reader, and the row the replay writes.

    /// <summary>
    /// Every kind is kept in the log and read back as it was made, and the quest's cached row is what its history
    /// replays to, every property of it. A field the payload does not write is lost when the store reads the history
    /// back; a property a kind's step sets that the cache does not keep is missing from every list.
    /// </summary>
    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task Every_kind_is_kept_in_the_log_and_the_cache_is_its_replay(QuestOperationKind kind)
    {
        var applying = Applying(kind);
        Assert.True(applying.Count > 0, $"`{kind}` applies to no quest ({Guide}, the replay's rule).");
        var (_, history, operation) = applying[0];
        IReadOnlyList<QuestOperation> made = [.. history, operation];

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var store = await QuestStore.OpenAsync(connection);

        // Kept as another machine's, numbered, as a fetch keeps them: through the payload and back.
        await store.IntegrateAsync(
            Workspaces.Default, [.. made.Select((each, index) => each with { Number = index + 1 })], made.Count);

        var kept = await store.HistoryAsync(Id);
        Assert.Equal(made.Count, kept.Count);
        foreach (var (was, read) in made.Zip(kept)) Assert.Equal(Spelled(was), Spelled(read with { Number = null }));
        var replayed = QuestLog.Replay(made);
        var cached = await store.FindAsync(Id);
        Assert.Equal(replayed is null ? null : Spelled(replayed), cached is null ? null : Spelled(cached));
    }

    // ——— The rebase's rule: QuestLog.Lost, which QuestStore.RebaseAsync reads.

    /// <summary>
    /// Every kind has a rebase rule, and the conflict a rebase makes of a move crosses the wire. A kind with no rule
    /// once fell through to a conflict naming no attempted status, which a remote refused, and every later push of
    /// its circle with it (QUESTOP1, the fix log).
    /// </summary>
    [Theory]
    [MemberData(nameof(Kinds))]
    public void The_rebase_has_a_rule_for_every_kind_and_makes_a_conflict_only_of_a_move(QuestOperationKind kind)
    {
        var loss = QuestLoss.Kept;
        var ruled = Record.Exception(() => loss = QuestLog.Lost(kind));
        Assert.True(ruled is null, $"A rebase has no rule for `{kind}`: QuestLog.Lost ({Guide}, the rebase's rule).");

        if (loss != QuestLoss.Conflict) return;
        var lost = Sample(kind, 1) with { Kind = QuestOperationKind.Conflict, Attempted = QuestTransitions.Target(kind), WhileOpen = false };
        Assert.True(QuestWire.ReadPush(QuestWire.Push(0, [lost])) is not null,
            $"A rebase makes a conflict of `{kind}` that the wire refuses: only a move, which names the status it attempted, becomes one ({Guide}, the rebase's rule).");
    }
}
