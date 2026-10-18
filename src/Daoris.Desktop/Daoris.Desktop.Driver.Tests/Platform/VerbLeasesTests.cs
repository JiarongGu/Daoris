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
    [InlineData("opinion myself 12")]
    [InlineData("setup some-repository")]
    [InlineData("setup --workspace w --pause")]
    [InlineData("register")]
    [InlineData("sync")]
    [InlineData("sync dismiss 12")]
    [InlineData("plugins install a.nupkg")]
    [InlineData("plugins new id --point p")]
    [InlineData("update --now")]
    [InlineData("workflow keep s-1")]
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
    // A conversation in the terminal, until RPC1g; and the loop, which holds it as an instance.
    [InlineData("chat --repository r")]
    [InlineData("help resume c-1")]
    [InlineData("sessions start-from s-1")]
    [InlineData("sessions hand-over s-1 --agent codex")]
    [InlineData("drive --once")]
    [InlineData("--until-idle")]
    public void A_read_or_another_writers_verb_holds_none(string words) => Assert.False(VerbLeases.Holds(words.Split(' ')), words);

    [Fact]
    public void No_words_hold_nothing() => Assert.False(VerbLeases.Holds([]));

    /// <summary>A lease names a verb by its own words, never an argument: a session, a branch or a path stays out of it.</summary>
    [Theory]
    [InlineData("trees land s-1", "daoris-driver trees land")]
    [InlineData("ask --pause a-1", "daoris-driver ask --pause")]
    [InlineData("setup some-repository", "daoris-driver setup")]
    [InlineData("trees remove some/tree/path", "daoris-driver trees remove")]
    [InlineData("register", "daoris-driver register")]
    public void A_lease_names_the_verb_and_never_its_arguments(string words, string named) =>
        Assert.Equal(named, VerbLeases.Name(words.Split(' ')));
}
