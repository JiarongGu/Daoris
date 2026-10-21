using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HOMELEASE1 (the terminal RPC design §5, §7.1): a verb acting alone holds the home's lease for its mutation, never for a read,
/// and a verb whose one writer is the service, a conversation, or the loop takes none of a verb's. An unclassed verb holds it.
/// </summary>
public sealed class VerbLeasesTests
{
    [Theory]
    // The mutations a verb makes alone (§1.2's census), for its run.
    [InlineData("trees land s-1")]
    [InlineData("trees remove some-branch --force")]
    [InlineData("trees clean --yes")]
    [InlineData("trees sync --all --yes")]
    [InlineData("trees hand s-1")]
    [InlineData("sessions stop s-1")]
    [InlineData("sessions say s-1 words")]
    [InlineData("sessions archive --ended --yes")]
    [InlineData("sessions delete s-1")]
    [InlineData("sessions helper s-1 why")]
    [InlineData("ask --pause a-1")]
    [InlineData("ask --clear a-1 --yes")]
    [InlineData("quest pause 12")]
    [InlineData("quest clear 12 --yes")]
    [InlineData("history clear --workspace w --yes")]
    [InlineData("help rename c-1 name")]
    [InlineData("help delete c-1")]
    [InlineData("help mode full")]
    [InlineData("access engine full")]
    [InlineData("access --workspace w landing=rule")]
    [InlineData("access permissions=asking")]
    [InlineData("access engine --clear")]
    [InlineData("heavy engine build=x")]
    [InlineData("heavy engine --clear")]
    [InlineData("checks engine light")]
    [InlineData("checks engine --clear")]
    [InlineData("opinion myself 12")]
    [InlineData("setup some-repository")]
    [InlineData("setup --workspace w --pause")]
    [InlineData("register")]
    [InlineData("sync")]
    [InlineData("sync dismiss 12")]
    [InlineData("sync forward 12 desk 41")]
    [InlineData("plugins install a.nupkg")]
    [InlineData("plugins new id --point p")]
    [InlineData("update --now")]
    [InlineData("workflow keep s-1")]
    // The driver's standing choices, `daoris driver …` handed on (PUSH1f2a, D194 point 2): each a short change, held from the top.
    [InlineData("driver drive engine")]
    [InlineData("driver undrive engine")]
    [InlineData("driver hold engine")]
    [InlineData("driver resume engine")]
    [InlineData("driver trees engine on")]
    [InlineData("driver notify off")]
    [InlineData("driver cap 3")]
    [InlineData("driver strikes 0")]
    [InlineData("driver retry 12")]
    [InlineData("driver retry 12 --at 2")]
    [InlineData("driver retry 12 --session s-1")]
    [InlineData("driver timeout 90")]
    [InlineData("driver cooloff 45")]
    [InlineData("driver adapter claude-code")]
    [InlineData("driver helper off")]
    [InlineData("driver intake claude-code")]
    [InlineData("driver line engine develop")]
    [InlineData("driver landing engine merge")]
    [InlineData("driver across engine read off")]
    [InlineData("driver standing engine words")]
    [InlineData("driver language engine zh")]
    [InlineData("driver review engine none")]
    [InlineData("driver opinion engine --reviewers codex-acp")]
    [InlineData("driver helpers engine --mode on")]
    [InlineData("driver handover engine --mode on")]
    [InlineData("driver commits engine --mode ask")]
    // The family's last verbs (PUSH1f2b): a landing's cycle, a work cycle, the brainstorm policy set or cleared, and a workflow's writes.
    [InlineData("driver landing engine merge --cycle cycle.json")]
    [InlineData("driver landing engine merge --no-cycle")]
    [InlineData("driver work-cycle engine quest cycle.json")]
    [InlineData("driver work-cycle --workspace shop ask --clear")]
    [InlineData("driver brainstorms --mode off")]
    [InlineData("driver brainstorms --workspace work --pass-minutes 5")]
    [InlineData("driver brainstorms --clear")]
    [InlineData("driver workflow new docs --from merge-after-accept")]
    [InlineData("driver workflow edit docs remove look")]
    [InlineData("driver workflow apply docs next.json")]
    [InlineData("driver workflow import carried.json")]
    [InlineData("driver workflow use docs --repository engine")]
    [InlineData("driver workflow use web-verify --repository engine --landed")]
    [InlineData("driver workflow kind work docs --label Documentation")]
    // The tools `daoris tool …` hands on (PUSH1f2c, D194 point 2): each short change, held from the top.
    [InlineData("tool use git system")]
    [InlineData("tool use git file C:/bin/git.exe")]
    [InlineData("tool delete gh 2.62.0")]
    [InlineData("tool locations add https://lists.example/r.json")]
    [InlineData("tool locations remove https://lists.example/r.json")]
    [InlineData("tool look")]
    // The agents' rules, trust and settings `daoris agent …` hands on (PUSH1f2d), a pin taken back and an account's change (PUSH1f2e2):
    // each change, held from the top.
    [InlineData("agent rules allow WebFetch")]
    [InlineData("agent rules deny Bash(rm:*) --workspace work")]
    [InlineData("agent rules ask Edit(/docs/**) --repository engine")]
    [InlineData("agent rules remove WebFetch")]
    [InlineData("agent rules default no-push off")]
    [InlineData("agent rules accept p0000007")]
    [InlineData("agent rules decline p0000007 --note no")]
    [InlineData("agent settings claude-code --account work model opus")]
    [InlineData("agent settings claude-code effort low --for claude-opus-5")]
    [InlineData("agent trust claude-code D:/work/engine --yes")]
    [InlineData("agent unpin claude-code")]
    [InlineData("agent unpin claude-code --workspace work")]
    [InlineData("agent profile add claude-code work")]
    [InlineData("agent profile remove claude-code work")]
    [InlineData("agent profile default claude-code --clear --workspace work")]
    [InlineData("agent profile order claude-code account-1 account-2")]
    [InlineData("agent profile ready claude-code --own")]
    [InlineData("agent profile rename claude-code account-1 seat")]
    [InlineData("agent profile join claude-code account-1 --machine")]
    [InlineData("agent profile use claude-code order")]
    [InlineData("agent profile use claude-code --switch off --workspace work")]
    [InlineData("agent profile use claude-code --clear")]
    [InlineData("agent profile use claude-code --frob")]
    [InlineData("a-verb-nobody-classed")]
    public void A_mutation_holds_the_lease(string words) => Assert.True(VerbLeases.Holds(words.Split(' ')), words);

