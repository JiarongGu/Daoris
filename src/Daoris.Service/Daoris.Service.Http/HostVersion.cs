using System.Reflection;

namespace Daoris.Knowledge.Http;

/// <summary>This host's informational version as the release workflow stamped it, read once: what the status answers as <c>version</c>.</summary>
public static class HostVersion
{
    public static string? Stamped { get; } = typeof(HostVersion).Assembly
        .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
        .OfType<AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion;
}
