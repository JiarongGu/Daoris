using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The ports Knowledge declares and the subdomains above it answer (FOUNDATION1 mod-viii, the design §14.3): a trace's reading of a
/// session's events, answered by the context. Each answer is installed when the driver's assembly loads, so the host's terminal, the
/// shell's route and every test read what they always read; a test may put another in and puts it back.
/// </summary>
public sealed class KnowledgePortsTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "knowledge-ports", Guid.NewGuid().ToString("N")[..8]);

    public KnowledgePortsTests() => Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));

    public void Dispose() => GitSeed.Discard(_home);

    private static DateTimeOffset At(int minute) => new(2026, 10, 26, 9, minute, 0, TimeSpan.Zero);

    private TraceEventsRead Read(string session) => TraceReads.Here(new TraceSources(null!, _home, DriverConfig.Empty)).ReadEvents(session);

    [Fact]
    public void The_reading_of_events_in_force_is_the_contexts_and_reads_a_sessions_file_whole()
    {
        Assert.IsType<SessionEventReads>(TraceEventFiles.Current);

        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Note, Text = "opened on `personal`.", At = At(1) });
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Message, Text = "Reading the tile.", At = At(2) });

        var read = Read("s1");

        Assert.Null(read.Missing);
        Assert.Equal(["opened on `personal`.", "Reading the tile."], read.All!.Select(each => each.Text));
        Assert.Equal(events.After("s1", 0).Events.Select(each => each.Seq), read.All!.Select(each => each.Seq));
    }

    [Fact]
    public void A_session_with_no_file_here_is_said_none_here()
    {
        var none = Read("s-none");

        Assert.Null(none.All);
        Assert.Equal(TraceEventGaps.NoneHere, none.Missing);
        Assert.Null(none.Problem);
    }

    [Fact]
    public void A_reading_that_fails_is_said_unread_with_its_problem_and_never_thrown()
    {
        var contexts = TraceEventFiles.Current;
        try
        {
            TraceEventFiles.Current = new StandEvents(new IOException("the file is held"));

            var unread = Read("s1");

            Assert.Null(unread.All);
            Assert.Equal(TraceEventGaps.Unread, unread.Missing);
            Assert.Equal("the file is held", unread.Problem);
        }
        finally
        {
            TraceEventFiles.Current = contexts;
        }
    }

    [Fact]
    public void A_test_may_install_another_reading_of_events_and_put_the_contexts_back()
    {
        var contexts = TraceEventFiles.Current;
        try
        {
            TraceEventFiles.Current = new StandEvents();

            var read = Read("anything");

            Assert.Equal(["said by the stand-in"], read.All!.Select(each => each.Text));
        }
        finally
        {
            TraceEventFiles.Current = contexts;
        }

        Assert.Same(contexts, TraceEventFiles.Current);
    }

    private sealed class StandEvents(Exception? fails = null) : ITraceEventFiles
    {
        public IReadOnlyList<SessionEvent>? Read(string folder, string sessionId) =>
            fails is not null
                ? throw fails
                : [new SessionEvent { Kind = SessionEventKind.Note, Text = "said by the stand-in", At = At(3) }];
    }
}