    [Theory]
    // Reads.
    [InlineData("trace commit abc")]
    [InlineData("git branches")]
    [InlineData("logs --since 2h")]
    [InlineData("workflow run --session s-1")]
    [InlineData("sessions")]
    [InlineData("sessions --group you --json")]
    [InlineData("history --json")]
    [InlineData("trees")]
    [InlineData("trees list")]
    [InlineData("trees state s-1")]
    [InlineData("trees land s-1 --plan")]
    [InlineData("setup some-repository --plan")]
    [InlineData("plugins show id")]
    [InlineData("plugins activity id")]
    [InlineData("sync status")]
    [InlineData("help list")]
    [InlineData("help mode")]
    [InlineData("access")]
    [InlineData("access engine")]
    [InlineData("access --workspace w")]
    [InlineData("heavy")]
    [InlineData("heavy engine")]
    [InlineData("checks")]
    [InlineData("checks engine")]
    [InlineData("opinion show 12")]
    [InlineData("update")]
    [InlineData("trees --help")]
    // The service's own doors: its one writer is the service already.
    [InlineData("quest delete 12")]
    [InlineData("quest accept 12")]
    [InlineData("quest done 12")]
    [InlineData("quest check 12")]
    [InlineData("quest review 12 reviewed")]
    [InlineData("quest reopen 12")]
    [InlineData("quest go-on 12")]
    [InlineData("answer s-1 go on")]
    [InlineData("ask words of an ask")]
    [InlineData("ask --publish a-1 --to r")]
    [InlineData("ask channel a-1")]
    [InlineData("ask channel a-1 --ordinary when you can")]
    // A conversation in the terminal, until RPC1g; and the loop, which holds it as an instance.
    [InlineData("chat --repository r")]
    [InlineData("help resume c-1")]
    [InlineData("sessions start-from s-1")]
    [InlineData("sessions hand-over s-1 --agent codex")]
    [InlineData("drive --once")]
    [InlineData("--until-idle")]
    // A repository's heavy gates, which run for minutes and write the home only in their own slot (HEAVYRUN1a).
    [InlineData("run-heavy")]
    [InlineData("run-heavy --gate web")]
    // The driver's standing choices read, or the usage: `list`, the bare family, a verb it does not have, a plan (PUSH1f2a).
    [InlineData("driver")]
    [InlineData("driver list")]
    [InlineData("driver --help")]
    [InlineData("driver frobnicate engine")]
    [InlineData("driver landing engine merge --plan")]
    // The family's last reads (PUSH1f2b): the brainstorm policy said, and a workflow shown, listed, exported or planned.
    [InlineData("driver brainstorms")]
    [InlineData("driver brainstorms --workspace work")]
    [InlineData("driver workflow")]
    [InlineData("driver workflow list")]
    [InlineData("driver workflow show docs@2")]
    [InlineData("driver workflow show --repository engine --kind docs")]
    [InlineData("driver workflow export docs --to carried.json")]
    [InlineData("driver workflow new docs --from merge-after-accept --plan")]
    [InlineData("driver workflow use docs --workspace work --plan")]
    [InlineData("driver workflow frobnicate")]
    // The tools read, their usage, and the long verbs, which take the lease themselves to publish (PUSH1f2c, the alone-path design §1.4).
    [InlineData("tool")]
    [InlineData("tool list")]
    [InlineData("tool path git")]
    [InlineData("tool locations")]
    [InlineData("tool --help")]
    [InlineData("tool frobnicate")]
    [InlineData("tool use gh managed")]
    [InlineData("tool use gh managed 2.62.0")]
    [InlineData("tool download gh")]
    [InlineData("tool update gh")]
    // The agents' rules, proposals and settings read, a trust asked or dry-run, and the usage (PUSH1f2d).
    [InlineData("agent")]
    [InlineData("agent rules")]
    [InlineData("agent rules list")]
    [InlineData("agent rules list --workspace work")]
    [InlineData("agent rules proposals")]
    [InlineData("agent settings claude-code")]
    [InlineData("agent settings claude-code --account work")]
    [InlineData("agent trust claude-code D:/work/engine")]
    [InlineData("agent trust claude-code D:/work/engine --yes --dry-run")]
    // What an agent runs as, read (PUSH1f2e).
    [InlineData("agent which claude-code")]
    [InlineData("agent profile")]
    [InlineData("agent profile list")]
    [InlineData("agent profile use claude-code")]
    [InlineData("agent profile use claude-code --workspace work")]
    [InlineData("agent profile use claude-code order --help")]
    [InlineData("agent profile order claude-code account-1 --help")]
    [InlineData("agent which claude-code-acp --workspace work --json")]
    // This machine's agents and their accounts, read, by its word and by none (PUSH1f2e4).
    [InlineData("agent list")]
    [InlineData("agent list --help")]
    [InlineData("agent --help")]
    [InlineData("agent rules allow WebFetch --help")]
    [InlineData("agent frobnicate")]
    // The long verbs (PUSH1f2e5, the alone-path design §1.4): an install holds none at all, its installer writing the machine; a key
    // holds none at its top, taking the lease itself to publish.
    [InlineData("agent install claude-code")]
    [InlineData("agent key claude-code")]
    [InlineData("agent key claude-code --account work")]
    public void A_read_or_another_writers_verb_holds_none(string words) => Assert.False(VerbLeases.Holds(words.Split(' ')), words);

