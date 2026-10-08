using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Which knowledge connector a protocol-door session is handed (ACP4), in the order
/// <see cref="ServiceHostLocator"/> finds the HTTP host: what the person said, then what the install
/// carries beside the running application, then the Daoris home's `bin/`, then the workspace build.
/// </summary>
/// <remarks>
/// 🔴 CONNECTOR1. The install carried its HTTP host and no connector, so every session on a deployed
/// shell was handed the home's `bin/` copy, which `publish:service --install` laid down once and no
/// republish refreshed. It was eight days old on the install that found it, and it opened the
/// shared store at its own older schema. Asserted as an ORDER, because the order is the contract.
/// </remarks>
public sealed class KnowledgeConnectorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-connector-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static string Touch(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
        return path;
    }

    /// <summary>An install as `publish:desktop --service` lays it out (D93): the application runs from its `app/`.</summary>
    private string Application => Path.Combine(_root, "install", "app");

    private string OwnCopy => Path.Combine(Application, "daoris-knowledge", KnowledgeConnector.ExecutableName);

    private string Home => Path.Combine(_root, "install", "data");

    private string HomeCopy => Path.Combine(Home, "bin", KnowledgeConnector.ExecutableName);

    [Fact]
    public void What_the_person_said_comes_first_then_the_shells_own_copy_then_the_homes_bin()
    {
        var candidates = KnowledgeConnector.Candidates("D:/somewhere/connector.exe", Home, Application);

        Assert.Equal(
            [
                "D:/somewhere/connector.exe",
                Path.Combine(Application, "daoris-knowledge", KnowledgeConnector.ExecutableName),
                Path.Combine(Application, "app", "daoris-knowledge", KnowledgeConnector.ExecutableName),
                HomeCopy,
            ],
            candidates.Take(4));
    }

    /// <summary>
    /// 🔴 The defect, whole: the install carries a connector and the home's `bin/` holds an older one.
    /// The session is handed the install's, by its absolute path.
    /// </summary>
    [Fact]
    public void A_session_on_a_deployed_shell_is_handed_the_connector_published_with_it_over_the_homes_bin()
    {
        Touch(HomeCopy);
        var own = Touch(OwnCopy);

        var offered = KnowledgeConnector.Offer(null, Home, Application, new Dictionary<string, string?>());

        Assert.NotNull(offered);
        Assert.Equal(own, offered.Command);
    }

    /// <summary>
    /// Both shapes the HTTP host's locator takes, for the same reason: the application runs from the
    /// install's `app/` (D93), and a hand-assembled folder runs it from beside the connector's folder.
    /// </summary>
    [Theory]
    [InlineData("daoris-knowledge")]
    [InlineData("app/daoris-knowledge")]
    public void A_shell_finds_the_connector_published_beside_it(string relative)
    {
        var folder = Path.Combine(Application, relative.Replace('/', Path.DirectorySeparatorChar));
        var own = Touch(Path.Combine(folder, KnowledgeConnector.ExecutableName));

        // No home: the deployed case before anything is set, and a machine with no home has no `bin/` (D63).
        Assert.Equal(own, KnowledgeConnector.Locate(null, home: null, Application));
    }

    /// <summary>`DAORIS_MCP_HOST` is the person's word and outranks what the install carries and the home holds.</summary>
    [Fact]
    public void The_explicit_path_outranks_the_shells_own_copy_and_the_homes()
    {
        Touch(HomeCopy);
        Touch(OwnCopy);
        var named = Touch(Path.Combine(_root, "elsewhere", KnowledgeConnector.ExecutableName));

        Assert.Equal(named, KnowledgeConnector.Locate(named, Home, Application));
    }

    /// <summary>A shell published without `--service` carries no connector, and the home's `bin/` is still found next.</summary>
    [Fact]
    public void A_shell_that_carries_no_connector_falls_through_to_the_homes_bin()
    {
        var installed = Touch(HomeCopy);

        Assert.Equal(installed, KnowledgeConnector.Locate(null, Home, Application));
    }

    /// <summary>Development: the workspace build after every installed place, found by walking up to the manifest.</summary>
    [Fact]
    public void The_workspace_build_comes_after_the_shells_own_copy_and_the_homes()
    {
        var workspace = Path.Combine(_root, "workspace");
        var deep = Path.Combine(workspace, "src", "Daoris.Desktop", "app", "bin");
        Directory.CreateDirectory(deep);
        File.WriteAllText(Path.Combine(workspace, "daoris.json"), "{}");

        var candidates = KnowledgeConnector.Candidates(null, Home, deep).ToList();
        var dev = Path.Combine(
            workspace, "src", "Daoris.Service", "Daoris.Service.Mcp", "bin", "Debug", "net10.0", KnowledgeConnector.ExecutableName);

        Assert.True(candidates.IndexOf(dev) > candidates.IndexOf(HomeCopy));
        Assert.True(candidates.IndexOf(HomeCopy) > candidates.IndexOf(Path.Combine(deep, "daoris-knowledge", KnowledgeConnector.ExecutableName)));
    }

    [Fact]
    public void Nothing_anywhere_is_no_connector_rather_than_a_throw()
    {
        Directory.CreateDirectory(Application);

        Assert.Null(KnowledgeConnector.Offer(null, Home, Application, new Dictionary<string, string?>()));
    }
}
