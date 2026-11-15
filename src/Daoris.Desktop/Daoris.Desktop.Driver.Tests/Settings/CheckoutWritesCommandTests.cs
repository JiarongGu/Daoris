using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris place checkout</c>, the terminal's twin of <c>SET_CHECKOUT_WRITES</c>: whether Daoris may change a repository's
/// registered checkout's files. It writes the driver's file alone, through <see cref="DriverConfig.WithCheckoutWrites"/>, the edit the route calls.
/// </summary>
public sealed class CheckoutWritesCommandTests : IDisposable
{
    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "checkout-writes-command", Guid.NewGuid().ToString("N")[..8]);

    public CheckoutWritesCommandTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => GitSeed.Discard(_folder);

    private string ConfigPath => Path.Combine(_folder, "driver.json");

    public static TheoryData<string[], string, bool> AskRows() => new()
    {
        { ["engine", "on"], "engine", true },
        { [" engine ", "OFF"], "engine", false },
    };

    public static TheoryData<string[]> BadRows() => new()
    {
        { [] },
        { ["engine"] },
        { ["engine", "maybe"] },
        { ["--json", "on"] },
        { ["engine", "on", "off"] },
    };

    [Theory]
    [MemberData(nameof(AskRows))]
    public void The_words_name_a_repository_and_on_or_off(string[] words, string repository, bool allow)
    {
        var ask = CheckoutWritesCommand.Read(words, out var problem);

        Assert.Null(problem);
        Assert.Equal(repository, ask!.Repository);
        Assert.Equal(allow, ask.Allow);
    }

    [Theory]
    [MemberData(nameof(BadRows))]
    public void Words_it_cannot_read_are_its_usage(string[] words)
    {
        Assert.Null(CheckoutWritesCommand.Read(words, out var problem));
        Assert.False(string.IsNullOrWhiteSpace(problem));
    }

    [Fact]
    public void On_writes_the_driver_file_as_the_route_does_and_says_what_it_set()
    {
        using var output = new StringWriter();
        var ask = CheckoutWritesCommand.Read(["engine", "on"], out _)!;

        Assert.Equal(0, CheckoutWritesCommand.Run(ask, ConfigPath, output));

        Assert.True(DriverConfig.Load(ConfigPath).MayChangeCheckout("engine"));
        Assert.Contains("`engine`", output.ToString());
        Assert.Contains("may now change", output.ToString());
    }

    [Fact]
    public void Off_takes_the_declaration_away()
    {
        using var output = new StringWriter();
        CheckoutWritesCommand.Run(CheckoutWritesCommand.Read(["engine", "on"], out _)!, ConfigPath, output);

        Assert.Equal(0, CheckoutWritesCommand.Run(CheckoutWritesCommand.Read(["engine", "off"], out _)!, ConfigPath, output));

        Assert.False(DriverConfig.Load(ConfigPath).MayChangeCheckout("engine"));
        Assert.Contains("may no longer change", output.ToString());
    }

    [Fact]
    public void The_host_routes_the_verb_and_its_usage_names_it()
    {
        Assert.Equal("usage: daoris place checkout <repository> on|off", CheckoutWritesCommand.Usage);
        Assert.Contains("\n  place checkout <repository> on|off\n", DriverCommand.Usage.ReplaceLineEndings("\n"));
        var program = File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Host", "Program.cs"));
        Assert.Contains("if (args is [\"checkout\", .. var checkoutArgs])", program);
    }
}
