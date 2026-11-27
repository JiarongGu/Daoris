using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A refusal's sentence as the machine log keeps it (<see cref="UpdateRefusalWords"/>): one line, its words, no path of the machine's.</summary>
public sealed class UpdateRefusalWordsTests
{
    [Fact]
    public void The_sentence_is_kept_on_one_line_with_the_install_and_the_home_named_not_written()
    {
        var install = Path.Combine(TestHomes.Root, "an-install");
        var home = Path.Combine(install, "data");
        var sentence = $"the copy of the home could not be made: The process cannot access the file '{Path.Combine(home, "checks", "broker.lock")}'\r\n"
            + $"because it is being used by another process (copy in {Path.Combine(install, "update", "probe", "c1")}).";

        var logged = UpdateRefusalWords.Logged(sentence, install, home);

        Assert.Equal(
            $"the copy of the home could not be made: The process cannot access the file '<home>{Path.DirectorySeparatorChar}checks{Path.DirectorySeparatorChar}broker.lock' "
            + $"because it is being used by another process (copy in <install>{Path.DirectorySeparatorChar}update{Path.DirectorySeparatorChar}probe{Path.DirectorySeparatorChar}c1).",
            logged);
        Assert.DoesNotContain(TestHomes.Root, logged, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_sentence_with_no_install_names_the_home_alone()
    {
        var home = Path.Combine(TestHomes.Root, "a-home");

        Assert.Equal("nothing is staged beside <home>.", UpdateRefusalWords.Logged($"nothing is staged beside {home}.", null, home));
    }
}
