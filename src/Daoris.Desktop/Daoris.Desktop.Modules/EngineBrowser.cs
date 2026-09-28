using System.Globalization;
using System.Net.WebSockets;
using System.Text.Json;

namespace Daoris.Desktop;

/// <summary>
/// What starts Daoris's own browser on the engine it ships (D85, CHR3), and how it is found and read,
/// kept out of both processes so it is tested like everything else here: the shell writes these
/// arguments, and `daoris-browser` parses them with the same code.
/// </summary>
/// <remarks>
/// <para><b>A process of its own, by measurement.</b> The engine's debug port reaches every page in
/// its process, the shell's page and its bridge included
/// (`docs/2026-09-28-chromium-embedding-evidence.md` §1). So the browser is another executable, and the
/// shell's process has no port.</para>
///
/// <para><b>The engine's own window.</b> A tab an agent opens over CDP joins a Chromium window of the
/// engine's own (§2 there), so the browser IS those windows, with their tabs, history and devtools,
/// rather than Daoris's chrome around a control.</para>
/// </remarks>
public static class EngineBrowser
{
    public const string ExecutableName = "daoris-browser.exe";

    /// <summary>Its folder inside an install, beside the other supporting binaries under `app/`.</summary>
    public static readonly string[] InstallHome = ["app", "daoris-browser"];

    /// <summary>
    /// Its profile, under the home (D63). Not the WebView2 window's `browser/profile`: a different
    /// engine's files, and the person signs in here once more.
    /// </summary>
    public static string ProfileFolder(string home) => Path.Combine(home, "browser", "engine");

    /// <summary>
    /// The profile the engine's windows actually use, inside <see cref="ProfileFolder"/>: its
    /// `Default`, as Chrome's own profiles are named. Measured (2026-09-28): a window made over CDP
    /// ignored a cache path set elsewhere and used this one, so the engine is told this one too, and
    /// its preferences, bookmarks and cookies are all here.
    /// </summary>
    public static string ProfileDirectory(string profileFolder) => Path.Combine(profileFolder, "Default");

