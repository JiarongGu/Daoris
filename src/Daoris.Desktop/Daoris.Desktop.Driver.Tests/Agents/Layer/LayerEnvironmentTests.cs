using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

public sealed class LayerEnvironmentTests
{
    [Fact]
    public void A_variable_the_shell_removed_is_handed_as_a_null_entry_so_the_seat_does_not_inherit_it_back()
    {
        var info = new ProcessStartInfo("claude");
        info.Environment["KEPT_BY_THE_SHELL"] = "1";
        var inherited = info.Environment.Keys.First(name => !name.Equals("KEPT_BY_THE_SHELL", StringComparison.OrdinalIgnoreCase));
        info.Environment.Remove(inherited);

        var composed = LayerEnvironment.Of(info);

        Assert.Equal("1", composed["KEPT_BY_THE_SHELL"]);
        Assert.True(composed.ContainsKey(inherited));
        Assert.Null(composed[inherited]);
    }

    [Fact]
    public void A_held_start_hands_its_seat_the_removed_variables_as_nulls()
    {
        var info = new ProcessStartInfo("claude");
        var inherited = info.Environment.Keys.First();
        info.Environment.Remove(inherited);

        var environment = new LayerHold(null!, new TransportStart(info, StartClass.Background)).Environment;

        Assert.True(environment.ContainsKey(inherited));
        Assert.Null(environment[inherited]);
    }
}