    [Fact]
    public void No_words_hold_nothing() => Assert.False(VerbLeases.Holds([]));

    /// <summary>
    /// A file `DAORIS_DRIVER_CONFIG` names is no instance's (D194 point 2, the alone-path design §1.2): the driver family acts on that
    /// file's home alone, holding nothing; with none named, the same change holds the home.
    /// </summary>
    [Fact]
    public void A_driver_choice_on_a_named_file_holds_nothing()
    {
        string[] words = ["driver", "drive", "engine"];
        Assert.False(VerbLeases.HoldsHere(words, name => name == DriverConfig.PathVariable ? "X:/daoris/elsewhere/driver.json" : null));
        Assert.True(VerbLeases.HoldsHere(words, _ => null));
        // Another family's verb is not the driver family's file: a named driver file isolates only what it names.
        Assert.True(VerbLeases.HoldsHere(["trees", "land", "s-1"], name => name == DriverConfig.PathVariable ? "X:/daoris/elsewhere/driver.json" : null));
    }

    /// <summary>
    /// A file `DAORIS_HARNESS_CONFIG` names is no instance's (PUSH1f2d, the alone-path design §1.2): an account's settings and a trust on
    /// its home act alone, holding nothing; the rules and their proposals are `DAORIS_HOME`'s whatever it names, so they hold that home.
    /// </summary>
    [Fact]
    public void An_agent_setting_on_a_named_wiring_file_holds_nothing_and_the_rules_hold_the_home()
    {
        static string? Named(string name) => name == HarnessSettings.PathVariable ? "X:/daoris/elsewhere/harnesses.json" : null;
        Assert.False(VerbLeases.HoldsHere(["agent", "settings", "claude-code", "--account", "work", "model", "opus"], Named));
        Assert.False(VerbLeases.HoldsHere(["agent", "trust", "claude-code", "D:/work/engine", "--yes"], Named));
        Assert.True(VerbLeases.HoldsHere(["agent", "rules", "accept", "p0000007"], Named));
        Assert.True(VerbLeases.HoldsHere(["agent", "rules", "allow", "WebFetch"], Named));
        Assert.True(VerbLeases.HoldsHere(["agent", "settings", "claude-code", "--account", "work", "model", "opus"], _ => null));
        // A named wiring file isolates only what it names: the driver family's choices are not its.
        Assert.True(VerbLeases.HoldsHere(["driver", "drive", "engine"], Named));
    }

