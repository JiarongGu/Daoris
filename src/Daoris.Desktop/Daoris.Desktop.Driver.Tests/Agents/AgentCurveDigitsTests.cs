using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>`--curve` takes whole percents in ASCII digits: any other digit is the verb's own refusal, never an exception from the parse.</summary>
public sealed class AgentCurveDigitsTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "agent-curve-digits", Guid.NewGuid().ToString("N")[..8]);

    private readonly StringWriter _out = new();

    private readonly StringWriter _error = new();

    public AgentCurveDigitsTests() => Directory.CreateDirectory(Path.Combine(_root, "home"));

    public void Dispose() => GitSeed.Discard(_root);

    private AgentsWorld World() => new()
    {
        Instance = new InstanceWorld
        {
            Home = Path.Combine(_root, "home"),
            Environment = variable => variable == DaorisHome.Variable ? Path.Combine(_root, "home") : null,
            Out = _out,
            Error = _error,
            In = new StringReader(""),
            Verb = "daoris",
        },
        Folder = _root,
        UserHome = Path.Combine(_root, "user"),
        Adapters = _ => ProviderRegistry.Built(),
        Now = () => DateTimeOffset.UtcNow,
    };

    [Theory]
    [InlineData("weekly=\u0660:\u0663")]
    [InlineData("weekly=\u0661\u0660:50")]
    public async Task A_curve_in_other_digits_is_refused_in_the_verbs_words(string curve)
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(Path.Combine(_root, "home"), "claude-code", "work"));

        var exit = await AgentsCommand.RunAsync(
            ["profile", "use", "claude-code", "order", "--switch", "curve", "--curve", curve], World(), CancellationToken.None);

        Assert.Equal(2, exit);
        Assert.Contains("is not a window and its points", _error.ToString(), StringComparison.Ordinal);
    }
}
