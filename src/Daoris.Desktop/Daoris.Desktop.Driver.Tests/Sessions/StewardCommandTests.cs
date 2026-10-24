using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris-driver steward …</c> (STEWARDROLE1b, D209 §3.3): each act's words make its route's exact form and nothing else; a verb with
/// no role's key sends nothing; and with no Daoris running the role does not exist, so every form answers <c>no-instance</c>, exit 2, and
/// nothing acts alone. The proof over a served pipe is the modules' <c>RoleActTests</c>; no test here starts a window or an agent.
/// </summary>
public sealed class StewardCommandTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "steward-command-tests", Guid.NewGuid().ToString("N")[..8]);

    public StewardCommandTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Remove(_home);

    [Theory]
    [InlineData("drive engine", "drive", """{"repository":"engine","drivable":true}""")]
    [InlineData("undrive engine", "undrive", """{"repository":"engine","drivable":false}""")]
    [InlineData("trees engine on", "trees", """{"repository":"engine","ownTree":true}""")]
    [InlineData("hold engine", "hold", """{"repository":"engine","held":true}""")]
    [InlineData("resume engine", "resume", """{"repository":"engine","held":false}""")]
    [InlineData("stop s1", "stop", """{"id":"s1","said":true}""")]
    [InlineData("finish s1", "finish", """{"id":"s1","state":"completed","said":true}""")]
    [InlineData("retry #q1", "retry", """{"quest":"q1"}""")]
    public void Each_verb_makes_its_route_s_exact_form(string words, string act, string form)
    {
        var read = StewardCommand.Read(words.Split(' '));

        Assert.NotNull(read);
        Assert.Equal(act, read.Value.Act);
        Assert.Equal(form, JsonSerializer.Serialize(read.Value.Form));
    }

    /// <summary>What is the owner's has no verb here: trees off, a decline, a done, a hand-over, words.</summary>
    [Theory]
    [InlineData("trees engine off")]
    [InlineData("decline s1")]
    [InlineData("done q1")]
    [InlineData("hand-over s1 --agent codex")]
    [InlineData("say s1 go on")]
    [InlineData("drive")]
    [InlineData("drive --key abc")]
    public void What_is_the_owner_s_has_no_verb(string words) => Assert.Null(StewardCommand.Read(words.Split(' ')));

    [Fact]
    public async Task An_act_with_no_role_s_key_sends_nothing()
    {
        var output = new StringWriter();

        var code = await StewardCommand.RunAsync(["drive", "engine"], new InstanceWorld { Home = _home, Verb = "steward" }, () => null, output);

        Assert.Equal(2, code);
        Assert.Contains("holds no role's key", output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>No alone path: with no Daoris running here, an act and the list each answer <c>no-instance</c>, exit 2.</summary>
    [Theory]
    [InlineData("drive engine")]
    [InlineData("acts")]
    public async Task With_no_daoris_running_the_steward_does_not_exist(string words)
    {
        var output = new StringWriter();

        var code = await StewardCommand.RunAsync(words.Split(' '), new InstanceWorld { Home = _home, Verb = "steward" }, HolderKey.Draw, output);

        Assert.Equal(2, code);
        Assert.Contains($"({StewardCommand.NoInstance})", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Words_no_verb_takes_answer_the_usage()
    {
        var output = new StringWriter();

        Assert.Equal(2, await StewardCommand.RunAsync(["trees", "engine", "off"], new InstanceWorld { Home = _home }, HolderKey.Draw, output));
        Assert.StartsWith(StewardCommand.Usage, output.ToString(), StringComparison.Ordinal);
    }
}
