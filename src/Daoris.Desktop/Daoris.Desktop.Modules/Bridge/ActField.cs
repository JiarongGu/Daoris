using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>A field only some acts of a route need: the record carries it as optional, and the act that wants it refuses it missing.</summary>
internal static class ActField
{
    public static string Needs(string? value, string name) =>
        value ?? throw new DriverException($"`{name}` is required for this act.");

    public static bool Needs(bool? value, string name) =>
        value ?? throw new DriverException($"`{name}` is required for this act.");
}
