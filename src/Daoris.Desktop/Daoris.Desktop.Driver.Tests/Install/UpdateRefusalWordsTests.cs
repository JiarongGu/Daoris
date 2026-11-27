using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A refusal's sentence as the machine log keeps it (<see cref="UpdateRefusalWords"/>): one line, bounded, the install's and the home's
/// paths named, an email said as one, and the rest as the build said it.
/// </summary>
public sealed class UpdateRefusalWordsTests
{
    [Fact]
    public void The_sentence_is_kept_on_one_line_with_the_install_and_the_home_named_not_written()
    {
        var install = Path.Combine(TestHomes.Root, "an-install");
        var home = Path.Combine(install, "data");
        // A sentence's words, no home read: the seat's path as a build's message writes it.
        var seat = string.Join(Path.DirectorySeparatorChar, home, "checks", "broker.lock");
        var sentence = $"the copy of the home could not be made: The process cannot access the file '{seat}'\r\n"
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

    [Fact]
    public void A_path_is_named_only_where_it_ends_on_a_separator_in_either_slash_and_in_the_long_form()
    {
        var home = Path.Combine(TestHomes.Root, "an-install", "data");
        var forward = home.Replace('\\', '/');

        var logged = UpdateRefusalWords.Logged($"a: {home}2{Path.DirectorySeparatorChar}x; b: {forward}/checks; c: \\\\?\\{home}\\sessions; d: `{home}`", null, home);

        Assert.Equal($"a: {home}2{Path.DirectorySeparatorChar}x; b: <home>/checks; c: <home>\\sessions; d: `<home>`", logged);
    }

    [Fact]
    public void A_path_that_is_no_path_an_email_and_a_long_sentence_never_make_a_refusal_a_crash()
    {
        var home = Path.Combine(TestHomes.Root, "a-home");

        Assert.Equal("seen", UpdateRefusalWords.Logged("seen", "bad\0path", home));
        Assert.Equal("the account `codex/<email>` is no longer listed", UpdateRefusalWords.Logged("the account `codex/someone@example.com` is no longer listed", null, home));
        var logged = UpdateRefusalWords.Logged(new string('x', 5000), null, home);
        Assert.Equal(UpdateRefusalWords.Longest, logged.Length);
        Assert.EndsWith("…", logged);
    }
}
