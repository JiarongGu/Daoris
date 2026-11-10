using System.Collections;
using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// A start's environment as its shell composed it, for a process the launcher starts from the driver's own: each variable the shell
/// removed is handed as a null entry, so the child does not inherit it back.
/// </summary>
internal static class LayerEnvironment
{
    public static Dictionary<string, string?> Of(ProcessStartInfo info)
    {
        var composed = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in info.Environment) composed[name] = value;
        foreach (DictionaryEntry inherited in System.Environment.GetEnvironmentVariables())
        {
            if (inherited.Key is string name && !info.Environment.ContainsKey(name)) composed[name] = null;
        }

        return composed;
    }
}
