using Daoris.Driver;

namespace Daoris.Desktop;

internal sealed class HelpScreenDoors(DriverContext context) : IHelpDoors
{
    public void Change(Func<DriverConfig, DriverConfig> edit) => context.Change(edit);
}
