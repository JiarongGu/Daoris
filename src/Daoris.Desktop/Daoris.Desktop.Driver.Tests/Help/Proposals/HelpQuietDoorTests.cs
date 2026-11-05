using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris's <c>quiet</c> door of the <c>setting</c> kind: the one quiet setting proposed as <c>daoris settings quiet</c> takes it,
/// applied as the terminal writes it, and refused out of its bounds.
/// </summary>
public sealed class HelpQuietDoorTests : HelpProposalsFixture
{
    [Theory]
    [InlineData("15")]
    [InlineData("120")]
    public void It_is_planned_as_the_terminal_says_it_and_applied_as_the_terminal_writes_it(string value)
    {
        var plan = HelpProposals.Plan(Setting("quiet", null, null, value), DriverConfig.Empty, Facts);

        Assert.Null(plan.Refusal);
        Assert.Equal($"daoris settings quiet {value}", plan.Terminal);
        Assert.Contains($"runs nothing for {value} minutes", plan.Describe);
        Assert.Contains("none is ever stopped for running long", plan.Describe);
        Assert.Equal(int.Parse(value, System.Globalization.CultureInfo.InvariantCulture), plan.Apply!(DriverConfig.Empty).QuietMinutes);
    }

    [Theory]
    [InlineData("4")]
    [InlineData("121")]
    [InlineData("soon")]
    [InlineData("")]
    public void A_value_out_of_its_bounds_is_refused(string value) =>
        Assert.Equal($"`quiet {value}` is not a change the driver makes.", HelpProposals.Plan(Setting("quiet", null, null, value), DriverConfig.Empty, Facts).Refusal);

    [Fact]
    public void It_is_a_machine_door_naming_no_repository_or_workspace() =>
        Assert.Contains("quiet", HelpProposals.DoorsOf("setting"));
}
