using Daoris.Desktop.Driver.Tests;

namespace Daoris.Driver.Tests;

/// <summary>
/// A subagent's own record is never read as its session's words: no reader of a session's record finds it, no enumeration of the
/// sessions folder names it, and only the readers named here open it. And every reader that walks all of a session's events reads the
/// two kinds of what it ran beside itself, <c>child</c> and <c>task</c>, as no word of the session's.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class ChildRecordsCensusTests : IDisposable
{
    private const string ChildWords = "CHILDWORDS only the subagent said";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-child-census-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly SessionEvents _events;

    public ChildRecordsCensusTests()
    {
        Directory.CreateDirectory(_home);
        _events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
    }

    public void Dispose() => Directory.Delete(_home, recursive: true);

    /// <summary>The files beside its keeper that may open a subagent's own record: the record's part of a run's sink, the two roots that wire
    /// it, and the session record that reads a child's calls and forgets it with its session.</summary>
    private static readonly string[] Readers =
    [
        "Context/Events/RecordReadings.cs", "Loop/Chat/ChatWatch.cs", "Loop/Run/Hold/SessionHold.cs", "Context/Events/SessionEventLog.cs",
    ];

    [Fact]
    public void Only_the_named_readers_open_a_subagent_s_own_record()
    {
        var driver = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver");
        var opening = SourceFiles.Under(driver, "*.cs")
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => SourceTexts.Of(file) is var text
                && (text.Contains("new ChildRecords(", StringComparison.Ordinal) || text.Contains("ChildRecords.Folder", StringComparison.Ordinal)
                    || text.Contains("ChildRecords? ", StringComparison.Ordinal)))
            .Select(file => Path.GetRelativePath(driver, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(Readers.Order(StringComparer.Ordinal), opening);
    }

    [Fact]
    public void No_reader_of_a_session_s_words_finds_its_subagent_s_own_record()
    {
        _events.Append("s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "split the work" });
        _events.Append("s1", new SessionEvent { Kind = SessionEventKind.Tool, Id = "toolu_ag", Title = "Agent", ToolKind = "think", Status = "in_progress" });
        _events.Append("s1", new SessionEvent { Kind = SessionEventKind.Child, Id = "a1", Call = "toolu_ag", Depth = 1, Name = "Reader", By = "reported" });
        _events.Append("s1", new SessionEvent { Kind = SessionEventKind.Message, Text = "The session's own answer." });
        _events.Append("s1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
        var children = new ChildRecords(DaorisHome.Entry(_home, HomeEntry.Sessions));
        children.Keep("s1", "a1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = ChildWords });
        children.Keep("s1", "a1", new SessionEvent { Kind = SessionEventKind.Message, Text = ChildWords });
        children.Keep("s1", "a1", new SessionEvent { Kind = SessionEventKind.Tool, Id = "toolu_child", Title = ChildWords });

        Assert.Equal(3, children.Read("s1", "a1")!.Count);
        Assert.Equal("The session's own answer.", _events.LastSaid("s1"));
        Assert.DoesNotContain(_events.Spoken("s1").Said, said => said.Text.Contains("CHILDWORDS", StringComparison.Ordinal));
        Assert.Empty(_events.Search("CHILDWORDS").Hits);
        Assert.Empty(_events.Within("s1", "CHILDWORDS").Hits);
        Assert.Null(_events.FirstFound("s1", "CHILDWORDS"));
        Assert.Equal("split the work", _events.FirstAsked("s1"));
        Assert.DoesNotContain(_events.Page("s1").Events, e => e.Id == "toolu_child" || (e.Text ?? "").Contains("CHILDWORDS", StringComparison.Ordinal));
        // The sessions folder names one session, never the child.
        Assert.Equal(["s1"], _events.Search("own answer").Hits.Select(hit => hit.Session));

        Assert.True(_events.Forget("s1"));
        Assert.Null(children.Read("s1", "a1"));
    }

    [Fact]
    public void Every_reader_that_walks_a_session_s_events_reads_a_child_s_and_a_task_s_lifetime_as_no_word_of_the_session_s()
    {
        _events.Append("s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "run the gates" });
        _events.Append("s1", new SessionEvent { Kind = SessionEventKind.Tool, Id = "toolu_gates", Title = "npm run gates", Status = "completed" });
        _events.Append("s1", new SessionEvent { Kind = SessionEventKind.Task, Id = "b1", Call = "toolu_gates", Name = "npm run gates", By = "reported" });
        _events.Append("s1", new SessionEvent { Kind = SessionEventKind.Message, Text = "The gates are " });
        // A task's or a child's end lands while the agent is still writing: its words stay one message.
        _events.Append("s1", new SessionEvent { Kind = SessionEventKind.Task, Id = "b1", End = ChildEnds.Failed, By = "reported" });
        _events.Append("s1", new SessionEvent { Kind = SessionEventKind.Child, Id = "a1", End = ChildEnds.Failed, By = "reported" });
        _events.Append("s1", new SessionEvent { Kind = SessionEventKind.Message, Text = "running." });
        _events.Append("s1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn", Tokens = new TurnTokens(10, 5, null, null) });
        _events.Append("s1", new SessionEvent { Kind = SessionEventKind.Task, Id = "b2", End = ChildEnds.Lost, By = "inferred" });

        Assert.Equal("The gates are running.", _events.LastSaid("s1"));
        Assert.Equal(["run the gates", "The gates are running."], _events.Spoken("s1").Said.Select(said => said.Text));
        Assert.True(_events.TurnEnded("s1"));
        Assert.Equal(1, _events.Spent("s1").Turns);
        // A failed child or task is no failed call.
        Assert.Null(_events.Page("s1").FirstFailure);
        Assert.Equal("run the gates", _events.FirstAsked("s1"));
    }
}
