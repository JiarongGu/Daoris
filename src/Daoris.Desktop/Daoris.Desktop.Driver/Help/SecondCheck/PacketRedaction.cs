using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What a packet's text loses before it is sent: an address its credentials, query and fragment, and every secret Daoris holds cut to
/// <c>[key]</c> (<see cref="WiringRedact"/>); a path inside a registered checkout written as its repository's name in brackets and the
/// path within it; the person's home folder as <c>~</c>. A path is matched in each spelling a record holds it: either slash, and a JSON
/// string's doubled backslash.
/// </summary>
public sealed record PacketRedaction(IReadOnlyCollection<string> Secrets, IReadOnlyDictionary<string, string> Checkouts, string? UserHome)
{
    public static PacketRedaction None { get; } = new([], new Dictionary<string, string>(), null);

    public string Apply(string? text)
    {
        var cut = WiringRedact.Text(text ?? "", Secrets)!;
        foreach (var (name, root) in Checkouts.OrderByDescending(each => each.Value.Length)) cut = Rooted(cut, root, $"[{name}]");
        return UserHome is { Length: > 0 } home ? Rooted(cut, home, "~") : cut;
    }

    /// <summary>This machine's: the secrets the wiring reader meets, the registry's checkouts that read, and the person's home folder.</summary>
    public static async Task<PacketRedaction> OfAsync(string home, ServiceClient service, CancellationToken ct)
    {
        var secrets = SystemWiring.Secrets(new WiringWorld
        {
            Home = home,
            BaseDirectory = AppContext.BaseDirectory,
            Process = Environment.GetEnvironmentVariable,
            Account = OperatingSystem.IsWindows() ? name => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User) : null,
            Machine = OperatingSystem.IsWindows() ? name => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine) : null,
        });
        var checkouts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var repository in await RefWatch.RepositoriesAsync(home, service.RegistryAsync, ct).ConfigureAwait(false)) checkouts.TryAdd(repository.Repository, repository.Root);
        }
        catch (Exception error) when (error is DriverException or IOException or HttpRequestException or System.Text.Json.JsonException)
        {
            // A registry that does not answer names no checkout; the home folder is still cut.
        }

        return new PacketRedaction(secrets, checkouts, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    private static string Rooted(string text, string root, string said)
    {
        var trimmed = root.TrimEnd('\\', '/');
        if (trimmed.Length < 3) return text;
        var spellings = new[] { trimmed, trimmed.Replace('\\', '/'), trimmed.Replace('/', '\\'), trimmed.Replace('/', '\\').Replace("\\", "\\\\", StringComparison.Ordinal) };
        foreach (var spelling in spellings.Distinct(StringComparer.Ordinal).OrderByDescending(each => each.Length))
        {
            text = Regex.Replace(text, Regex.Escape(spelling) + @"(?=$|[\\/""'\s:;,)\]}])", said.Replace("$", "$$", StringComparison.Ordinal), RegexOptions.IgnoreCase);
        }

        return text;
    }
}
