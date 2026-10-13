using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LAND3c (D102's LAND3b note): discarding a session branch is one act behind two doors, the screen's
/// <c>DISCARD_SESSION_BRANCH</c> by the branch and its repository, and the terminal's <c>trees remove &lt;session|branch&gt;</c>
/// by the branch or the session. Both keep a branch whose tree a session still running or waiting holds, even forced: LAND3b
/// built that keep into the screen's door alone, and the terminal's took the live session's tree. No git runs here: the keep
/// is read off the layout and the service's ledger before the checkout is asked, so a service standing in is the whole world.
/// The git half is <c>LandingTidyTests</c>.
/// </summary>
public sealed class SessionBranchDiscardTests : IDisposable
{
    private const string Branch = "daoris/s-1a2b3c4d";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-branch-discard-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_home, recursive: true);
        }
        catch (DirectoryNotFoundException) { /* nothing was written */ }
    }

    /// <summary>The tree Daoris's layout gives the branch, under this home's trees.</summary>
    private string Tree => Path.Combine(_home, "trees", "aurora", "engine", "s-1a2b3c4d");

    private SessionBranchDiscard Discard() => new(new SessionTrees(_home));

    /// <summary>
    /// The screen's door: forced, a branch whose tree a session running or waiting holds is kept, in the sentence the page
    /// words verbatim. The checkout is never asked, so nothing reached git; the tree stands.
    /// </summary>
    [Theory]
    [InlineData("working")]
    [InlineData(SessionStates.AwaitingPerson)]
    public async Task A_forced_discard_of_a_branch_whose_tree_a_live_session_holds_is_kept(string state)
    {
        Directory.CreateDirectory(Tree);
        var ledger = new DiscardLedger { Registry = [("engine", _home)], Live = [("s1", state, Tree)] };

        var kept = await Discard().DiscardAsync(ledger.Client(), "engine", Branch, force: true);

        Assert.False(kept.Removed);
        Assert.Equal("a session still running or waiting holds the tree of `daoris/s-1a2b3c4d` in `engine`, so it is kept.", kept.Message);
        Assert.Equal(SessionBranchDiscard.Held("engine", Branch), kept.Message);
        Assert.True(Directory.Exists(Tree));
        Assert.DoesNotContain("/api/registry", ledger.Asked);
    }

    /// <summary>
    /// The terminal's door, by every name it takes: the branch, its tree's name, and the live session itself, whose tree is
    /// still here. Each is kept, forced, with the screen's sentence; the tree stands and no checkout is asked.
    /// </summary>
    [Theory]
    [InlineData("daoris/s-1a2b3c4d", "engine")]
    [InlineData("s-1a2b3c4d", null)]
    [InlineData("s1", null)]
    public async Task The_terminals_forced_removal_keeps_a_branch_whose_tree_a_live_session_holds(string named, string? repository)
    {
        Directory.CreateDirectory(Tree);
        var trees = new SessionTrees(_home);
        trees.Grown.Record(new GrownBranch("engine", "aurora", Branch, "main", "abc123", null, DateTimeOffset.UtcNow));
        var ledger = new DiscardLedger { Registry = [("engine", _home)], Live = [("s1", "working", Tree)] };

        var kept = await new SessionBranchDiscard(trees).DiscardNamedAsync(ledger.Client(), named, repository, force: true);

        Assert.False(kept.Removed);
        Assert.Equal(SessionBranchDiscard.Held("engine", Branch), kept.Message);
        Assert.True(Directory.Exists(Tree));
        Assert.DoesNotContain("/api/registry", ledger.Asked);
        Assert.Single(trees.FindBranches(Branch, "engine"));
    }

    /// <summary>
    /// The keep reads the tree's branch off the layout, so another session's tree, or one in another repository, keeps
    /// nothing here: the act goes on to the checkout, which this machine does not have.
    /// </summary>
    [Fact]
    public async Task Only_the_branchs_own_tree_keeps_it()
    {
        var ledger = new DiscardLedger
        {
            Live =
            [
                ("s2", "working", Path.Combine(_home, "trees", "aurora", "engine", "s-ffffffff")),
                ("s3", "working", Path.Combine(_home, "trees", "aurora", "game", "s-1a2b3c4d")),
            ],
        };

        var removal = await Discard().DiscardAsync(ledger.Client(), "engine", Branch, force: true);

        Assert.False(removal.Removed);
        Assert.Equal("`engine` has no checkout here, so its branch `daoris/s-1a2b3c4d` cannot be removed from this machine.", removal.Message);
        Assert.Contains("/api/registry", ledger.Asked);
    }

    /// <summary>A checkout the registry names that is not on this disk is no checkout here, said as the door always said it.</summary>
    [Fact]
    public async Task A_registered_checkout_missing_from_this_disk_is_no_checkout_here()
    {
        var ledger = new DiscardLedger { Registry = [("engine", Path.Combine(_home, "nowhere"))] };

        var removal = await Discard().DiscardAsync(ledger.Client(), "engine", Branch, force: true);

        Assert.Equal("`engine` has no checkout here, so its branch `daoris/s-1a2b3c4d` cannot be removed from this machine.", removal.Message);
    }

    /// <summary>Only Daoris's own branches are discarded: a branch of the person's is refused in the driver's words, before git.</summary>
    [Fact]
    public async Task Only_a_session_branch_is_discarded_and_its_refusal_is_the_drivers()
    {
        Directory.CreateDirectory(_home);
        var ledger = new DiscardLedger { Registry = [("engine", _home)] };

        var removal = await Discard().DiscardAsync(ledger.Client(), "engine", "feature/0fda18-fix", force: true);

        Assert.False(removal.Removed);
        Assert.Contains("is not a session branch", removal.Message);
    }

    /// <summary>
    /// The terminal's naming, moved into the driver with the act: its sentences are the ones `trees remove` printed, each
    /// said before the keep or the checkout is asked.
    /// </summary>
    [Fact]
    public async Task The_terminals_naming_says_what_it_always_said()
    {
        var trees = new SessionTrees(_home);
        trees.Grown.Record(new GrownBranch("engine", "aurora", Branch, "main", "abc", null, DateTimeOffset.UtcNow));
        trees.Grown.Record(new GrownBranch("game", "aurora", Branch, "main", "def", null, DateTimeOffset.UtcNow));
        var ledger = new DiscardLedger { Closed = [("s9", Path.Combine(_home, "elsewhere", "s-9a8b7c6d"))] };
        var discard = new SessionBranchDiscard(trees);

        Assert.Equal("`s-1a2b3c4d` names a session branch in `engine`, `game` — say which with `--repository <name>`.",
            (await discard.DiscardNamedAsync(ledger.Client(), "s-1a2b3c4d", null, force: true)).Message);
        Assert.Equal("`s-ffffffff` names no session branch this machine recorded — say which repository holds it with `--repository <name>`.",
            (await discard.DiscardNamedAsync(ledger.Client(), "s-ffffffff", null, force: true)).Message);
        Assert.Equal("session `s8` names no working tree on this machine, so it left no session branch here.",
            (await discard.DiscardNamedAsync(ledger.Client(), "s8", null, force: true)).Message);
        Assert.Equal("session `s9`'s tree is not one this machine's trees home holds, so Daoris removes nothing for it.",
            (await discard.DiscardNamedAsync(ledger.Client(), "s9", null, force: true)).Message);
        Assert.DoesNotContain("/api/registry", ledger.Asked);
    }

    /// <summary>
    /// Named by the session whose tree is gone, the branch is the one its tree's layout names, discarded by the same act as
    /// the screen's: here, the checkout this machine does not have.
    /// </summary>
    [Fact]
    public async Task A_session_whose_tree_is_gone_names_its_branch_for_the_same_act()
    {
        var ledger = new DiscardLedger { Closed = [("s1", Tree)] };

        var removal = await Discard().DiscardNamedAsync(ledger.Client(), "s1", null, force: true);

        Assert.Equal("`engine` has no checkout here, so its branch `daoris/s-1a2b3c4d` cannot be removed from this machine.", removal.Message);
    }
}

