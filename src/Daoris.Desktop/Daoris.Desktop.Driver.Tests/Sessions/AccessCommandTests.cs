using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AUTONOMY1a (D184 point 1): <c>daoris-driver access</c>, the terminal's door to a repository's session access setup, its workspace's
/// and this machine's. It reads and writes the driver's file alone, through <see cref="AccessRules"/>, so it refuses what the screen's
/// <c>SET_ACCESS</c> refuses, in the same words, with nothing written.
/// </summary>
public sealed class AccessCommandTests : IDisposable
{
    private readonly string _folder = Path.Combine(AppContext.BaseDirectory, "access-command-" + Guid.NewGuid().ToString("N")[..8]);

    public AccessCommandTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string ConfigPath => Path.Combine(_folder, "driver.json");

    public static TheoryData<string, string> AskRows() => new()
    {
        { "", "machine say" },
        { "engine", "repository engine say" },
        { "--workspace shop", "workspace shop say" },
        { "engine full", "repository engine full" },
        { "engine FULL", "repository engine full" },
        { "--workspace shop --clear", "workspace shop clear" },
        { "permissions=asking goAheads=never", "machine set permissions=asking goAheads=never" },
        { "engine landing=rule routing=", "repository engine set landing=rule routing=" },
    };

    [Theory]
    [MemberData(nameof(AskRows))]
    public void The_words_name_a_level_and_a_change_or_none(string words, string expected)
    {
        var ask = AccessCommand.Read(Words(words), out var problem);

        Assert.Null(problem);
        Assert.Equal(expected, Spelled(ask!));
    }

    [Theory]
    [InlineData("--workspace", "`--workspace` takes a workspace's name.")]
    [InlineData("engine full landing=rule", "`access` takes `full`, `--clear`, or `<pause>=<value>` for each pause it sets — one change at a time.")]
    [InlineData("engine sometimes", "`access` takes `full`, `--clear`, or `<pause>=<value>` for each pause it sets — one change at a time.")]
    [InlineData("--json", "`--json` is not a repository's name; `access` takes `--workspace <name>`, or a repository's name, or neither for this machine.")]
    public void Words_it_cannot_read_are_its_usage(string words, string problem)
    {
        Assert.Null(AccessCommand.Read(Words(words), out var said));
        Assert.Equal(problem, said);
    }

    [Fact]
    public void A_set_writes_the_driver_s_file_and_a_say_answers_each_pause_with_where_it_came_from()
    {
        Assert.Equal(0, Run("engine permissions=asking goAheads=never", out var set));
        Assert.Contains("`engine`'s own access setup is now permissions asking, goAheads never.", set);
        Assert.Equal(0, Run("--workspace shop full", out _));

        var resolved = AccessRules.Resolve(DriverConfig.Load(ConfigPath), "engine", "shop");
        Assert.Equal(("asking", "repository"), (resolved.Value(AccessRules.Permissions), resolved.Source(AccessRules.Permissions)));
        Assert.Equal(("intake", "workspace"), (resolved.Value(AccessRules.Routing), resolved.Source(AccessRules.Routing)));

        Assert.Equal(0, Run("engine", out var said, new AccessPlace("shop", Known: true)));
        Assert.Contains("`engine`'s own: permissions asking, goAheads never.", said);
        Assert.Contains("  permissions  asking        its repository", said);
        Assert.Contains("  routing      intake        its workspace, `shop`", said);
        Assert.Contains("  questions    claude-code   its workspace, `shop`", said);

        // Its workspace not read: the service did not answer, so a workspace's setup is left out and the terminal says so.
        Assert.Equal(0, Run("engine", out var unplaced, new AccessPlace(null, Known: false)));
        Assert.Contains("  routing      person        the default", unplaced);
        Assert.Contains("its workspace was not read", unplaced);
    }

    /// <summary>
    /// ACCESSSWAP1b (D183's note): the terminal writes the file from another process and reaches no session's console, so a change that
    /// moves a level's permission prompts says how a driven session already running takes it: at its next turn, ending at this one's end
    /// and going on in the same conversation. A change of another pause says nothing of it.
    /// </summary>
    [Fact]
    public void A_change_of_permission_prompts_says_a_running_session_takes_it_at_its_next_turn()
    {
        const string NextTurn = "a driven session running now under other permission prompts takes these at its next turn";
        Assert.Equal(0, Run("engine permissions=asking", out var asking));
        Assert.Contains(NextTurn, asking);
        Assert.Equal(0, Run("engine landing=rule", out var landing));
        Assert.DoesNotContain(NextTurn, landing);
        Assert.Equal(0, Run("--workspace shop full", out var full));
        Assert.Contains(NextTurn, full);
        Assert.Equal(0, Run("engine permissions=asking", out var same));
        Assert.DoesNotContain(NextTurn, same);
    }

    [Fact]
    public void Full_access_and_a_clear_say_what_they_did()
    {
        Assert.Equal(0, Run("full", out var full));
        Assert.Contains("this machine now runs its sessions with full access: every pause off.", full);
        Assert.Contains("your own deny rules still hold", full);
        Assert.True(DriverConfig.Load(ConfigPath).MachineAccess!.Full);

        Assert.Equal(0, Run("", out var machine));
        Assert.Contains("this machine's own: full access.", machine);

        Assert.Equal(0, Run("--clear", out var cleared));
        Assert.Contains("this machine sets nothing of its own now", cleared);
        Assert.Null(DriverConfig.Load(ConfigPath).MachineAccess);
    }

    [Theory]
    [InlineData("engine plan=always", "`plan` is not a pause a session stops for — one of `permissions`, `routing`, `landing`, `goAheads` or `questions`.")]
    [InlineData("--workspace shop permissions=sometimes", "`permissions` is `asking`, `auto` or `unrestricted`, not `sometimes`.")]
    public void A_pause_or_value_it_does_not_have_is_refused_in_the_route_s_words_and_nothing_is_written(string words, string refusal)
    {
        Assert.Equal(0, Run("engine landing=rule", out _));
        var before = File.ReadAllText(ConfigPath);

        Assert.Equal(1, Run(words, out var said));

        Assert.Equal($"access: {refusal}", said.Trim());
        Assert.Equal(before, File.ReadAllText(ConfigPath));
    }

    private int Run(string words, out string said, AccessPlace? place = null)
    {
        using var output = new StringWriter();
        var exit = AccessCommand.Run(AccessCommand.Read(Words(words), out _)!, ConfigPath, place, output);
        said = output.ToString().ReplaceLineEndings("\n");
        return exit;
    }

    private static string[] Words(string words) => words.Length == 0 ? [] : words.Split(' ');

    private static string Spelled(AccessAsk ask)
    {
        var level = ask.Repository is { } repository ? $"repository {repository}" : ask.Workspace is { } workspace ? $"workspace {workspace}" : "machine";
        return ask.Edit switch
        {
            null => $"{level} say",
            { Full: true } => $"{level} full",
            { Clear: true } => $"{level} clear",
            { Set: { } set } => $"{level} set {string.Join(' ', set.Select(pause => $"{pause.Key}={pause.Value}"))}",
            _ => $"{level} ?",
        };
    }
}