    /// <summary>
    /// The home a profile folder is under — <see cref="ProfileFolder"/> undone — where the browser finds
    /// Daoris's favorites and its settings at each start.
    /// </summary>
    public static string HomeOf(string profileFolder) =>
        Path.GetDirectoryName(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(profileFolder))!)!;

    /// <summary>
    /// The engine's language, from the person's: one of the two the install keeps (en-US, zh-CN), so
    /// the engine never asks for a locale file that was left out.
    /// </summary>
    public static string Locale(string uiCulture) =>
        uiCulture.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh-CN" : "en-US";

    /// <summary>
    /// Where `daoris-browser.exe` is, in the order they deserve trust: what the install carries, a
    /// hand-assembled folder beside the shell, then the workspace build for development. The same
    /// order as <c>ServiceHostLocator</c>, for the same reason (the first-deployment case study, 2a).
    /// </summary>
    public static IReadOnlyList<string> Candidates(string baseDirectory)
    {
        var candidates = new List<string>
        {
            Path.Combine([baseDirectory, .. InstallHome, ExecutableName]),
            Path.Combine(baseDirectory, "daoris-browser", ExecutableName),
        };

        var directory = new DirectoryInfo(baseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json")))
        {
            directory = directory.Parent;
        }

        if (directory is not null)
        {
            var project = Path.Combine(directory.FullName, "src", "Daoris.Desktop", "Daoris.Desktop.Browser");
            foreach (var flavour in new[] { "Debug", "Release" })
            {
                candidates.Add(Path.Combine(project, "bin", flavour, "net10.0-windows", ExecutableName));
            }
        }

        return candidates;
    }

    /// <summary>The first candidate that exists, or null: a browser this build cannot start, said, never guessed.</summary>
    public static string? Locate(string baseDirectory) => Candidates(baseDirectory).FirstOrDefault(File.Exists);

    /// <summary>
    /// The pages an engine's `/json/list` names, in its order, as target ids. Anything that is not a page
    /// (a worker, the browser itself) is not a window the person sees.
    /// </summary>
    public static IReadOnlyList<string> Pages(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return [];
        return document.RootElement.EnumerateArray()
            .Where(target => target.ValueKind == JsonValueKind.Object
                             && target.TryGetProperty("type", out var type) && type.GetString() == "page"
                             && target.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
            .Select(target => target.GetProperty("id").GetString()!)
            .ToList();
    }
}

/// <summary>What `daoris-browser` is started with. <see cref="Parse"/> and <see cref="ToArguments"/> are one contract.</summary>
/// <param name="Profile">The engine's root folder, under the home. Required: with none it would pick one under the user profile (D63).</param>
/// <param name="Port">Its loopback debug port, which is the browser's endpoint.</param>
/// <param name="Parent">The shell's process: the browser closes when it does, as the shell's window did.</param>
/// <param name="Background">Open its first window without taking the person's focus, as a session asks.</param>
public sealed record EngineBrowserOptions(string Profile, int Port, int? Parent, bool Background)
{
    public IReadOnlyList<string> ToArguments()
    {
        var arguments = new List<string> { "--profile", Profile, "--port", Port.ToString(CultureInfo.InvariantCulture) };
        if (Parent is { } parent) arguments.AddRange(["--parent", parent.ToString(CultureInfo.InvariantCulture)]);
        if (Background) arguments.Add("--background");
        return arguments;
    }

    /// <summary>The options, or null with the reason. Anything unknown is refused rather than ignored.</summary>
    public static EngineBrowserOptions? Parse(IReadOnlyList<string> arguments, out string? problem)
    {
        string? profile = null;
        int? port = null, parent = null;
        var background = false;

        for (var i = 0; i < arguments.Count; i++)
        {
            string? Next() => i + 1 < arguments.Count ? arguments[++i] : null;
            switch (arguments[i])
            {
                case "--profile":
                    profile = Next();
                    break;
                case "--port":
                    port = int.TryParse(Next(), NumberStyles.None, CultureInfo.InvariantCulture, out var p) ? p : -1;
                    break;
                case "--parent":
                    parent = int.TryParse(Next(), NumberStyles.None, CultureInfo.InvariantCulture, out var q) ? q : -1;
                    break;
                case "--background":
                    background = true;
                    break;
                default:
                    problem = $"`{arguments[i]}` is not an argument daoris-browser takes.";
                    return null;
            }
        }

        problem = profile is null || !Path.IsPathFullyQualified(profile)
            ? "--profile <folder> is required, as a full path under the Daoris home."
            : port is not (>= 1024 and <= 65535)
                ? "--port <1024-65535> is required: the loopback port the browser listens on."
                : parent is <= 0
                    ? "--parent must be a process id."
                    : null;
        return problem is null ? new EngineBrowserOptions(profile!, port!.Value, parent, background) : null;
    }
}

/// <summary>
/// The engine's own endpoints, over loopback: whether it answers, its pages, a window opened, one
/// brought forward. What both the shell and `daoris-browser` ask of it; nothing Daoris-specific goes
/// over this wire.
/// </summary>
public sealed class EngineCdp(int port) : IDisposable
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri($"http://127.0.0.1:{port}/"), Timeout = TimeSpan.FromSeconds(5) };

    public async Task<bool> AnswersAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync("json/version", ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>The endpoint's own account of itself (`/json/version`), or null when nothing answers.</summary>
    public async Task<string?> VersionAsync(CancellationToken ct = default)
    {
        try
        {
            return await _http.GetStringAsync("json/version", ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>The page targets, or null when the engine does not answer.</summary>
    public async Task<IReadOnlyList<string>?> PagesAsync(CancellationToken ct = default)
    {
        try
        {
            return EngineBrowser.Pages(await _http.GetStringAsync("json/list", ct).ConfigureAwait(false));
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Bring a page's window forward.</summary>
    public async Task ActivateAsync(string target, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync($"json/activate/{Uri.EscapeDataString(target)}", ct).ConfigureAwait(false);
    }

    /// <summary>A new window on <paramref name="url"/>; in the background when a session asked, not the person.</summary>
    public Task<JsonElement> NewWindowAsync(string url, bool background, CancellationToken ct = default) =>
        BrowserCallAsync("Target.createTarget", new { url, newWindow = true, background }, ct);

    /// <summary>One call on the browser's own socket, answered by its id.</summary>
    public async Task<JsonElement> BrowserCallAsync(string method, object parameters, CancellationToken ct = default)
    {
        using var version = JsonDocument.Parse(await _http.GetStringAsync("json/version", ct).ConfigureAwait(false));
        var socketUrl = version.RootElement.GetProperty("webSocketDebuggerUrl").GetString()!;

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(socketUrl), ct).ConfigureAwait(false);
        var request = JsonSerializer.SerializeToUtf8Bytes(new { id = 1, method, @params = parameters });
        await socket.SendAsync(request, WebSocketMessageType.Text, endOfMessage: true, ct).ConfigureAwait(false);

        var buffer = new byte[64 * 1024];
        while (true)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult received;
            do
            {
                received = await socket.ReceiveAsync(buffer, ct).ConfigureAwait(false);
                if (received.MessageType == WebSocketMessageType.Close) throw new WebSocketException("the engine closed its socket");
                message.Write(buffer, 0, received.Count);
            }
            while (!received.EndOfMessage);

            using var reply = JsonDocument.Parse(message.ToArray());
            if (!reply.RootElement.TryGetProperty("id", out var id) || id.GetInt32() != 1) continue;
            if (reply.RootElement.TryGetProperty("error", out var error))
            {
                throw new InvalidOperationException($"{method}: {error.GetProperty("message").GetString()}");
            }

            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, ct).ConfigureAwait(false);
            return reply.RootElement.GetProperty("result").Clone();
        }
    }

    public void Dispose() => _http.Dispose();
}
