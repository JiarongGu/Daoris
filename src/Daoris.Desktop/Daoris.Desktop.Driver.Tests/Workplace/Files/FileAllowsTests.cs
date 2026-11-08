using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A supervisor's allow of a rule file's ask: the held act's own text written into the layer of each repository the act reaches and
/// never the machine's, with the role and the decision; taken back like the person's own; and settled at the next start from the file.
/// </summary>
public sealed class FileAllowsTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "file-allows", Guid.NewGuid().ToString("N")[..8]);

    public FileAllowsTests() => Directory.CreateDirectory(_home);

    public void Dispose() => TreeSetAside.DeleteNeverFollowing(_home);

    private FileVerdict Judge(string repository, string act) => FileRules.Of([], [], PersonFiles.Read(_home), repository).Judge(act);

    private static FileAsk Ask(string repository, string act, string what = FileAsk.Run) => new(repository, "2026-11-08T10:00:00Z", what, act);

    [Fact]
    public void An_ask_met_again_keeps_the_start_that_first_met_it_and_a_closed_one_is_gone()
    {
        FileAsks.Note(_home, Ask("web", "run(npm ci)"));
        var again = FileAsks.Note(_home, Ask("web", "run(npm ci)") with { Start = "2026-11-08T11:00:00Z" });

        Assert.Equal("2026-11-08T10:00:00Z", again.Start);
        Assert.Single(FileAsks.Read(_home));

        FileAsks.Close(_home, "web", FileAsk.Run, "run(npm ci)");

        Assert.Empty(FileAsks.Read(_home));
    }

    [Fact]
    public void An_ask_that_names_the_person_is_read_so_and_an_allow_still_answers_it_as_any_allow_does()
    {
        PersonPermissions.Add(_home, null, "ask", "run(npm *)@person", null);

        var rules = FileRules.Of([], [], PersonFiles.Read(_home), "web");

        Assert.Equal(FileVerdict.Held, rules.Judge("run(npm ci)"));
        Assert.True(rules.NamesPerson("run(npm ci)"));
        Assert.False(rules.NamesPerson("run(pip install)"));
    }

    [Fact]
    public void A_machines_ask_is_answered_by_one_repositorys_allow_of_the_act_alone()
    {
        PersonPermissions.Add(_home, null, "ask", "run(npm *)", null);

        var given = FileAllows.Give(_home, Ask("x", "run(npm ci)"), "supervisor", "d1");

        Assert.Equal(1, given);
        Assert.Equal(FileVerdict.Made, Judge("x", "run(npm ci)"));
        Assert.Equal(FileVerdict.Held, Judge("x", "run(npm test)"));
        Assert.Equal(FileVerdict.Held, Judge("y", "run(npm ci)"));
        var read = PersonFiles.Read(_home);
        Assert.Empty(read.Machine.Permissions.Allow);
        Assert.Equal("supervisor", read.For("x").Permissions.Makers["run(npm ci)"]);
        Assert.Equal("d1", read.For("x").Permissions.Decisions["run(npm ci)"]);
    }

    [Fact]
    public void A_share_is_written_into_both_repositories_layers()
    {
        PersonPermissions.Add(_home, null, "ask", "share(*)", null);

        var given = FileAllows.Give(_home, Ask("x", "share(deps/)", FileAsk.Share) with { Others = ["y"] }, "supervisor", "d1");

        Assert.Equal(2, given);
        Assert.Equal(FileVerdict.Made, Judge("x", "share(deps/)"));
        Assert.Equal(FileVerdict.Made, Judge("y", "share(deps/)"));
        Assert.Equal(FileVerdict.Held, Judge("z", "share(deps/)"));
    }

    [Fact]
    public void A_taken_back_allow_asks_again()
    {
        PersonPermissions.Add(_home, null, "ask", "run(npm *)", null);
        FileAllows.Give(_home, Ask("x", "run(npm ci)"), "supervisor", "d1");

        Assert.True(PersonPermissions.Remove(_home, "x", "allow", "run(npm ci)"));

        Assert.Equal(FileVerdict.Held, Judge("x", "run(npm ci)"));
        Assert.True(FileAllows.StillHeld(_home, Ask("x", "run(npm ci)")));
    }

    [Fact]
    public void The_start_s_sweep_records_a_decision_made_whose_allow_stands_under_it_and_lapses_one_whose_allow_never_landed()
    {
        PersonPermissions.Add(_home, null, "ask", "run(npm *)", null);
        FileAsks.Note(_home, Ask("x", "run(npm ci)"));
        FileAsks.Note(_home, Ask("x", "run(npm test)"));
        FileAsks.Decide(_home, "x", "2026-11-08T10:00:00Z", FileAsk.Run, "run(npm ci)", "supervisor", "made-one");
        FileAsks.Decide(_home, "x", "2026-11-08T10:00:00Z", FileAsk.Run, "run(npm test)", "supervisor", "lost-one");
        FileAllows.Give(_home, Ask("x", "run(npm ci)"), "supervisor", "made-one");

        var swept = FileAllows.Sweep(_home);

        Assert.Equal([("made-one", true), ("lost-one", false)], swept.Select(each => (each.Decision, each.Made)));
        var left = Assert.Single(FileAsks.Read(_home));
        Assert.Equal("run(npm test)", left.Text);
        Assert.Null(left.Decision);
    }
}
