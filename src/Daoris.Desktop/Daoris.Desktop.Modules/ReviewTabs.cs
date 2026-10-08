using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>How a request a set-up step's tab makes is answered (REVIEWENV1d).</summary>
public enum ReviewAnswerKind
{
    /// <summary>A file of the build, by its path.</summary>
    File,

    /// <summary>A page of the app, answered with the build's <c>index.html</c>, so the app's own routes work.</summary>
    Index,

    /// <summary>Handed on, to wherever it would go for the person.</summary>
    Pass,

    /// <summary>A path that would leave the build: answered not found, and never handed on.</summary>
    Refused,
}

/// <param name="Path">The file answered with, for <see cref="ReviewAnswerKind.File"/> and <see cref="ReviewAnswerKind.Index"/>.</param>
public sealed record ReviewAnswer(ReviewAnswerKind Kind, string? Path = null);

/// <summary>
/// What a set-up step's tab is answered for each request it makes under the served pattern (REVIEWENV1d; design §2.3 step 3):
/// a request whose path names a file of the build gets that file; any other page request gets the build's <c>index.html</c>; and
/// every other request, the app's calls among them, goes where it would go for the person. Pure, so each case is a test.
/// </summary>
public static class ReviewRequests
{
    /// <summary>
    /// The answer to <paramref name="url"/>, asked by a <paramref name="resourceType"/> as CDP's <c>Fetch</c> names it, with
    /// <paramref name="method"/>. Only a read is answered from the build. A path whose words would leave the build, by <c>..</c>,
    /// a back slash, a drive or a link, is refused: answered not found, never handed to the person's server, which holds the
    /// same address.
    /// </summary>
    public static ReviewAnswer Answer(ReviewServe serve, string url, string resourceType, string method)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var at)
            || !string.Equals(at.GetLeftPart(UriPartial.Authority), serve.Address, StringComparison.OrdinalIgnoreCase)
            || !at.AbsolutePath.StartsWith(serve.Base, StringComparison.Ordinal)
            || method is not ("GET" or "HEAD"))
        {
            return new(ReviewAnswerKind.Pass);
        }

        var rest = Uri.UnescapeDataString(at.AbsolutePath[serve.Base.Length..]);
        if (rest.Contains('\\') || rest.Contains(':') || rest.Contains('\0')
            || rest.Split('/').Any(segment => segment is "." or ".."))
        {
            return new(ReviewAnswerKind.Refused);
        }

        if (rest.Length > 0 && !rest.EndsWith('/'))
        {
            switch (FileIn(serve.Folder, rest))
            {
                case (true, { } file):
                    return new(ReviewAnswerKind.File, file);
                case (false, _):
                    return new(ReviewAnswerKind.Refused);
            }
        }

        if (resourceType == "Document")
        {
            var index = Path.Combine(serve.Folder, "index.html");
            return File.Exists(index) ? new(ReviewAnswerKind.Index, index) : new(ReviewAnswerKind.Refused);
        }

        return new(ReviewAnswerKind.Pass);
    }

    /// <summary>
    /// The file <paramref name="rest"/> names in <paramref name="folder"/>: (true, its path) where it is a regular file reached by
    /// no link; (true, null) where it names nothing there; (false, null) where it would leave the folder.
    /// </summary>
    private static (bool Inside, string? File) FileIn(string folder, string rest)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        var at = root;
        var segments = rest.Split('/');
        for (var index = 0; index < segments.Length; index++)
        {
            if (segments[index].Length == 0) return (true, null);
            at = Path.Combine(at, segments[index]);
            FileSystemInfo found = index < segments.Length - 1 ? new DirectoryInfo(at) : new FileInfo(at);
            if (!found.Exists) return (true, null);
            if (found.Attributes.HasFlag(FileAttributes.ReparsePoint)) return (false, null);
        }

        var full = Path.GetFullPath(at);
        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? (true, full) : (false, null);
    }

    /// <summary>A file's type by its extension, text in UTF-8; a type this table does not name is bytes.</summary>
    public static string TypeOf(string file) => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".html" or ".htm" => "text/html; charset=utf-8",
        ".js" or ".mjs" or ".cjs" => "text/javascript; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".json" or ".map" => "application/json; charset=utf-8",
        ".webmanifest" => "application/manifest+json; charset=utf-8",
        ".txt" => "text/plain; charset=utf-8",
        ".xml" => "application/xml; charset=utf-8",
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".avif" => "image/avif",
        ".ico" => "image/x-icon",
        ".woff" => "font/woff",
        ".woff2" => "font/woff2",
        ".ttf" => "font/ttf",
        ".otf" => "font/otf",
        ".wasm" => "application/wasm",
        ".pdf" => "application/pdf",
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        _ => "application/octet-stream",
    };
}

