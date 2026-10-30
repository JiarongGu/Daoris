using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-i part 1 (D212; the foundation design §10's row 1f-i): the move record's readers, each a row's adoption. Until row f-ii none
/// but the one wiring through which the workplace's delete takes a record's moves with it; since FOUNDATION1f-ii part a the conversations'
/// holder and the live limit move, which writes its move through it, and since FOUNDATION1f-iv part a the driven carry-on's record and the
/// driver that runs it. A source that names a type the move store's files declare is a row's
/// adoption, and moves this check.
/// </summary>
/// <remarks>Not a twin of <see cref="CourseVocabularyTests"/>' check of the course's vocabulary (<c>twins.md</c>): it shares that check's shape and reads other files.</remarks>
public sealed class MoveStoreCensusTests
{
    private static string Driver => Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver");

    /// <summary>
    /// The move store's own files, which declare its types: since part 2 the take-up of dead runs' moves and the box importers too, which
    /// nothing calls either; since part 3 the moved words' delivery, a hold's one rule and the door to a destination's <c>said</c>.
    /// </summary>
    private static readonly string[] Own =
    [
        "Course/MoveStore.cs", "Course/MoveRecords.cs", "Course/MoveTakeUp.cs", "Course/MoveBoxImports.cs", "Course/MoveDelivery.cs",
        "Course/MoveResolve.cs", "Course/MoveSaid.cs",
    ];

    /// <summary>The importers, which only rows f-v and f-vi may call: importing a box sets it aside from today's readers, which still own it.</summary>
    private static readonly string[] Importers = ["LimitBoxImport", "HandOverBoxImport", "MoveBoxImport"];

    /// <summary>The workplace's delete port, opened over a home: the first production reader, row f-i's.</summary>
    private const string Wiring = "Course/WorkplaceMoveRecordsWiring.cs";

    /// <summary>The conversations' holder (FOUNDATION1f-ii part a): the store, the said door and the take-up a shell's conversation moves go through.</summary>
    private const string Conversations = "Course/ConversationMoves.cs";

    /// <summary>The live limit move (FOUNDATION1f-ii part a), which decides its move through the holder.</summary>
    private const string LiveMove = "Course/Chat/LimitMove.cs";

    /// <summary>The curve's move (FOUNDATION1f-iii part a), which decides its move through the holder as the live limit move does.</summary>
    private const string CurveMove = "Course/Chat/CurveMove.cs";

    /// <summary>The driven carry-on's record (FOUNDATION1f-iv part a), which decides its move through the same holder.</summary>
    private const string CarryOn = "Loop/CarryOnRecord.cs";

    /// <summary>The driver, which builds the driven holder over the lease holder's scope and runs the carry-on's record.</summary>
    private const string DrivenLine = "Loop/Driver.cs";

    private static string Relative(string path) => Path.GetRelativePath(Driver, path).Replace('\\', '/');

    [Fact]
    public void The_move_store_s_production_readers_are_the_workplace_s_delete_the_conversations_holder_and_the_live_limit_and_curve_moves()
    {
        var names = Own
            .SelectMany(file => Regex.Matches(
                    CourseCensusTests.WithoutComments(File.ReadAllText(Path.Combine(Driver, file))),
                    @"^public\s+(?:abstract\s+|sealed\s+|static\s+|readonly\s+)*(?:record(?:\s+struct)?|enum|class|struct|interface)\s+(\w+)",
                    RegexOptions.Multiline)
                .Select(match => match.Groups[1].Value))
            .ToList();
        Assert.True(names.Count >= 10, $"found {names.Count} types: the pattern has drifted");
        Assert.Contains("MoveStore", names);

        var readers = new List<string>();
        foreach (var path in CourseCensusTests.ProductionFiles())
        {
            if (Own.Contains(Relative(path))) continue;
            var text = CourseCensusTests.WithoutComments(File.ReadAllText(path));
            readers.AddRange(names.Where(name => Regex.IsMatch(text, $@"\b{name}\b")).Select(name => $"{Relative(path)} names {name}"));
        }

        Assert.Equal(
            [CurveMove, LiveMove, Conversations, Wiring, CarryOn, DrivenLine],
            readers.Select(reader => reader[..reader.IndexOf(" names ", StringComparison.Ordinal)]).Distinct().Order(StringComparer.Ordinal));
        Assert.Contains($"{Wiring} names MoveStore", readers);
        Assert.All(
            ["MoveStore", "MoveTakeUp", "MoveTakeUpScope", "MoveDelivery", "MoveResolve", "IMoveSaid", "MoveSaidDoor"],
            used => Assert.Contains($"{Conversations} names {used}", readers));

        // The live moves, the limit's and the curve's, and the driven carry-on's record name the decision alone: every write of their
        // records goes through the holder's claim.
        foreach (var move in new[] { LiveMove, CurveMove, CarryOn })
        {
            Assert.Equal(
                [$"{move} names MoveDecision", $"{move} names MoveExecutors", $"{move} names MoveHows"],
                readers.Where(reader => reader.StartsWith(move + " ", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
        }

        // The take of the curve's move writes its phases through the holder's move, never the store: it names no type of the store's.
        Assert.DoesNotContain(readers, reader => reader.StartsWith("Course/Chat/ChatHandOver.cs ", StringComparison.Ordinal));

        // The driver names only the scope its driven holder is built with (FOUNDATION1f-iv part a); its take-up (part b, below) goes through
        // the holder, and the watch names no type of the move record at all.
        Assert.Equal([$"{DrivenLine} names MoveTakeUpScope"], readers.Where(reader => reader.StartsWith(DrivenLine + " ", StringComparison.Ordinal)));
        Assert.Contains("MoveTakeUp", names);
        Assert.All(Importers, importer => Assert.Contains(importer, names));
        Assert.All(["MoveDelivery", "MoveResolve", "IMoveSaid", "MoveSaidDoor", "MoveWordDispositions"], part3 => Assert.Contains(part3, names));
    }

    /// <summary>
    /// FOUNDATION1f-ii part a (the foundation design §3.3): the conversations' take-up runs at the shell's come-up before anything else that
    /// could act on their records, the limit boxes' take-up first among them, and at each look of the shell beside the limits' wake and before
    /// it, the shell's one periodic place for its conversations; so a dead shell's move is taken up and a live one's quiet is said.
    /// </summary>
    [Fact]
    public void The_conversations_take_up_runs_at_the_shell_s_come_up_and_at_each_of_its_looks_before_what_acts_on_their_records()
    {
        var goOn = CourseCensusTests.WithoutComments(File.ReadAllText(Path.Combine(Driver, "Course", "Chat", "ChatGoOn.cs")));
        var comeUp = goOn[goOn.IndexOf("Task<IReadOnlyList<ChatStart>> TakeUpAsync(", StringComparison.Ordinal)..];
        Assert.Matches(@"^[^;]*\{\s*await _limit\.Moves\.ComeUpAsync\(", comeUp);
        Assert.True(comeUp.IndexOf("Moves.ComeUpAsync(", StringComparison.Ordinal) < comeUp.IndexOf("TakeUpBoxesAsync(", StringComparison.Ordinal));

        var loop = CourseCensusTests.WithoutComments(File.ReadAllText(
            Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Modules", "Loop", "DriverLoop.cs")));
        Assert.Matches(@"new ConversationMoves\(homeDirectory, service, Events, Output\)", loop);
        Assert.Matches(@"Limits = async \(looked, token\) =>\s*\{\s*await moves\.LookAsync\(token\)[^;]*;\s*await chat\.WakeLimitsAsync\(", loop);
    }

    /// <summary>
    /// FOUNDATION1f-iv part b (the foundation design §3.3): a driven line's take-up runs at the watch's first look, at the slot where uncertain
    /// words are settled and before it, after the orphan sweep and the supervisor's, through the driver's own holder (<c>Driver.Moves</c>, whose
    /// scope is the lease holder's), so the watch names no type of the move record and the census of readers gains none.
    /// </summary>
    [Fact]
    public void The_driven_take_up_runs_at_the_watch_s_first_look_through_the_drivers_holder_before_uncertain_words_are_settled()
    {
        var watch = CourseCensusTests.WithoutComments(File.ReadAllText(Path.Combine(Driver, "Loop", "DriverWatch.cs")));
        var sweep = watch.IndexOf("Supervisor.SweepAtStart(", StringComparison.Ordinal);
        var take = watch.IndexOf(".TakeUpMovesAsync(ct)", StringComparison.Ordinal);
        var uncertain = watch.IndexOf("UncertainWords.SettleAsync(", StringComparison.Ordinal);
        Assert.True(sweep > 0 && take > sweep && uncertain > take, $"sweep {sweep}, take-up {take}, uncertain {uncertain}");
        Assert.Matches(@"Stages\.TimeAsync\(\s*""moves"",\s*\(\)\s*=>\s*new Driver\([^;]*\)\s*\{\s*Log = Log\s*\}\.TakeUpMovesAsync\(ct\)\)", watch);

        var driver = CourseCensusTests.WithoutComments(File.ReadAllText(Path.Combine(Driver, "Loop", "Driver.cs")));
        Assert.Matches(@"internal async Task TakeUpMovesAsync\(CancellationToken ct\) => await Moves\.LookAsync\(ct\)", driver);
    }

    /// <summary>
    /// FOUNDATION1f-i part 2: no production source names an importer, the take-up's own file included, which takes whatever importers its
    /// caller hands it and is handed none yet; the box files' readers name no move type, so today's owners of the boxes read them as before.
    /// The limit box's reader, <c>LimitMove.cs</c>, left that list at FOUNDATION1f-ii part a, its live move writing its record through the
    /// conversations' holder; its ended move and its box are read as before until row f-vi.
    /// </summary>
    [Fact]
    public void No_production_source_calls_a_box_importer_and_the_boxes_readers_name_no_move_type()
    {
        var callers = new List<string>();
        foreach (var path in CourseCensusTests.ProductionFiles())
        {
            if (Relative(path) == "Course/MoveBoxImports.cs") continue;
            var text = CourseCensusTests.WithoutComments(File.ReadAllText(path));
            callers.AddRange(Importers.Where(name => Regex.IsMatch(text, $@"\b{name}\b")).Select(name => $"{Relative(path)} names {name}"));
        }

        Assert.Empty(callers);
        foreach (var reader in new[] { "Course/Chat/LimitBox.cs", "Course/HandOver/HandOverFiles.cs", "Course/HandOver/HandOver.cs" })
        {
            Assert.DoesNotMatch(@"\bMove(Store|Record|TakeUp|BoxImport|FromBox|Imported)\b", CourseCensusTests.WithoutComments(File.ReadAllText(Path.Combine(Driver, reader))));
        }
    }

    [Fact]
    public void The_delete_reaches_a_record_s_moves_through_the_workplace_s_port_which_names_no_course_type()
    {
        Assert.IsType<WorkplaceMoveRecordStoreOperations>(WorkplaceMoveRecords.Current);
        var home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "move-store-census", Guid.NewGuid().ToString("N")[..8]);
        var opened = WorkplaceMoveRecords.Current.Open(home);
        Assert.IsType<MoveStore>(opened);
        Assert.Equal(DesktopStore.LocationIn(home), opened.StoreLocation);
        Assert.Empty(opened.MovesOf(["s1"]));
        Assert.Empty(opened.ForgetMoves(["s1"]));
        Assert.False(Directory.Exists(home));

        var port = File.ReadAllText(Path.Combine(Driver, "Workplace", "WorkplaceMoveRecords.cs"));
        Assert.DoesNotMatch(@"\bMove(Store|Record|Claim|Decision)\b", CourseCensusTests.WithoutComments(port));
        Assert.Contains("WorkplaceMoveRecords.Current.Open(home)", File.ReadAllText(Path.Combine(Driver, "Workplace", "SessionHomeFiles.cs")));
    }
}