/// <summary>
/// The service's ledger and registry standing in for a discard (LAND3c): the registry's checkouts, the sessions running or
/// waiting (<c>/api/sessions</c>), and every session with the closed ones (<c>?includeClosed=true</c>). Each path asked is kept.
/// </summary>
internal sealed class DiscardLedger : HttpMessageHandler
{
    private readonly List<string> _asked = [];

    public (string Repository, string Root)[] Registry { get; set; } = [];

    public (string Id, string State, string Tree)[] Live { get; set; } = [];

    public (string Id, string Tree)[] Closed { get; set; } = [];

    public IReadOnlyList<string> Asked
    {
        get { lock (_asked) return [.. _asked]; }
    }

    public ServiceClient Client() => new("http://stand-in", null, new HttpClient(this, disposeHandler: false));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var uri = request.RequestUri!;
        lock (_asked) _asked.Add(uri.AbsolutePath);
        object body = uri.AbsolutePath switch
        {
            "/api/registry" => Registry.Select(row => new { repository = row.Repository, workspace = "aurora", root = row.Root }).ToArray(),
            "/api/sessions" when uri.Query.Contains("includeClosed", StringComparison.Ordinal) =>
                Live.Select(each => new { id = each.Id, repository = "engine", state = each.State, tree = each.Tree })
                    .Concat(Closed.Select(each => new { id = each.Id, repository = "engine", state = "completed", tree = each.Tree }))
                    .ToArray(),
            "/api/sessions" => Live.Select(each => new { id = each.Id, repository = "engine", state = each.State, tree = each.Tree }).ToArray(),
            _ => Array.Empty<object>(),
        };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        });
    }
}
