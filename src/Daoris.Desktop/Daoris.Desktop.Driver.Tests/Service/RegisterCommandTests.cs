using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris-driver register [--repository &lt;name&gt;]</c> (WSSETUP5, D124 §3.1, §4.5): the terminal's door to following each
/// line when the person asks, in the library's words, against a world in memory.
/// </summary>
public sealed class RegisterCommandTests
{
    private const string Declared = """{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":["play"],"accepts":[]}}""";

    [Theory]
    [InlineData(new string[0], null)]
    [InlineData(new[] { "--repository", "game" }, null)]
    [InlineData(new[] { "--repository" }, "`--repository` takes a repository's name.")]
    [InlineData(new[] { "--repository", "--all" }, "`--repository` takes a repository's name.")]
    [InlineData(new[] { "--all" }, "`--all` is not a word register takes.")]
    [InlineData(new[] { "game" }, "`game` is not a word register takes.")]
    public void The_words_are_none_or_one_repository_named(string[] args, string? problem)
    {
        Assert.Equal(problem, RegisterCommand.Problem(args));
    }

    [Fact]
    public async Task Words_it_does_not_take_are_the_usage_and_exit_2_and_nothing_is_followed()
    {
        var world = new RegistrationStandIn();
        world.Rows.Add(RegistrationStandIn.Row("game"));
        var output = new StringWriter();

        var code = await RegisterCommand.RunAsync(["--all"], output, world);

        Assert.Equal(2, code);
        Assert.Contains(RegisterCommand.Usage, output.ToString());
        Assert.Empty(world.Said);
    }

    [Fact]
    public async Task Every_repository_is_said_with_its_outcome_and_a_refusal_among_them_is_no_failure()
    {
        var world = new RegistrationStandIn();
        world.Rows.Add(RegistrationStandIn.Row("game", "/checkouts/game"));
        world.Rows.Add(RegistrationStandIn.Row("engine", "/checkouts/engine"));
        world.Lines["game"] = RegistrationStandIn.OnLine(Declared);
        world.Lines["engine"] = RegistrationStandIn.OnLine(null);
        var output = new StringWriter();

        var code = await RegisterCommand.RunAsync([], output, world);

        Assert.Equal(0, code);
        var said = output.ToString().ReplaceLineEndings("\n");
        Assert.Contains("  registered       game  registered from its line `main` at `0123456`", said);
        Assert.Contains("  not-set-up       engine  `engine` is not set up on its line `main`", said);
        Assert.Contains("register: 1 registered, 0 already as their lines say, 1 not registered, each saying why above.", said);
        Assert.Single(world.Sent);
    }

    [Fact]
    public async Task A_repository_named_and_refused_is_exit_1()
    {
        var world = new RegistrationStandIn();
        world.Rows.Add(RegistrationStandIn.Row("engine", "/checkouts/engine"));
        world.Lines["engine"] = RegistrationStandIn.OnLine(null);
        var output = new StringWriter();

        var refused = await RegisterCommand.RunAsync(["--repository", "engine"], output, world);
        var missing = await RegisterCommand.RunAsync(["--repository", "ghost"], output, world);

        Assert.Equal(1, refused);
        Assert.Equal(1, missing);
        Assert.Contains("`ghost` is not on this machine's registry", output.ToString());
    }

    [Fact]
    public async Task A_repository_named_whose_row_already_holds_its_line_is_exit_0()
    {
        var world = new RegistrationStandIn();
        world.Rows.Add(new RegistrationRow("game", "default", "/checkouts/game", true, "The game", ["play"], [], [], [], false, false, []));
        world.Lines["game"] = RegistrationStandIn.OnLine(Declared);
        var output = new StringWriter();

        var code = await RegisterCommand.RunAsync(["--repository", "game"], output, world);

        Assert.Equal(0, code);
        Assert.Contains("  unchanged        game  its row already holds what its line `main` at `0123456` declares.", output.ToString());
        Assert.Empty(world.Sent);
    }

    [Fact]
    public async Task No_checkout_here_is_said_as_nothing_to_register()
    {
        var output = new StringWriter();

        var code = await RegisterCommand.RunAsync([], output, new RegistrationStandIn());

        Assert.Equal(0, code);
        Assert.Contains("register: no repository has a checkout here, so there is nothing to register.", output.ToString());
    }

    [Fact]
    public async Task A_refresh_that_failed_is_said()
    {
        var world = new RegistrationStandIn { RefreshFails = "a shared deployment is fed, not scanned" };
        world.Rows.Add(RegistrationStandIn.Row("game", "/checkouts/game"));
        world.Lines["game"] = RegistrationStandIn.OnLine(Declared);
        var output = new StringWriter();

        await RegisterCommand.RunAsync([], output, world);

        Assert.Contains("register: the index was not read again after registering: a shared deployment is fed, not scanned", output.ToString());
    }
}
