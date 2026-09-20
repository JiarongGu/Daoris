using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Where this machine's remote is — machine-local configuration, never per-repository (D47 §9):
/// `~/.daoris/remote.json` holding `{ "url": "...", "key": "dk_..." }`, with the environment
/// overriding. The FILE is the contract this component shares with the service's hosts; the driver
/// deliberately reads it with its own code, because it links against no service assembly — the
/// service-side twin is `RemoteConfig`, and each carries a test table the other must keep matching.
/// </summary>
public sealed record RemoteTarget(string Url, string Key)
{
    public const string UrlVariable = "DAORIS_REMOTE_URL";
    public const string KeyVariable = "DAORIS_REMOTE_KEY";
    public const string PathVariable = "DAORIS_REMOTE_CONFIG";

    /// <summary>The machine's remote, if it has one. Absence is the default and it is silent (D21).</summary>
    public static RemoteTarget? Load() => Load(
        Environment.GetEnvironmentVariable,
        Environment.GetEnvironmentVariable(PathVariable)
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".daoris", "remote.json"));

    /// <summary>
    /// The testable shape: the same judgement over injected surroundings. Either environment variable
    /// present means the environment IS the answer, whole — a half-set pair is no remote, never a mix
    /// of an env URL with the file's key, which would quietly aim one machine's key at another's host.
    /// </summary>
    public static RemoteTarget? Load(Func<string, string?> environment, string path)
    {
        var url = environment(UrlVariable);
        var key = environment(KeyVariable);

        if (string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(key) && File.Exists(path))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                url = Text(root, "url");
                key = Text(root, "key");
            }
            catch (JsonException)
            {
                return null;
            }
        }

        return string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key)
            ? null
            : new RemoteTarget(url.TrimEnd('/'), key);
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
