using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// BRAINSTORM1a2 (D176 points 1 and 2; its amendment of D72): the <c>brainstorm</c> posture a pass is handed, in place of the reviewer's
/// rules BRAINSTORM1a handed it. Reading in its room and its one result tool are allowed, and nothing else: every write, the shell, the
/// web and every other tool of the connector are denied, the person's asks and denies stand, and the classifier is told it changes
/// nothing.
/// </summary>
/// <remarks>
/// The golden files under <c>golden/brainstorm-rules/</c> are what the posture adds, as the rules file the harness is handed holds it:
/// its allows whole, with the room's read written <c>Read(&lt;room&gt;)</c>, its denies beyond the person's composition, and its sentence
/// to the classifier. A change to the posture changes its golden file in the same commit, so review reads the rules as the agent meets them.
/// </remarks>
public sealed class BrainstormPostureTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-brainstorm-posture-" + Guid.NewGuid().ToString("N")[..8]);

    public BrainstormPostureTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_home, recursive: true);
        }
        catch (IOException) { }
    }

    private string Room(string turn) => Path.Combine(_home, "work", "brainstorm-rooms", "b1", turn);

    private static string GoldenPath(string file) =>
        Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "golden", "brainstorm-rules", file);

    /// <summary>The rules file the driver hands a pass on Claude Code's pipe door, as the harness reads it.</summary>
    private JsonNode Handed(string turn)
    {
        var adapters = AdapterSet.Built();
        var driver = new Daoris.Driver.Driver(new ServiceClient("http://127.0.0.1:9", null), DriverConfig.Empty, adapters, _home);
        var adapter = adapters.Resolve("claude-code");
        var info = new ProcessStartInfo("claude");

        var (file, _, _) = driver.HandRules(
            adapter, info, $"brainstorm-b1-{turn}", null, null, tree: Room(turn), kept: null, brainstorm: BrainstormPermissions.ToolOf(turn));

        Assert.NotNull(file);
        return JsonNode.Parse(File.ReadAllText(file!))!;
    }

    private static List<string> Strings(JsonNode? list) => [.. list!.AsArray().Select(each => each!.GetValue<string>())];

    /// <summary>
    /// 🔴 Each turn's rules are its golden file: a read of its room and its own result tool allowed and nothing else, every write, the
    /// shell, the web and the connector's other tools denied beside the person's own denies, and the classifier told it changes nothing.
    /// </summary>
    [Theory]
    [InlineData("g1", "give.json")]
    [InlineData("g1r", "give.json")]
    [InlineData("s1", "synthesize.json")]
    public void A_pass_is_handed_its_golden_rules(string turn, string golden)
    {
        var handed = Handed(turn);
        var expected = JsonNode.Parse(File.ReadAllText(GoldenPath(golden)))!;
        var composed = PermissionRules.Compose(PermissionRules.Load(_home), null, null);
        var permissions = handed["permissions"]!;

        var room = PermissionRules.ReadRule(Room(turn));
        Assert.Equal(Strings(expected["allow"]), Strings(permissions["allow"]).Select(rule => rule == room ? "Read(<room>)" : rule));
        Assert.Equal(composed.Ask, Strings(permissions["ask"] ?? new JsonArray()));
        var deny = Strings(permissions["deny"]);
        Assert.Equal(composed.Deny, deny.Take(composed.Deny.Count));
        Assert.Equal(Strings(expected["deny"]), deny.Skip(composed.Deny.Count));
        Assert.Equal(expected["hardDeny"]!.GetValue<string>(), Strings(handed["autoMode"]!["hard_deny"]).Last());
    }

    /// <summary>
    /// The posture narrows the person's composition and never widens it: their deny and ask stand, and their allows (a shell command,
    /// a folder elsewhere) are not a pass's, which reads its room alone. Neither result tool is ever another session's by default.
    /// </summary>
    [Fact]
    public void The_persons_denies_and_asks_stand_and_their_allows_are_not_a_passs()
    {
        var file = PermissionRules.Add(PermissionFile.Empty, RuleScope.Machine, null, RuleList.Allow, "Bash(npm test:*)");
        file = PermissionRules.Add(file, RuleScope.Machine, null, RuleList.Allow, "Read(//c/notes/**)");
        file = PermissionRules.Add(file, RuleScope.Machine, null, RuleList.Deny, "Read(//c/secrets/**)");
        file = PermissionRules.Add(file, RuleScope.Machine, null, RuleList.Ask, "Glob");
        var composed = PermissionRules.Compose(file, null, null);

        var handed = BrainstormPermissions.Shape(composed, Room("g1"), BrainstormPermissions.Give);

        Assert.Equal([PermissionRules.ReadRule(Room("g1")), "mcp__daoris-knowledge__brainstorm_give"], handed.Allow);
        Assert.Contains("Read(//c/secrets/**)", handed.Deny);
        Assert.Contains("Glob", handed.Ask);
        Assert.True(PermissionRules.AllowsConnector(handed, BrainstormPermissions.Give));
        Assert.False(PermissionRules.AllowsConnector(handed, BrainstormPermissions.Synthesize));
        Assert.False(PermissionRules.AllowsConnector(handed, "knowledge_search"));
        Assert.False(PermissionRules.AllowsConnector(composed, BrainstormPermissions.Give));
        Assert.Equal(handed.Deny.Distinct(StringComparer.Ordinal).Count(), handed.Deny.Count);
    }

    [Theory]
    [InlineData("g1", BrainstormPermissions.Give)]
    [InlineData("g2r", BrainstormPermissions.Give)]
    [InlineData("s1", BrainstormPermissions.Synthesize)]
    [InlineData("s1r", BrainstormPermissions.Synthesize)]
    public void A_turn_answers_through_its_kind_s_one_tool(string turn, string tool)
    {
        Assert.Equal(tool, BrainstormPermissions.ToolOf(turn));
    }
}
