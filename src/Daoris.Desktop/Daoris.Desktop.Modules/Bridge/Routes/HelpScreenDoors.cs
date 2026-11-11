using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>The doors Ask Daoris's Apply goes through, each the code a screen's own route runs.</summary>
internal sealed class HelpScreenDoors(DriverContext context) : IHelpDoors
{
    public void Change(Func<DriverConfig, DriverConfig> edit) => context.Change(edit);
}
