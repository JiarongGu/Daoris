using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris session list</c> with no Daoris running places every row from what is recorded, as the window's list does, and walks a
/// tree only for a row needing the person, whose line says what that tree holds: listing walks no tree whose row needs none, and says
/// how many it left unread.
/// </summary>
public sealed class SessionListWalksTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-list-walks-" + Guid.NewGuid().ToString("N")[..8]);

    public SessionListWalksTests() => Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    /// <summary>A tree of this home's, standing, as a walk would find it.</summary>
    private string Tree(string name)
    {
        var tree = Path.Combine(new SessionTrees(_home).TreesRoot, "default", "engine", name);
        Directory.CreateDirectory(tree);
        File.WriteAllText(Path.Combine(tree, "part.txt"), "part\n");
        return tree;
    }

    private static JsonObject Record(string id, string? quest, string tree, string kind = "driven") => new()
    {
        ["id"] = id, ["repository"] = "engine", ["adapter"] = "claude-code", ["state"] = "completed", ["kind"] = kind, ["quest"] = quest,
        ["line"] = id, ["tree"] = tree, ["door"] = "claude-code",
        ["created"] = T0.ToString("O"), ["updated"] = T0.AddMinutes(1).ToString("O"),
    };

    private static Func<string, CancellationToken, Task<TreeWork?>> Walk(List<string> walked) => (tree, _) =>
    {
        lock (walked) walked.Add(tree);
        return Task.FromResult<TreeWork?>(new TreeWork(2, 0));
    };

    /// <summary>A run waiting on the person's Accept has its tree walked, its commits to review said; a done session's tree is left unread.</summary>
    [Fact]
    public async Task Listing_walks_only_the_trees_of_rows_needing_the_person()
    {
        var review = Tree("s-1");
        var done = Tree("s-2");
        QuestView[] quests =
        [
            new("q1", "game", "engine", "The work of #q1", "A body.", "Done"),
            new("q2", "game", "engine", "The work of #q2", "A body.", "Done"),
        ];
        var look = SessionLook.From(new JsonArray(Record("s1", "q1", review), Record("s2", "q2", done)).ToJsonString(), quests, [], _ => 0) with
        {
            RunWaits = [new PersonWait("q1", "review", PersonWaits.Accept, "engine", T0.AddMinutes(30))],
        };
        var walked = new List<string>();
        var times = new LookTimes();

        var listed = await ListedTrees.JudgeAsync(look, SessionGroups.Read(look), new SessionTrees(_home).Holds, Walk(walked), times, CancellationToken.None);

        Assert.Equal([review], walked);
        Assert.Equal((1, 1), (listed.Unread, times.Trees));
        var rows = listed.Rows.ToDictionary(row => row.Session, StringComparer.Ordinal);
        Assert.Equal((SessionGroup.NeedsYou, 2), (rows["s1"].Group, rows["s1"].Work?.Commits));
        Assert.Equal((SessionGroup.Done, null), (rows["s2"].Group, rows["s2"].Lands));
    }

    /// <summary>
    /// The terminal's verb alone: a done conversation's tree is never walked, and the listing's foot says the trees it left unread and
    /// the door that reads one; <c>--json</c> stays one element.
    /// </summary>
    [Fact]
    public async Task The_terminal_alone_walks_no_tree_of_a_done_session_and_says_it_left_it_unread()
    {
        var tree = Tree("s-3");
        var ledger = new SessionsCommandTests.Ledger([Record("c0ffee11", null, tree, kind: "chat")], []);
        using var service = ledger.Client();
        var walked = new List<string>();
        var world = new SessionsWorld(service, _home, DriverConfig.Empty, false, null) { Walk = Walk(walked) };

        foreach (var json in new[] { false, true })
        {
            var output = new StringWriter();
            var exit = await SessionsCommand.RunAsync(new SessionsAsk("list") { Json = json }, world, output);

            Assert.Equal(0, exit);
            Assert.Empty(walked);
            var said = output.ToString().ReplaceLineEndings("\n");
            if (json) Assert.DoesNotContain(ListedTrees.Unread(1), said);
            else Assert.EndsWith(ListedTrees.Unread(1) + "\n", said);
        }
    }
}
