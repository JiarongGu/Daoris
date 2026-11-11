using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>Ask Daoris's Apply goes through the screen's own door: the driver's file as <c>SET_*</c> edits it, never a second path.</summary>
[Trait(Category.Name, Category.Process)]
public sealed class HelpDoorsTests : Bridge
{
    private DriverModule Module() => new(Bus, new DriverLoop(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0"));

    [Fact]
    public void A_setting_is_the_edit_the_screens_route_makes_to_the_drivers_file()
    {
        Module().HelpDoors(null).Change(config => config.WithDrivable("engine", true));

        Assert.Contains("engine", DriverConfig.Load(DriverConfigPath).Drivable);
    }
}
