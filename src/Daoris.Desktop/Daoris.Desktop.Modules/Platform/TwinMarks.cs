namespace Daoris.Desktop;

/// <summary>
/// A setting the window writes that no <c>daoris</c> verb writes, with <paramref name="why"/>: what keeps it the window's alone, so
/// Ask Daoris and a session are told there is no door rather than finding none. A setting a verb writes says nothing here: its
/// verb's table names the route (<c>SettingDoorsCensusTests</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
internal sealed class NoTwinAttribute(string route, string why) : Attribute
{
    public string Route { get; } = route;

    public string Why { get; } = why;
}