    /// <summary>A lease names a verb by its own words, never an argument: a session, a branch or a path stays out of it.</summary>
    [Theory]
    [InlineData("trees land s-1", "daoris-driver trees land")]
    [InlineData("ask --pause a-1", "daoris-driver ask --pause")]
    [InlineData("setup some-repository", "daoris-driver setup")]
    [InlineData("trees remove some/tree/path", "daoris-driver trees remove")]
    [InlineData("register", "daoris-driver register")]
    [InlineData("help mode full", "daoris-driver help mode")]
    [InlineData("heavy engine build=x", "daoris-driver heavy")]
    [InlineData("driver drive engine", "daoris-driver driver drive")]
    [InlineData("driver retry 12 --at 2", "daoris-driver driver retry")]
    [InlineData("driver workflow new docs --from merge-after-accept", "daoris-driver driver workflow")]
    [InlineData("driver work-cycle engine quest cycle.json", "daoris-driver driver work-cycle")]
    [InlineData("tool use git system", "daoris-driver tool use")]
    [InlineData("tool look", "daoris-driver tool look")]
    [InlineData("agent rules accept p0000007", "daoris-driver agent rules")]
    [InlineData("agent settings claude-code model opus", "daoris-driver agent settings")]
    [InlineData("agent trust claude-code D:/work/engine --yes", "daoris-driver agent trust")]
    [InlineData("agent unpin claude-code", "daoris-driver agent unpin")]
    [InlineData("agent profile order claude-code account-1", "daoris-driver agent profile")]
    [InlineData("agent list", "daoris-driver agent list")]
    [InlineData("agent key claude-code", "daoris-driver agent key")]
    [InlineData("agent install claude-code", "daoris-driver agent install")]
    public void A_lease_names_the_verb_and_never_its_arguments(string words, string named) =>
        Assert.Equal(named, VerbLeases.Name(words.Split(' ')));
}
