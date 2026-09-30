using System.Globalization;
using System.Net.WebSockets;
using System.Text.Json;

namespace Daoris.Desktop;

/// <summary>
/// What starts Daoris's own browser on the engine it ships (D85, CHR3), and how it is told apart and
/// read, kept out of both processes so it is tested like everything else here: the shell writes these
/// arguments, and `daoris-browser` parses them with the same code.
/// </summary>
/// <remarks>
/// <para><b>A process of its own, by measurement.</b> The engine's debug port reaches every page in
/// its process, the shell's page and its bridge included
/// (`docs/2026-09-28-chromium-embedding-evidence.md` §1). So the browser is another process, and the
/// shell's process has no port.</para>
///
/// <para><b>The engine's own window.</b> A tab an agent opens over CDP joins a Chromium window of the
/// engine's own (§2 there), so the browser IS those windows, with their tabs, history and devtools,
/// rather than Daoris's chrome around a control.</para>
///
/// <para><b>The application's own executable</b> (CHR8, D99). `daoris-browser` is no longer an
/// executable of its own with a CEF of its own: it is `Daoris.Desktop.exe` started with
/// <see cref="Argument"/> first, whose <c>Main</c> hands the process to the kit's
/// <c>ChromiumBrowserProcess.Run</c>. So an install carries one Chromium.</para>
/// </remarks>
public static class EngineBrowser
{
    /// <summary>
    /// The argument that makes the application's executable the browser, first on its command line
    /// (CHR8). A twin of `tools/processes.mjs`'s <c>BROWSER_ARGUMENT</c>, which tells the browser from
    /// the application and the engine's processes; `desktop-tool.test.ts` reads both.
    /// </summary>
    public const string Argument = "--daoris-browser";

    /// <summary>
    /// Whether this start of the application is the browser: <see cref="Argument"/> is its FIRST
    /// argument. First, because Chromium starts its renderer, GPU and utility processes from the same
    /// executable (`--type=` first), and those belong to whichever Chromium started them, not here.
    /// </summary>
    public static bool IsBrowserProcess(IReadOnlyList<string> arguments) =>
        arguments.Count > 0 && string.Equals(arguments[0], Argument, StringComparison.Ordinal);

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
    /// The engine's log in the profile folder: the kit names it (`ChromiumBrowserProcessOptions.UserDataFolder`),
    /// and a browser that will not start says where to look.
    /// </summary>
    public const string LogName = "cef.log";

    /// <summary>Where the machine log says a failure making the browser's first window happened (LOG2b).</summary>
    public const string FirstWindowPlace = "the browser's first window";

    /// <summary>Where it says a failure watching for the shell that started the browser happened (LOG2b).</summary>
    public const string ShellWatchPlace = "the browser's watch on the shell";

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
/// <remarks>
/// <b>The same four, spelled as Chromium reads a switch</b> (CHR8, D99): the browser is the
/// application's executable now, whose command line Chromium reads too, and it takes a switch's value
/// only after <c>=</c>. So each is one argument, <c>--daoris-&lt;name&gt;=&lt;value&gt;</c>, behind
/// <see cref="EngineBrowser.Argument"/>; a value given as the next word would reach Chromium as a loose
/// argument, and the prefix keeps every name clear of Chromium's own.
/// </remarks>
public sealed record EngineBrowserOptions(string Profile, int Port, int? Parent, bool Background)
{
    private const string ProfileSwitch = "--daoris-profile";
    private const string PortSwitch = "--daoris-port";
    private const string ParentSwitch = "--daoris-parent";
    private const string BackgroundSwitch = "--daoris-background";

    /// <summary>The whole command line after the executable: <see cref="EngineBrowser.Argument"/> first, then the options.</summary>
    public IReadOnlyList<string> ToArguments()
    {
        var arguments = new List<string>
        {
            EngineBrowser.Argument,
            $"{ProfileSwitch}={Profile}",
            $"{PortSwitch}={Port.ToString(CultureInfo.InvariantCulture)}",
        };
        if (Parent is { } parent) arguments.Add($"{ParentSwitch}={parent.ToString(CultureInfo.InvariantCulture)}");
        if (Background) arguments.Add(BackgroundSwitch);
        return arguments;
    }

    /// <summary>The options, or null with the reason. Anything unknown is refused rather than ignored.</summary>
    public static EngineBrowserOptions? Parse(IReadOnlyList<string> arguments, out string? problem)
    {
        if (!EngineBrowser.IsBrowserProcess(arguments))
        {
            problem = $"{EngineBrowser.Argument} comes first: it is what makes this start the browser.";
            return null;
        }

        string? profile = null;
        int? port = null, parent = null;
        var background = false;

        foreach (var argument in arguments.Skip(1))
        {
            var equals = argument.IndexOf('=', StringComparison.Ordinal);
            var name = equals < 0 ? argument : argument[..equals];
            var value = equals < 0 ? null : argument[(equals + 1)..];
            switch (name)
            {
                case ProfileSwitch when value is not null:
                    profile = value;
                    break;
                case PortSwitch when value is not null:
                    port = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var p) ? p : -1;
                    break;
                case ParentSwitch when value is not null:
                    parent = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var q) ? q : -1;
                    break;
                case BackgroundSwitch when value is null:
                    background = true;
                    break;
                default:
                    problem = $"`{argument}` is not an argument daoris-browser takes.";
                    return null;
            }
        }

        problem = profile is null || !Path.IsPathFullyQualified(profile)
            ? $"{ProfileSwitch}=<folder> is required, as a full path under the Daoris home."
            : port is not (>= 1024 and <= 65535)
                ? $"{PortSwitch}=<1024-65535> is required: the loopback port the browser listens on."
                : parent is <= 0
                    ? $"{ParentSwitch} must be a process id."
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

    /// <summary>
    /// The browser's first window (CHR3): made over this port once it answers, the way an agent's windows
    /// are, in the background when a session asked. Throws what went wrong, a port that never answered
    /// within <paramref name="portLimit"/> as a <see cref="TimeoutException"/>; cancelled only when
    /// <paramref name="ct"/> is, because the shell or the engine went before there was a window to make.
    /// </summary>
    /// <remarks>
    /// In the modules rather than in `daoris-browser`, so it is tested (LOG2b): the browser starts it and
    /// does not await it, and hands it to the log to observe.
    /// </remarks>
    public async Task FirstWindowAsync(bool background, TimeSpan portLimit, CancellationToken ct = default)
    {
        try
        {
            var deadline = DateTime.UtcNow + portLimit;
            while (!await AnswersAsync(ct).ConfigureAwait(false))
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException(
                        $"its port did not answer within {portLimit.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture)} seconds");
                }

                await Task.Delay(100, ct).ConfigureAwait(false);
            }

            await NewWindowAsync("chrome://newtab/", background, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException error) when (!ct.IsCancellationRequested)
        {
            // The client's own timeout, not the shell going: a failure to say, never a quiet cancellation.
            throw new TimeoutException("the engine did not answer the call that makes the window", error);
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

    /// <summary>
    /// A new tab on <paramref name="url"/> in the window in front, as an agent's tab is made (BRW7), and
    /// its target id, which <see cref="ActivateAsync"/> brings forward.
    /// </summary>
    public async Task<string> NewTabAsync(string url, CancellationToken ct = default)
    {
        var created = await BrowserCallAsync("Target.createTarget", new { url }, ct).ConfigureAwait(false);
        return created.GetProperty("targetId").GetString()
            ?? throw new InvalidOperationException("Target.createTarget answered no target id.");
    }

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
