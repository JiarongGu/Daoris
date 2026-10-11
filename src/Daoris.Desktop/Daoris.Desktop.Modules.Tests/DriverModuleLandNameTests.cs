using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// LANDNAME1a (D145): the <c>LANDING</c> plan and the <c>LAND_SESSION_TREE</c> press read an optional <c>branch</c> the page
/// enters, which names the branch in place of the rule's. Blank or absent keeps the rule's name, as the page sent before.
/// What the landing does with the name is held over real git by the driver's <c>LandingTests</c>.
/// </summary>
public sealed class DriverModuleLandNameTests
{
    private static IpcRequest Request(object payload) => new()
    {
        Id = "r1",
        Module = "driver",
        Type = "LAND_SESSION_TREE",
        Payload = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(payload)),
    };

    private static readonly LandingSubject Subject = new("s1a2b3c4", "0fda18", "Fix");

    [Fact]
    public void An_entered_branch_is_carried_to_the_landing()
    {
        var subject = DriverModule.Entering(Subject, Request(new { id = "s1a2b3c4", branch = "my/own-name" }));

        Assert.Equal("my/own-name", subject.Entered);
        Assert.Equal(Subject.Session, subject.Session);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_branch_keeps_the_rules_name(string blank) =>
        Assert.Null(DriverModule.Entering(Subject, Request(new { id = "s1a2b3c4", branch = blank })).EnteredName);

    [Fact]
    public void An_absent_or_non_text_branch_keeps_the_rules_name()
    {
        Assert.Null(DriverModule.Entering(Subject, Request(new { id = "s1a2b3c4" })).EnteredName);
        Assert.Null(DriverModule.Entering(Subject, Request(new { id = "s1a2b3c4", branch = 7 })).EnteredName);
    }
}
