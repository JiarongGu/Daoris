using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>What a plugin's manifest declares of landing (LANDNODE1e): that it keeps a delivery key, and the modes beyond a pull request it serves.</summary>
public sealed record PluginLandDeclares(bool Key, IReadOnlyList<string> Modes)
{
    public static PluginLandDeclares None { get; } = new(false, []);
}
