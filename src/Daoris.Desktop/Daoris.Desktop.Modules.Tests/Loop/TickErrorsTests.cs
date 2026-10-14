using System.Net.Sockets;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// What a failed tick tells the page (UX5 U30). With the machine's service stopped, the shell toasted
/// the .NET socket's own words on every poll, <i>No connection could be made because the target
/// machine actively refused it. (127.0.0.1:5188)</i>, beside the page's own sentence for the same
/// fact.
/// </summary>
public sealed class TickErrorsTests
{
    private static HttpRequestException Refused() => new(
        "No connection could be made because the target machine actively refused it. (127.0.0.1:5188)",
        new SocketException(10061));

    [Fact]
    public void A_refused_connection_is_named_so_the_page_words_it_and_never_in_the_sockets_words()
    {
        var said = new TickErrors().Failed(Refused());

        Assert.NotNull(said);
        Assert.Equal("SERVICE_UNREACHABLE", said.Code);
        Assert.DoesNotContain("actively refused", said.Message);
        Assert.DoesNotContain("127.0.0.1", said.Message);
    }

    [Fact]
    public void The_same_failure_is_said_once_until_a_tick_runs_again()
    {
        var errors = new TickErrors();

        Assert.NotNull(errors.Failed(Refused()));
        Assert.Null(errors.Failed(Refused()));
        Assert.Null(errors.Failed(Refused()));

        errors.Ran();
        Assert.NotNull(errors.Failed(Refused()));
    }

    [Fact]
    public void Any_other_failure_keeps_the_drivers_own_words_and_is_news_when_it_changes()
    {
        var errors = new TickErrors();

        var torn = errors.Failed(new DriverException("driver.json could not be read: a comma is missing."));
        Assert.NotNull(torn);
        Assert.Null(torn.Code);
        Assert.Equal("driver.json could not be read: a comma is missing.", torn.Message);

        Assert.NotNull(errors.Failed(Refused()));
    }
}
