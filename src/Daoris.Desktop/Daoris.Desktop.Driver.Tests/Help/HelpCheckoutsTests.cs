using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HELPMODE1c4 (D183's note; the Ask Daoris modes design §4.1): Ask Daoris reads the checkouts through one typed tool the driver
/// judges at every call, for Claude and Codex alike. The person's record keeps the checkouts turned off, since the grant is on by
/// default (D107's readable set, inherited); each start hands the checkouts on, through the tool and no rule; and each call is
/// judged: the record is Ask Daoris's, the repository is registered in the conversation's workspace with a checkout here, readable
/// under D107, not turned off now and handed at this start; the path stays inside the checkout, out of git's own folder and out of
/// what git ignores. Git answers through its stand-in seam here, so no process starts; <c>HelpCheckoutsGitTests</c> asks the real one.
/// </summary>
public sealed class HelpCheckoutsTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-help-checkouts-" + Guid.NewGuid().ToString("N")[..8]);

    public HelpCheckoutsTests()
    {
        Directory.CreateDirectory(_home);
        Write("engine", "README.md", "# Engine\n");
        Write("engine", "src/Game.cs", "class Game {}\n");
        Write("engine", "local/notes.md", "kept private\n");
        Write("engine", ".git/config", "[remote \"origin\"]\n");
        Write("game", "README.md", "# Game\n");
        Write("game", ".git/HEAD", "ref: refs/heads/main\n");
        Write("rival", "README.md", "# Rival\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Root(string repository) => Path.Combine(_home, "checkouts", repository);

    private void Write(string repository, string relative, string text)
    {
        var path = Path.Combine(Root(repository), relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private void Choose(string json) => File.WriteAllText(ConfiguredAccess.PathOf(_home), json);

    /// <summary>What this start was handed, as its receipt keeps it: the checkouts the tool may read.</summary>
    private void Handed(string session, params string[] checkouts) =>
        new HelpWired(_home).Keep(session, new HelpReceipt
        {
            At = DateTimeOffset.UtcNow, Harness = "codex-acp", Folder = HelpRoom.PathOf(_home, "aurora"),
            Checkouts = [.. checkouts.Select(name => new HelpRead(name, "aurora", Root(name)))],
        });

    /// <summary>Ask Daoris's <c>h1</c> in workspace <c>aurora</c>, a driven <c>s1</c>; engine and game aurora's, rival borealis's, ghost with no checkout.</summary>
    private HousekeepingLedger Ledger() => new()
    {
        Records = [("h1", HelpRoom.Repository, "aurora", null), ("s1", "engine", "aurora", null)],
        Registry =
        [
            ("engine", "aurora", Root("engine")), ("game", "aurora", Root("game")), ("rival", "borealis", Root("rival")),
            ("ghost", "aurora", Root("ghost")),
        ],
    };

    private static SessionRequest Request(string operation, string? repository, string? path = null) =>
        new("h1", SessionMove.Read, DateTimeOffset.UtcNow)
        {
            By = RequestDoor.Help, Key = "0123456789ab", Operation = operation, Repository = repository, Path = path,
        };

    private Task<CheckoutAnswer> AnswerAsync(HousekeepingLedger ledger, SessionRequest request) =>
        new HelpCheckouts(_home, ledger.Client).AnswerAsync(request, CancellationToken.None);

    /// <summary>The git each call asks, by its arguments, kept for the test to read.</summary>
    private readonly List<IReadOnlyList<string>> _asked = [];

    /// <summary>
    /// Git standing in: <c>check-ignore</c> names <c>local/</c> ignored, whatever it was asked, as git names only what it was asked
    /// and the judge reads only those; <c>status</c> and <c>branch</c> answer their own words. Anything else fails, so a read that
    /// asks git for more than those three is seen.
    /// </summary>
    private void Git((int, string, string)? ignore = null)
    {
        WorkingTree.Answered.Value = arguments =>
        {
            lock (_asked) _asked.Add([.. arguments]);
            if (arguments.Contains("check-ignore")) return ignore ?? (0, "local/\0", "");
            if (arguments.Contains("status")) return (0, "On branch main\nnothing to commit, working tree clean\n", "");
            if (arguments.Contains("branch")) return (0, "* main\n  feature\n", "");
            return (1, "", "not answered here");
        };
    }

    /// <summary>Granted and handed: engine and game handed at this start, nothing turned off.</summary>
    private HousekeepingLedger Granted()
    {
        Handed("h1", "engine", "game");
        Git();
        return Ledger();
    }

    /// <summary>
    /// 🔴 The record keeps the checkouts turned off, since the grant is on by default: none kept is every readable checkout on; a
    /// choice turns one off or on again by its repository's name, the other grants kept as written; a name that is no one word is
    /// refused, and nothing is written.
    /// </summary>
    [Fact]
    public void The_record_keeps_the_checkouts_turned_off_and_a_choice_edits_them_alone()
    {
        Assert.Empty(ConfiguredAccess.Read(_home).CheckoutsOff);
        Assert.True(ConfiguredAccess.Read(_home).Reads("engine"));
        Choose("""{ "housekeeping": ["inspect"] }""");

        var off = ConfiguredAccess.Choose(_home, ConfiguredAccess.CheckoutsGrant, "engine", false);
        ConfiguredAccess.Choose(_home, ConfiguredAccess.CheckoutsGrant, "Alpha", false);

        var read = ConfiguredAccess.Read(_home);
        Assert.Equal(["engine"], off.CheckoutsOff);
        Assert.Equal(["Alpha", "engine"], read.CheckoutsOff);
        Assert.False(read.Reads("ENGINE"));
        Assert.True(read.Reads("game"));
        Assert.Equal([ConfiguredAccess.Inspect], read.Housekeeping);
        Assert.Equal(read.CheckoutsOff, read.Of(ConfiguredAccess.CheckoutsGrant));
        Assert.False(read.Same(off));
        using (var file = JsonDocument.Parse(File.ReadAllText(ConfiguredAccess.PathOf(_home))))
        {
            Assert.Equal(["Alpha", "engine"], file.RootElement.GetProperty("checkouts-off").EnumerateArray().Select(each => each.GetString()));
        }

        Assert.Equal(["Alpha"], ConfiguredAccess.Choose(_home, ConfiguredAccess.CheckoutsGrant, "Engine", true).CheckoutsOff);
        Assert.Contains("repository", Assert.Throws<DriverException>(() => ConfiguredAccess.Choose(_home, ConfiguredAccess.CheckoutsGrant, "two words", false)).Message);
        Assert.Contains("not built", Assert.Throws<DriverException>(() => ConfiguredAccess.Choose(_home, "shell", "run", true)).Message);
        Assert.Equal(["Alpha"], ConfiguredAccess.Read(_home).CheckoutsOff);
    }

    /// <summary>
    /// 🔴 Each start hands the checkouts on through the tool and no rule (the design's §4.1 table, now one mechanism for every agent):
    /// the place keeps the readable ones less those turned off, its allow-list names <c>checkout_read</c> and no <c>Read</c> or git
    /// rule for any checkout; with none to read, not even the tool.
    /// </summary>
    [Fact]
    public void Each_start_hands_the_checkouts_on_through_the_tool_and_no_rule()
    {
        var file = PermissionRules.Load(_home);
        HelpRead[] reads = [new("engine", "aurora", Root("engine")), new("game", "aurora", Root("game"))];
        Choose("""{ "checkouts-off": ["engine"] }""");

        var place = ChatRunner.HelpPlaceIn(_home, HelpRoom.PathOf(_home, "aurora"), reads, "aurora");
        var allow = place.Rules(file, "h1").Allow;

        Assert.Equal(["game"], place.Reads.Select(read => read.Name));
        Assert.Contains($"mcp__{KnowledgeConnector.ServerName}__{HelpCheckouts.Tool}", allow);
        Assert.DoesNotContain(allow, rule => rule.Contains(Root("game").Replace('\\', '/'), StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(allow, rule => rule.StartsWith("Bash(git -C", StringComparison.Ordinal));
        Assert.DoesNotContain(HelpRoom.Allowed, rule => rule.EndsWith(HelpCheckouts.Tool, StringComparison.Ordinal));

        Choose("""{ "checkouts-off": ["engine", "game"] }""");
        var none = ChatRunner.HelpPlaceIn(_home, HelpRoom.PathOf(_home, "aurora"), reads, "aurora");
        Assert.Empty(none.Reads);
        Assert.DoesNotContain(none.Rules(file, "h1").Allow, rule => rule.EndsWith(HelpCheckouts.Tool, StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 Granted and handed, a file is read whole with what it is said first, and a folder lists its entries, folders marked, git's
    /// own folder and what git ignores left out.
    /// </summary>
    [Fact]
    public async Task A_file_is_read_and_a_folder_listed_without_gits_folder_or_what_it_ignores()
    {
        var ledger = Granted();

        var file = await AnswerAsync(ledger, Request(HelpCheckouts.Read, "engine", "README.md"));
        Assert.Equal((HelpCheckouts.Read, true, null, "# Engine\n"), (file.Operation, file.Done, file.Code, file.Text));
        Assert.Contains("`engine`", file.Said);
        Assert.Contains("`README.md`", file.Said);
        Assert.Contains("whole", file.Said);

        var top = await AnswerAsync(ledger, Request(HelpCheckouts.Read, "engine"));
        Assert.True(top.Done);
        Assert.Equal(["src/", "README.md"], top.Text!.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("git ignores", top.Said);

        var nested = await AnswerAsync(ledger, Request(HelpCheckouts.Read, "engine", "./src/"));
        Assert.Equal(["Game.cs"], nested.Text!.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.DoesNotContain(_asked, arguments => arguments.Contains("log") || arguments.Contains("diff") || arguments.Contains("show"));
    }

    /// <summary>
    /// 🔴 A path is relative to the checkout and stays inside it: <c>..</c>, an absolute path, a drive or a stream, and git's own
    /// folder however it is spelled are refused before anything is read, and git is not asked.
    /// </summary>
    [Theory]
    [InlineData("../rival/README.md")]
    [InlineData("src/../../rival/README.md")]
    [InlineData("src\\..\\..\\rival\\README.md")]
    [InlineData("/etc/passwd")]
    [InlineData("\\README.md")]
    [InlineData("C:/Windows/win.ini")]
    [InlineData("README.md:secret")]
    [InlineData(".git/config")]
    [InlineData(".GIT/config")]
    [InlineData(".git./config")]
    [InlineData("src/*")]
    public async Task A_path_out_of_the_checkout_is_refused_and_nothing_is_read(string path)
    {
        var ledger = Granted();

        var answer = await AnswerAsync(ledger, Request(HelpCheckouts.Read, "engine", path));

        Assert.Equal((false, CheckoutAnswer.Escapes, null), (answer.Done, answer.Code, answer.Text));
        Assert.Contains("Nothing was read", answer.Said);
        Assert.Empty(_asked);
    }

    /// <summary>🔴 What git ignores is refused, a file in an ignored folder too (<c>local/</c>, build output, secrets kept ignored).</summary>
    [Fact]
    public async Task What_git_ignores_is_refused()
    {
        var ledger = Granted();

        var answer = await AnswerAsync(ledger, Request(HelpCheckouts.Read, "engine", "local/notes.md"));

        Assert.Equal((false, CheckoutAnswer.Ignored, null), (answer.Done, answer.Code, answer.Text));
        Assert.Contains("git ignores", answer.Said);
        Assert.Contains("check-ignore", Assert.Single(_asked));
    }

    /// <summary>
    /// 🔴 Where git cannot say what it ignores, nothing is read: a read that cannot be judged is refused, never let through as
    /// though nothing were ignored.
    /// </summary>
    [Fact]
    public async Task A_read_git_cannot_judge_is_refused()
    {
        Handed("h1", "engine");
        Git(ignore: (128, "", "fatal: not a git repository"));

        var answer = await AnswerAsync(Ledger(), Request(HelpCheckouts.Read, "engine", "README.md"));

        Assert.Equal((false, CheckoutAnswer.Failed, null), (answer.Done, answer.Code, answer.Text));
        Assert.Contains("Nothing was read", answer.Said);
    }

    /// <summary>
    /// 🔴 A file past the bound is cut at 200 KB, never mid-character, and said so; one that is not text is said so and not read;
    /// one not there is said so.
    /// </summary>
    [Fact]
    public async Task A_file_past_the_bound_is_cut_and_said_so()
    {
        var ledger = Granted();
        var big = string.Concat(Enumerable.Repeat("一二三四五六七八九十\n", 8_000));
        Write("engine", "big.txt", big);
        File.WriteAllBytes(Path.Combine(Root("engine"), "logo.png"), [0x89, 0x50, 0x4E, 0x47, 0x00, 0x00, 0x01]);

        var cut = await AnswerAsync(ledger, Request(HelpCheckouts.Read, "engine", "big.txt"));
        Assert.True(cut.Done);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(cut.Text!) <= HelpCheckouts.Bound);
        Assert.True(cut.Text!.Length > 60_000);
        Assert.StartsWith(cut.Text!, big, StringComparison.Ordinal);
        Assert.Contains("cut", cut.Said);
        Assert.Contains("200 KB", cut.Said);

        var binary = await AnswerAsync(ledger, Request(HelpCheckouts.Read, "engine", "logo.png"));
        Assert.Equal((true, null), (binary.Done, binary.Text));
        Assert.Contains("not text", binary.Said);

        var missing = await AnswerAsync(ledger, Request(HelpCheckouts.Read, "engine", "src/Nothing.cs"));
        Assert.Equal((false, CheckoutAnswer.Missing), (missing.Done, missing.Code));
    }

    /// <summary>
    /// 🔴 <c>status</c> and <c>branches</c> answer <c>git status</c> and <c>git branch --list</c>, never another git command, and name
    /// no path; nothing they ask writes (<c>--no-optional-locks</c>).
    /// </summary>
    [Fact]
    public async Task Status_and_branches_answer_git_and_name_no_path()
    {
        var ledger = Granted();

        var status = await AnswerAsync(ledger, Request(HelpCheckouts.Status, "engine"));
        var branches = await AnswerAsync(ledger, Request(HelpCheckouts.Branches, "game"));
        var pathed = await AnswerAsync(ledger, Request(HelpCheckouts.Status, "engine", "src"));

        Assert.Equal((true, "On branch main\nnothing to commit, working tree clean\n"), (status.Done, status.Text));
        Assert.Contains("git status", status.Said);
        Assert.Equal((true, "* main\n  feature\n"), (branches.Done, branches.Text));
        Assert.Equal((false, CheckoutAnswer.Named), (pathed.Done, pathed.Code));
        Assert.Equal(2, _asked.Count);
        Assert.Contains("--no-optional-locks", _asked[0]);
        Assert.Equal(["branch", "--list"], _asked[1].SkipWhile(argument => argument != "branch").Take(2));
        Assert.Equal(CheckoutAnswer.Named, (await AnswerAsync(ledger, Request("log", "engine"))).Code);
    }

    /// <summary>
    /// 🔴 A checkout turned off is refused at the next call, since each call reads the record; one handed at no start waits for the
    /// conversation's next turn; one whose repository switched reading by agents outside it off is refused, D107's set being the grant's.
    /// </summary>
    [Fact]
    public async Task Turned_off_not_handed_and_reading_switched_off_are_refused()
    {
        var ledger = Ledger();
        Git();
        Handed("h1", "engine");

        var notHanded = await AnswerAsync(ledger, Request(HelpCheckouts.Read, "game", "README.md"));
        Assert.Equal(CheckoutAnswer.NotHanded, notHanded.Code);
        Assert.Contains("next turn", notHanded.Said);

        Choose("""{ "checkouts-off": ["engine"] }""");
        var off = await AnswerAsync(ledger, Request(HelpCheckouts.Read, "engine", "README.md"));
        Assert.Equal(CheckoutAnswer.NotGranted, off.Code);
        Assert.Contains("configured access", off.Said);

        Choose("{}");
        var config = DriverConfig.ResolvePath(_home);
        DriverConfig.Load(config).WithReadAcross("engine", false).Save(config);
        var unreadable = await AnswerAsync(ledger, Request(HelpCheckouts.Read, "engine", "README.md"));
        Assert.Equal(CheckoutAnswer.NotReadable, unreadable.Code);
        Assert.Contains("daoris driver across engine read on", unreadable.Said);
        Assert.Empty(_asked);
    }

    /// <summary>
    /// 🔴 Only the conversation's own: a session that is not Ask Daoris's, a repository of another workspace, one not registered and
    /// one with no checkout here are each refused, the workspace read from the conversation's record and never from the request.
    /// </summary>
    [Fact]
    public async Task Another_session_another_workspace_and_no_checkout_are_refused()
    {
        var ledger = Granted();
        Handed("s1", "engine");

        var driven = await AnswerAsync(ledger, Request(HelpCheckouts.Read, "engine", "README.md") with { Session = "s1" });
        Assert.Equal(CheckoutAnswer.NotHelp, driven.Code);
        Assert.Contains("`s1`", driven.Said);

        var rival = await AnswerAsync(ledger, Request(HelpCheckouts.Read, "rival", "README.md"));
        Assert.Equal(CheckoutAnswer.Outside, rival.Code);
        Assert.Contains("`aurora`", rival.Said);

        Assert.Equal(CheckoutAnswer.Unknown, (await AnswerAsync(ledger, Request(HelpCheckouts.Read, "nobody"))).Code);
        Assert.Equal(CheckoutAnswer.NoCheckout, (await AnswerAsync(ledger, Request(HelpCheckouts.Read, "ghost"))).Code);
        Assert.Equal(CheckoutAnswer.Named, (await AnswerAsync(ledger, Request(HelpCheckouts.Read, null))).Code);
        Assert.Equal(CheckoutAnswer.NoService, (await new HelpCheckouts(_home, () => null).AnswerAsync(Request(HelpCheckouts.Read, "engine"), CancellationToken.None)).Code);
        Assert.Empty(_asked);
    }
}
