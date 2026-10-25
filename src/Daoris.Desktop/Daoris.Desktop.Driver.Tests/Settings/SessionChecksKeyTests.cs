using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Which checks a repository's sessions run, as <c>driver.json</c> keeps them (FOUNDATION1 mod-iii): the settings' own choices and
/// reading, which the checks answer through, so the key is read and kept alike wherever a choice is named.
/// </summary>
public sealed class SessionChecksKeyTests
{
    [Fact]
    public void The_checks_answer_through_the_key_for_their_names_and_choice()
    {
        Assert.Equal(SessionChecksKey.Key, SessionChecks.Key);
        Assert.Equal(["light", "heavy-at-end"], SessionChecksKey.Choices);
        Assert.Equal(SessionChecksKey.Choices, SessionChecks.Choices);
        Assert.Equal("heavy-at-end", SessionChecksKey.Choice("  HEAVY-AT-END "));
        Assert.Equal(SessionChecksKey.Choice("Light"), SessionChecks.Choice("Light"));
        Assert.Null(SessionChecks.Choice("sometimes"));
    }

    [Fact]
    public void A_map_keeps_the_first_readable_choice_of_a_name_in_its_own_spelling()
    {
        using var document = JsonDocument.Parse("""{"sessionChecks":{" a ":"LIGHT","A":"heavy-at-end","b":"sometimes","c":7,"":"light"}}""");

        var map = SessionChecksKey.Map(document.RootElement);

        Assert.Equal(["a"], map.Keys);
        Assert.Equal("light", map["a"]);
    }
}
