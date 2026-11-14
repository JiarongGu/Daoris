using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The command a cooling account's line names (<c>daoris agent account ready &lt;agent&gt; &lt;account&gt;|--own</c>) works for
/// every account a cool-off is written under: the terminal takes its words, the route's gate takes the agent it sends, and the roster
/// ends the cool-off. The line prints the entry's owner key, which a build's doors, the agents they run as (<c>codex</c>) and a plugin
/// door's own account key (<c>acme</c>, which no program answers) each are.
/// </summary>
public sealed class AccountReadyCommandTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 8, 0, 0, TimeSpan.Zero);

    private readonly string _home = ConformanceScratch.New("account-ready-command");

    public void Dispose() => ConformanceScratch.Remove(_home);

    private sealed class Declared(IEnumerable<DeclaredDoor> doors) : IAgentPlugins
    {
        public IEnumerable<DeclaredDoor> Doors { get; } = doors;

        public IEnumerable<string> DeclaredAgents { get; } = doors.Select(door => door.Name);

        public IReadOnlyList<SkillSource> Skills(string to) => [];
    }

    public static TheoryData<string> Registries => new() { "built", "plugin" };

    private static ProviderRegistry Registry(string kind) => kind == "built"
        ? ProviderRegistry.Built()
        : ProviderRegistry.Built().WithPlugins(new Declared([
            new("acme-agent", "acme", ["acme-agent", "--acp"], Posture: "edits", ProfileVariable: null, AccountOf: "acme", Product: "Acme", Maker: null),
        ]));

    private AgentsWorld World(ProviderRegistry registry, StringWriter said) => new()
    {
        Instance = new InstanceWorld
        {
            Home = _home, Environment = variable => variable == DaorisHome.Variable ? _home : null,
            Out = said, Error = said, In = new StringReader(""), Verb = "daoris",
        },
        Adapters = _ => registry,
        Now = () => Now,
    };

    [Theory]
    [MemberData(nameof(Registries))]
    public void The_command_the_line_of_a_cooling_account_names_is_taken_and_ends_it_for_every_owner(string kind)
    {
        var registry = Registry(kind);
        var roster = new HarnessRoster(registry, DaorisHome.Entry(_home, HomeEntry.Harnesses)) { Clock = () => Now };
        // The owners of the agents the verbs take; a stand-in names no program and is none.
        var owners = registry.Operands().Select(name => registry.OwnerOfAgent(name).Value).Distinct(StringComparer.Ordinal).ToList();
        Assert.Contains("codex", owners);
        if (kind == "plugin") Assert.Contains("acme", owners);

        foreach (var owner in owners)
        {
            foreach (var account in new string?[] { "account-1", null })
            {
                var entry = new CoolingEntry(owner, account, Now.AddHours(1), true, "weekly", Now, "3f9c2a71", false, false);
                AccountCooling.Cool(_home, entry, Now);
                var line = AgentAccountLines.CoolingLine(entry, Now, TimeZoneInfo.Utc);
                var words = Regex.Match(line, "`daoris agent account ready ([^`]+)`").Groups[1].Value.Split(' ');
                var said = new StringWriter();

                // The terminal's gate, the route's gate, then the roster's end.
                var press = AgentAccountsCommand.Profile(["profile", "ready", .. words], World(registry, said), out var exit);
                Assert.True(press is not null && exit == 0, $"`{line}`: the terminal refused it: {said}");
                Assert.NotNull(ManagedAgents.Of(roster, words[0]));
                Assert.True(roster.Ready(words[0], account), $"`ready {string.Join(' ', words)}` did not end the cool-off");
                Assert.Null(AccountCooling.Of(_home, owner, account, Now));
            }
        }
    }
}
