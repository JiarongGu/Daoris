using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PIPELINE1b (D192 point 4, D50): <c>daoris-driver checks</c>, the terminal's door to which checks a repository's sessions run
/// under Current. It reads and writes the driver's file alone, through <see cref="SessionChecks.Apply"/>, so it refuses what Setup's door
/// does, in the same words, with nothing written.
/// </summary>
public sealed class SessionChecksCommandTests : IDisposable
{
    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "session-checks-command", Guid.NewGuid().ToString("N")[..8]);

    public SessionChecksCommandTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        GitSeed.Discard(_folder);
    }

    private string ConfigPath => Path.Combine(_folder, "driver.json");

    public static TheoryData<string[], string> AskRows() => new()
    {
        { [], "every say" },
        { ["engine"], "engine say" },
        { [" engine ", "--clear"], "engine clear" },
        { ["engine", "light"], "engine set light" },
        { ["engine", "heavy-at-end"], "engine set heavy-at-end" },
        // The door judges the value, so a word that is no choice is read and refused there, in its words.
        { ["engine", "heavy"], "engine set heavy" },
    };

    [Theory]
    [MemberData(nameof(AskRows))]
    public void The_words_name_a_repository_and_its_checks_or_a_clear_or_none(string[] words, string expected)
    {
        var ask = SessionChecksCommand.Read(words, out var problem);

        Assert.Null(problem);
        Assert.Equal(expected, Spelled(ask!));
    }

    public static TheoryData<string[], string> ProblemRows() => new()
    {
        { ["--json"], "`--json` is not a repository's name: `checks` takes a repository's name, then `light`, `heavy-at-end` or `--clear`." },
        { ["engine", "light", "heavy-at-end"], "one change at a time: `light`, `heavy-at-end` or `--clear`." },
        { ["engine", "--light"], "`--light` is not a session's checks: `checks` takes `light`, `heavy-at-end` or `--clear`." },
    };

    [Theory]
    [MemberData(nameof(ProblemRows))]
    public void Words_it_cannot_read_are_its_usage(string[] words, string problem)
    {
        Assert.Null(SessionChecksCommand.Read(words, out var said));
        Assert.Equal(problem, said);
    }

    /// <summary>A set writes the driver's file, a say tells the rule, and the list names each repository's.</summary>
    [Fact]
    public void A_set_writes_the_driver_s_file_and_a_say_tells_the_rule()
    {
        Assert.Equal(0, Run(["engine", "light"], out var set));
        Assert.Equal(
            "checks: `engine`'s sessions now run light: the gates its daoris.gates.json does not mark heavy and the checks a change reaches, "
            + "filtered; its heavy gates are left to the verification and refused to them.\n",
            set);
        Assert.Equal(SessionChecks.Light, SessionChecks.For(DriverConfig.Load(ConfigPath), "engine"));

        Assert.Equal(0, Run(["shop", "HEAVY-AT-END"], out var atEnd));
        Assert.Equal(
            "checks: `shop`'s sessions now run the checks a change reaches while they work, and its heavy gates once, at their end, through "
            + "`run-heavy`.\n",
            atEnd);

        Assert.Equal(0, Run(["ENGINE"], out var said));
        Assert.StartsWith("checks: `ENGINE`'s sessions run light: ", said);

        Assert.Equal(0, Run([], out var every));
        Assert.Equal(
            """
            checks: the sessions' checks set on this machine, each a repository's under Current:
              `engine`: light
              `shop`: heavy-at-end

            """.ReplaceLineEndings("\n"),
            every);
    }

    [Fact]
    public void A_clear_and_a_repository_with_none_say_so()
    {
        Assert.Equal(0, Run([], out var nothing));
        Assert.Equal("checks: no repository's sessions' checks are set on this machine, so each runs its checks as it did.\n", nothing);

        Assert.Equal(0, Run(["engine", "light"], out _));
        Assert.Equal(0, Run(["engine", "--clear"], out var cleared));
        Assert.Equal("checks: `engine` sets no sessions' checks now, so its sessions run their checks as they did.\n", cleared);
        Assert.Empty(DriverConfig.Load(ConfigPath).SessionChecks);

        Assert.Equal(0, Run(["shop"], out var none));
        Assert.Equal("checks: `shop` sets no sessions' checks, so its sessions run their checks as they did.\n", none);
    }

    /// <summary>A value the door refuses is said in its words, exit 1, with nothing written.</summary>
    [Fact]
    public void A_value_the_door_refuses_is_said_in_its_words_and_nothing_is_written()
    {
        Assert.Equal(0, Run(["engine", "light"], out _));
        var before = File.ReadAllText(ConfigPath);

        Assert.Equal(1, Run(["engine", "heavy"], out var said));

        Assert.StartsWith("checks: `heavy` is not a session's checks: they are `light`", said);
        Assert.Equal(before, File.ReadAllText(ConfigPath));
    }

    /// <summary>The host answers the verb, and its usage names it.</summary>
    [Fact]
    public void The_host_routes_the_verb_and_its_usage_names_it()
    {
        Assert.Contains("\n  checks [<repository> [light | heavy-at-end | --clear]]\n", DriverCommand.Usage.ReplaceLineEndings("\n"));
        Assert.Equal("usage: daoris-driver checks [<repository> [light | heavy-at-end | --clear]]", SessionChecksCommand.Usage);

        var program = File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Host", "Program.cs"));
        Assert.Contains("if (args is [\"checks\", .. var checksArgs])", program);
        Assert.Contains("Daoris.Driver.Host.SessionChecksConsole.Run(checksArgs)", program);
        Assert.Contains("//   checks [<repository> [light | heavy-at-end | --clear]]", program);
    }

    private int Run(string[] words, out string said)
    {
        using var output = new StringWriter();
        var exit = SessionChecksCommand.Run(SessionChecksCommand.Read(words, out _)!, ConfigPath, output);
        said = output.ToString().ReplaceLineEndings("\n");
        return exit;
    }

    private static string Spelled(SessionChecksAsk ask)
    {
        var who = ask.Repository ?? "every";
        if (ask.Clear) return $"{who} clear";
        return ask.Checks is { } checks ? $"{who} set {checks}" : $"{who} say";
    }
}
