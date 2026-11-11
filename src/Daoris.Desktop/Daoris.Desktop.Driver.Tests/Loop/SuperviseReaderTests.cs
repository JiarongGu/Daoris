using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A supervisor's turn's folder and what it says ran: a copy only of a git work tree, an empty folder of its own for a subject with
/// none (Ask Daoris's room) and for a packet's reader whatever its subject names; the model its harness reported for its session's
/// turns, never the one asked, and none reported said as none.
/// </summary>
public sealed class SuperviseReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "supervise-reader", Guid.NewGuid().ToString("N")[..8]);

    public SuperviseReaderTests() => Directory.CreateDirectory(_root);

    public void Dispose() => GitSeed.Discard(_root);

    private string Folder(string name, bool git)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(git ? Path.Combine(path, ".git") : path);
        return path;
    }

    [Fact]
    public void A_git_work_tree_is_copied()
    {
        var tree = Folder("checkout", git: true);

        Assert.Equal(tree, SuperviseFolder.Copied(tree, packetOnly: false));
    }

    [Fact]
    public void A_worktree_whose_git_is_a_file_is_copied()
    {
        var tree = Folder("worktree", git: false);
        File.WriteAllText(Path.Combine(tree, ".git"), "gitdir: elsewhere\n");

        Assert.Equal(tree, SuperviseFolder.Copied(tree, packetOnly: false));
    }

    [Fact]
    public void A_room_with_no_git_takes_an_empty_folder_and_never_a_capture()
    {
        Assert.Null(SuperviseFolder.Copied(Folder("room", git: false), packetOnly: false));
        Assert.Null(SuperviseFolder.Copied(Path.Combine(_root, "gone"), packetOnly: false));
        Assert.Null(SuperviseFolder.Copied(null, packetOnly: false));
    }

    [Fact]
    public void A_packet_s_reader_takes_an_empty_folder_even_where_its_subject_names_a_git_tree()
    {
        Assert.Null(SuperviseFolder.Copied(Folder("checkout", git: true), packetOnly: true));
    }

    private sealed class Events : ISessionEvents
    {
        public event Action<string, SessionEvent>? Evented;

        public void Keep(string sessionId, SessionEvent e, Action<string>? say) => Evented?.Invoke(sessionId, e);
    }

    private static SessionEvent Turn(params string[] reported) => new()
    {
        Kind = SessionEventKind.Turn,
        Models = new TurnModels(reported.FirstOrDefault() ?? TurnModels.Unknown, null, null, TurnModels.FromMessages) { Reported = reported },
    };

    [Fact]
    public void Ran_is_the_model_the_harness_reported_for_the_reader_s_own_turns()
    {
        var events = new Events();
        using var ran = new SuperviseRan(events, "s-read");

        events.Keep("s-other", Turn("claude-opus-4-1"), null);
        events.Keep("s-read", Turn("claude-haiku-4-5-20251001"), null);
        events.Keep("s-read", new SessionEvent { Kind = SessionEventKind.Note, Text = "a note" }, null);

        Assert.Equal("claude-haiku-4-5-20251001", ran.Model);
    }

    [Fact]
    public void Ran_names_each_model_reported_once_in_the_order_reported()
    {
        var events = new Events();
        using var ran = new SuperviseRan(events, "s-read");

        events.Keep("s-read", Turn("claude-sonnet-4-5", "claude-haiku-4-5"), null);
        events.Keep("s-read", Turn("claude-sonnet-4-5"), null);

        Assert.Equal("claude-sonnet-4-5, claude-haiku-4-5", ran.Model);
    }

    [Fact]
    public void Ran_is_unknown_where_no_model_was_reported_and_stops_listening_once_disposed()
    {
        var events = new Events();
        var ran = new SuperviseRan(events, "s-read");

        events.Keep("s-read", Turn(), null);
        Assert.Null(ran.Model);

        ran.Dispose();
        events.Keep("s-read", Turn("claude-opus-4-1"), null);
        Assert.Null(ran.Model);
    }
}