/// <summary>
/// The shell's half of the review's showing (REVIEWENV1d, D154 point 5; design §2.3 steps 1–4): a set-up step's tab in Daoris's
/// browser, and its build served to that tab alone, over the shell's own CDP connection to its own browser, at the rule's address
/// and the build's base only. Held here, not by the session, so it lasts through reloads and the app's own navigation until the
/// verdict lets it go: a session's own interception ends with it, and a reload would then load the person's own server
/// (measured, <c>docs/2026-10-09-review-serving-evidence.md</c>).
/// </summary>
/// <remarks>
/// <para><b>Beside a session's browser server, never instead of it.</b> Interceptions chain on one tab, the session's first
/// (measured, §1 rows 2–2c): what the session fulfils while it lives is what it shows, and what it lets go on is answered here,
/// before the network. Nothing here stops, restarts or takes over a process or a port of the person's: a request this lets go
/// reaches their server as it would have, and one this answers never does.</para>
///
/// <para><b>One tab per set-up step</b>, remembered by its target for the shell's life; a tab the person closed is opened again
/// by the next serving. A serving lasts until <see cref="Stop"/>, the tab's closing, or the browser's going, and
/// <see cref="Serving"/> says so at once.</para>
/// </remarks>
public sealed class ReviewTabs(IInAppBrowser browser) : IReviewTabs, IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Tab> _tabs = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A set-up step's tab: its target, and while it is served, the connection that holds it and what it serves.</summary>
    private sealed class Tab
    {
        public string? Target { get; set; }

        public CdpChannel? Channel { get; set; }

        public ReviewServe? Serve { get; set; }
    }

    public IReadOnlyList<ReviewServe> Serving
    {
        get
        {
            lock (_gate)
            {
                return [.. _tabs.Values.Where(tab => tab is { Serve: not null, Channel.Open: true }).Select(tab => tab.Serve!)];
            }
        }
    }

    public async Task OpenAsync(string quest, string title, CancellationToken ct = default)
    {
        var port = await PortAsync(ct).ConfigureAwait(false);
        using var engine = new EngineCdp(port);
        var target = await TabOfAsync(quest, title, engine, ct).ConfigureAwait(false);
        await engine.ActivateAsync(target, ct).ConfigureAwait(false);
    }

    public async Task ServeAsync(ReviewServe serve, CancellationToken ct = default)
    {
        var port = await PortAsync(ct).ConfigureAwait(false);
        using var engine = new EngineCdp(port);
        var target = await TabOfAsync(serve.Quest, serve.Title, engine, ct).ConfigureAwait(false);

        // What the tab was served before goes first: one serving per tab, and a newer set-up's build is the one it shows.
        Release(serve.Quest);

        var channel = await CdpChannel.ConnectAsync(port, ct).ConfigureAwait(false);
        try
        {
            var attached = await channel.CallAsync("Target.attachToTarget", new { targetId = target, flatten = true }, null, ct)
                .ConfigureAwait(false);
            var session = attached.GetProperty("sessionId").GetString()
                          ?? throw new InvalidOperationException("the browser attached to the step's tab with no session.");

            channel.Heard += (method, parameters, from) =>
            {
                if (method == "Fetch.requestPaused" && from == session)
                {
                    // Off the reading loop, which reads this call's own answer.
                    _ = Task.Run(() => AnswerAsync(channel, session, serve, parameters), CancellationToken.None);
                }
                else if ((method == "Target.detachedFromTarget" && Text(parameters, "sessionId") == session)
                         || (method == "Inspector.detached" && from == session))
                {
                    // The person closed the tab: it is served no more, and the next serving opens one of its own.
                    Forget(serve.Quest, channel, closed: true);
                }
            };
            channel.Closed += () => Forget(serve.Quest, channel, closed: false);

            await channel.CallAsync(
                    "Fetch.enable", new { patterns = new[] { new { urlPattern = serve.Pattern, requestStage = "Request" } } }, session, ct)
                .ConfigureAwait(false);
            lock (_gate)
            {
                var tab = _tabs.TryGetValue(serve.Quest, out var known) ? known : _tabs[serve.Quest] = new Tab();
                tab.Target = target;
                tab.Channel = channel;
                tab.Serve = serve;
            }

            await channel.CallAsync("Page.navigate", new { url = serve.Look }, session, ct).ConfigureAwait(false);
            await engine.ActivateAsync(target, ct).ConfigureAwait(false);
        }
        catch
        {
            Forget(serve.Quest, channel, closed: false);
            await channel.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public void Stop(string quest) => Release(quest);

    public void Dispose()
    {
        List<CdpChannel> open;
        lock (_gate)
        {
            open = [.. _tabs.Values.Select(tab => tab.Channel).OfType<CdpChannel>()];
            _tabs.Clear();
        }

        foreach (var channel in open) channel.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(3));
    }

    /// <summary>Let the quest's serving go, its tab left open: the connection closes, and its interception with it.</summary>
    private void Release(string quest)
    {
        CdpChannel? channel;
        lock (_gate)
        {
            if (!_tabs.TryGetValue(quest, out var tab) || tab.Channel is null) return;
            channel = tab.Channel;
            tab.Channel = null;
            tab.Serve = null;
        }

        _ = channel.DisposeAsync().AsTask();
    }

    /// <summary>Forget a serving whose connection went, or whose tab the person closed, where it is still this one's.</summary>
    private void Forget(string quest, CdpChannel channel, bool closed)
    {
        lock (_gate)
        {
            if (!_tabs.TryGetValue(quest, out var tab) || !ReferenceEquals(tab.Channel, channel)) return;
            tab.Channel = null;
            tab.Serve = null;
            if (closed) tab.Target = null;
        }

        if (closed) _ = channel.DisposeAsync().AsTask();
    }

    /// <summary>The quest's tab: the one opened before where the browser still shows it, else a new one, titled for its quest.</summary>
    private async Task<string> TabOfAsync(string quest, string title, EngineCdp engine, CancellationToken ct)
    {
        string? known;
        lock (_gate) known = _tabs.TryGetValue(quest, out var tab) ? tab.Target : null;
        if (known is not null && await engine.PagesAsync(ct).ConfigureAwait(false) is { } pages && pages.Contains(known)) return known;

        var target = await engine.NewTabAsync(Page(quest, title), ct).ConfigureAwait(false);
        lock (_gate)
        {
            var tab = _tabs.TryGetValue(quest, out var held) ? held : _tabs[quest] = new Tab();
            tab.Target = target;
        }

        return target;
    }

    /// <summary>
    /// The page the step's tab opens on: its title, and a line saying whose tab it is, as data the browser makes itself, so it
    /// reaches no server. The person reads it until the session takes the tab to the app.
    /// </summary>
    private static string Page(string quest, string title) =>
        "data:text/html;charset=utf-8," + Uri.EscapeDataString(
            $"<!doctype html><meta charset=\"utf-8\"><title>{WebUtility.HtmlEncode(title)}</title>"
            + "<body style=\"font:15px system-ui,sans-serif;margin:3rem;color:#444\">"
            + $"<p>Daoris opened this tab for set-up step #{WebUtility.HtmlEncode(quest.TrimStart('#'))}.</p>"
            + "<p>Its session shows the work here, and Daoris keeps it shown until you say whether it is right.</p></body>");

    /// <summary>The browser's port, brought up in the background where it was not running.</summary>
    private async Task<int> PortAsync(CancellationToken ct)
    {
        var endpoint = await browser.EnsureAsync(ct).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("this shell has no browser to show it in.");
        return Uri.TryCreate(endpoint, UriKind.Absolute, out var at) && at.Port > 0
            ? at.Port
            : throw new InvalidOperationException($"the browser answered `{endpoint}`, which names no port.");
    }

    /// <summary>One paused request answered: from the build, refused, or handed on. A tab gone meanwhile is no failure.</summary>
    private static async Task AnswerAsync(CdpChannel channel, string session, ReviewServe serve, JsonElement paused)
    {
        var requestId = Text(paused, "requestId");
        if (requestId is null) return;
        var request = paused.TryGetProperty("request", out var asked) ? asked : default;
        var answer = ReviewRequests.Answer(
            serve, Text(request, "url") ?? "", Text(paused, "resourceType") ?? "", Text(request, "method") ?? "GET");

        try
        {
            switch (answer)
            {
                case { Kind: ReviewAnswerKind.File or ReviewAnswerKind.Index, Path: { } file }:
                    await FulfilAsync(channel, session, requestId, 200, ReviewRequests.TypeOf(file), await File.ReadAllBytesAsync(file).ConfigureAwait(false))
                        .ConfigureAwait(false);
                    break;
                case { Kind: ReviewAnswerKind.Pass }:
                    await channel.CallAsync("Fetch.continueRequest", new { requestId }, session, CancellationToken.None).ConfigureAwait(false);
                    break;
                default:
                    await FulfilAsync(channel, session, requestId, 404, "text/plain; charset=utf-8",
                        "Not in the build Daoris serves to this tab."u8.ToArray()).ConfigureAwait(false);
                    break;
            }
        }
        catch (IOException)
        {
            // A file that went between the look and the read: not found, and still never the person's server.
            await FulfilAsync(channel, session, requestId, 404, "text/plain; charset=utf-8", "Not in the build Daoris serves to this tab."u8.ToArray())
                .ConfigureAwait(false);
        }
        catch (Exception error) when (error is WebSocketException or InvalidOperationException or TimeoutException or ObjectDisposedException)
        {
            // The tab or the browser went while it was answered.
        }
    }

    private static async Task FulfilAsync(CdpChannel channel, string session, string requestId, int status, string type, byte[] body)
    {
        try
        {
            await channel.CallAsync("Fetch.fulfillRequest", new
            {
                requestId,
                responseCode = status,
                // Never cached: once the review lets it go, a reload must reach the person's own server, not this build kept.
                responseHeaders = new[] { new { name = "content-type", value = type }, new { name = "cache-control", value = "no-store" } },
                body = Convert.ToBase64String(body),
            }, session, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error) when (error is WebSocketException or InvalidOperationException or TimeoutException or ObjectDisposedException)
        {
            // The tab or the browser went while it was answered.
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
