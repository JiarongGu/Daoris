using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>The remotes file as the wiring answers it: each target redacted, and why a file did not read.</summary>
internal static class WiringRemotes
{
    internal sealed record RemotesRead(IReadOnlyDictionary<string, RemoteTarget>? Map, string From, string? Why);

    internal static RemotesRead LoadRemotes(WiringWorld world)
    {
        if (RemoteTarget.FromEnvironment(world.Process) is { } fromEnvironment) return new(fromEnvironment, "environment", null);
        var path = world.Process(WiringVariables.RemotesFile) is { Length: > 0 } named ? named
            : world.Home is { } home ? DaorisHome.Entry(home, HomeEntry.Remotes) : null;
        if (path is null || !File.Exists(path)) return new(new Dictionary<string, RemoteTarget>(), "file", null);
        try
        {
            var text = AtomicFile.ReadText(path);
            try
            {
                using var _ = JsonDocument.Parse(text);
            }
            catch (JsonException)
            {
                return new(null, "file", "unparsed");
            }

            return new(RemoteTarget.Parse(text), "file", null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new(null, "file", "unreadable");
        }
    }

    internal static (JsonArray Rows, string? Why) Remotes(WiringWorld world)
    {
        var read = LoadRemotes(world);
        var rows = new JsonArray();
        foreach (var (workspace, target) in (read.Map ?? new Dictionary<string, RemoteTarget>()).OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var url = WiringRedact.Address(target.Url);
            rows.Add(new JsonObject
            {
                ["workspace"] = workspace,
                ["url"] = url,
                ["urlWhy"] = url is null ? "malformed" : null,
                ["keySet"] = !string.IsNullOrEmpty(target.Key),
                ["keyPrefix"] = WiringRedact.Prefix(target.Key),
                ["from"] = read.From,
            });
        }

        return (rows, read.Why);
    }
}
