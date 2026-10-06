using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>Which version a download, a use or an update means, whether it is fetched, or why not (§3.7).</summary>
/// <param name="Action"><c>download</c>, <c>use</c> or <c>update</c>.</param>
/// <param name="Version">The version downloaded or used; null when there is nothing to do, or a refusal.</param>
/// <param name="Current">The version the tool is managed at now, or null.</param>
/// <param name="Fetch">True when the version is not downloaded and is fetched from what the lists offer.</param>
/// <param name="Offered">What the lists offer for it, when it is fetched.</param>
/// <param name="Nothing">Why there is nothing to do, when there is not.</param>
/// <param name="Check">The check that refuses it; <paramref name="Problem"/> says why.</param>
public sealed record ToolPlan(
    string Tool, string Action, string? Version, string? Current, bool Fetch, OfferedVersion? Offered, string? Source,
    ToolLicence? Licence, string? Nothing, string? Check, string? Problem);

/// <summary>One resource location, looked at (§3.7).</summary>
/// <param name="Outcome"><c>fetched</c> (its copy is the list just fetched), <c>unread</c> (it answered no list this build
/// reads) or <c>failed</c>.</param>
/// <param name="Added">What the copy names now that the one before did not, for this platform: <c>&lt;tool&gt; &lt;version&gt;</c>.</param>
public sealed record LocationLook(string Address, string Outcome, string Sentence, IReadOnlyList<string> Added, IReadOnlyList<string> Dropped);

/// <summary>
/// A managed version of a tool: downloaded, verified, unpacked and laid out (TOOLS4, D121;
/// <c>docs/2026-10-01-tools-design.md</c> §3.6, §3.7).
/// </summary>
/// <remarks>
/// <para>🔴 <b>A TWIN of the CLI's <c>toolinstall.ts</c>.</b> The two share no code — the LAYOUT, the refusals and the
/// plan are the contract — and each carries the same tables (<c>ToolInstallTests</c> here, <c>toolinstall.test.ts</c>
/// there), row for row. A rule changed here is changed there, in the same commit:</para>
/// <list type="number">
/// <item>A download is staged at <c>&lt;home&gt;/tools/&lt;tool&gt;/&lt;version&gt;.part/</c>: the archive lands there and is
/// held to the list's size and SHA-256, it is unpacked into <c>package/</c>, the executable is found at <c>exe</c>, and
/// <c>tool.json</c> is written. Then the whole folder is renamed to <c>&lt;version&gt;/</c>. A refusal, or the person's
/// stop, removes the staging.</item>
/// <item>Finding the record, naming a file that is there, is the proof: a version already downloaded fetches nothing.</item>
/// <item>Every address a list names is tried in read order, the person's first; each is held to https://, or http://
/// to this machine, and so is every redirect, followed here one at a time. The bytes are the check, whichever address
/// served them.</item>
/// <item>Which version: the one named, or the newest the lists named at the last look. <c>update</c> moves a managed
/// tool to the newest, never back; a tool run from <c>PATH</c> or a file is the machine's to update.</item>
/// <item>A version in use is never deleted.</item>
/// <item>A look fetches each location in order, each bounded, and keeps a copy only of a list that reads.</item>
/// </list>
/// <para>The machine log gets a line an event (D94) — a download started, verified, refused by its check or stopped,
/// and a location fetched or failed — naming a tool and a version, or a location's place in the list, and never an
/// address the person typed.</para>
/// </remarks>
public static partial class ToolInstall
{
    /// <summary>Where a download is staged, beside the folder it becomes. The CLI's <c>STAGING</c>.</summary>
    public const string Staging = ".part";

    /// <summary>A record's keys, in the order both twins write them (§3.6). The CLI's <c>RECORD_KEYS</c>.</summary>
    public static readonly IReadOnlyList<string> RecordKeys =
        ["tool", "version", "platform", "sha256", "size", "archive", "url", "lists", "exe", "paths", "at"];

    /// <summary>How many redirects one fetch follows. The CLI's <c>REDIRECTS</c>.</summary>
    public const int Redirects = 10;

    /// <summary>How long one location may take to answer a look (§3.7). The CLI's <c>LOOK_BOUND_MS</c>.</summary>
    public static readonly TimeSpan LookBound = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions Written = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>A version's folder under the home: <c>&lt;home&gt;/tools/&lt;tool&gt;/&lt;version&gt;</c>.</summary>
    public static string VersionFolder(string home, string tool, string version) => Path.Combine(home, Tools.Folder, tool, version);

    /// <summary>The versions of a tool downloaded and verified — a record naming a file that is there — newest first.</summary>
    public static IReadOnlyList<string> Downloaded(string home, string tool)
    {
        var folder = Path.Combine(home, Tools.Folder, tool);
        if (!Directory.Exists(folder)) return [];
        return
        [
            .. Directory.EnumerateDirectories(folder)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(name => Tools.IsExactVersion(name) && Tools.ManagedExecutable(home, tool, name).File is not null)
                .OrderDescending(Comparer<string>.Create(ToolResources.CompareVersions)),
        ];
    }

    private static ToolDeclaration Declared(string tool) =>
        Tools.Find(tool) ?? throw new DriverException(
            $"`{tool}` is not a tool this build runs — one of: {string.Join(", ", Tools.Declared.Select(each => each.Id))}. "
            + "A tool is added in the code, never by a file.");

    private static string NoPlatform =>
        $"this machine ({RuntimeInformationText()}) is not a platform a list may name, so no version is offered here";

    private static string RuntimeInformationText() =>
        $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription} {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}";

    /// <summary>
    /// Which version a download, a use or an update means, and whether it is fetched (§3.7, rule 4). It reads the home's
    /// file and what is downloaded, and the lists as last fetched; it fetches nothing. The CLI's <c>planTool</c>.
    /// </summary>
    /// <param name="action"><c>download</c>, <c>use</c> or <c>update</c>.</param>
    /// <param name="asked">The version named, or null for the newest the lists name.</param>
    public static ToolPlan Plan(string home, MergedResources merged, string tool, string action, string? asked)
    {
        var declared = Declared(tool);
        var entry = Tools.Read(home).Entries[tool];
        var current = entry.Way == ToolWay.Managed ? entry.Version : null;
        var offers = merged.Tools.First(each => each.Tool == tool);
        var platform = merged.Platform;
        ToolPlan Refuse(string check, string problem) =>
            new(tool, action, null, current, false, null, offers.Source, offers.Licence, null, check, problem);
        ToolPlan Done(string nothing) => new(tool, action, null, current, false, null, offers.Source, offers.Licence, nothing, null, null);
        var noneNamed = $"no list names a version of {declared.Name} for {platform} — `daoris tool look` fetches the locations, and "
            + "`daoris tool locations add <address>` adds one";
        bool Here(string version) => Tools.ManagedExecutable(home, tool, version).File is not null;

        string version;
        if (action == "update")
        {
            if (entry.Problem is not null) return Refuse("file", entry.Problem);
            if (entry.Way != ToolWay.Managed)
            {
                return Refuse("machine", $"{declared.Name} runs {(entry.Way == ToolWay.File ? "a file you name" : "the system's")}: its "
                    + $"updates are the machine's, not Daoris's — `daoris tool use {tool} managed` keeps a version in the home");
            }
            if (platform is null) return Refuse("platform", NoPlatform);
            if (offers.Newest is not { } newest) return Refuse("unknown", noneNamed);
            if (ToolResources.CompareVersions(newest, current!) < 0)
            {
                return Done($"{declared.Name} is managed at {current}, newer than the newest the lists name ({newest}) — update never "
                    + "moves a tool back");
            }
            if (newest == current && Here(current))
            {
                return Done($"{declared.Name} {current} is the newest the lists name, and it is downloaded — nothing to do");
            }

            version = newest;
        }
        else if (asked is not null)
        {
            if (!Tools.IsExactVersion(asked)) return Refuse("version", $"`{asked}` is not an exact version — one to four numbers, like 2.51.0");
            version = asked;
        }
        else
        {
            if (platform is null) return Refuse("platform", NoPlatform);
            if (offers.Newest is not { } newest) return Refuse("unknown", noneNamed);
            version = newest;
        }

        if (Here(version)) return new ToolPlan(tool, action, version, current, false, null, offers.Source, offers.Licence, null, null, null);
        if (platform is null) return Refuse("platform", NoPlatform);
        if (offers.Refused.FirstOrDefault(each => each.Version == version) is { } conflict) return Refuse("conflict", conflict.Problem);
        if (offers.Versions.FirstOrDefault(each => each.Version == version) is not { } offered)
        {
            return Refuse("unknown", $"no list names {declared.Name} {version} for {platform}");
        }

        return new ToolPlan(tool, action, version, current, true, offered, offers.Source, offers.Licence, null, null, null);
    }

    /// <summary>
    /// Download one version, verify it, unpack it and lay it out (§3.6, rules 1–3). Nothing switches: using it is
    /// <see cref="Tools.UseManaged"/>'s, once this has answered. The CLI's <c>downloadVersion</c>.
    /// </summary>
    /// <param name="ct">The person's stop: it ends the download, and the staging goes with it.</param>
    /// <param name="transport">How a host is reached — the network, unless a test holds its own.</param>
    /// <returns>The executable.</returns>
    /// <exception cref="ToolRefusal"><c>archive</c>, <c>address</c>, <c>unreachable</c>, <c>size</c> or <c>hash</c> (the
    /// last address tried says which), or the archive's own check.</exception>
    public static async Task<string> DownloadAsync(
        string home, string tool, OfferedVersion offered, string platform, Action<string> write, CancellationToken ct = default,
        HttpMessageHandler? transport = null, MachineLog? log = null)
    {
        var declared = Declared(tool);
        var version = offered.Version;
        if (!Tools.IsExactVersion(version)) throw new ToolRefusal("version", $"`{version}` is not an exact version — nothing was fetched");

        var folder = VersionFolder(home, tool, version);
        if (Tools.ManagedExecutable(home, tool, version).File is { } present)
        {
            write($"  {declared.Name} {version} is already downloaded — nothing was fetched.");
            write($"  {present}");
            return present;
        }

        if (!ToolResources.Archives.Contains(offered.Archive, StringComparer.Ordinal))
        {
            throw new ToolRefusal("archive", $"`{offered.Archive}` is not an archive this build unpacks — zip or tar.gz; nothing was fetched");
        }

        log ??= MachineLog.None;
        log.Info("tool.download.started", ("tool", tool), ("version", version));
        var staging = folder + Staging;
        try
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            Directory.CreateDirectory(staging);
            var archive = Path.Combine(staging, "archive");
            using var http = Client(transport);
            var url = await FetchVerifiedAsync(http, declared, offered, archive, write, ct).ConfigureAwait(false);
            write($"  its size and SHA-256 match the list: {offered.Sha256}");

            Unpack(archive, offered.Archive, Path.Combine(staging, Tools.Package), offered.Exe);
            File.Delete(archive);
            WriteRecord(Path.Combine(staging, Tools.Record), tool, offered, platform, url);

            if (Directory.Exists(folder))
            {
                // Daoris's own folder, holding nothing that runs (asked above): a download that stopped, or a record that went.
                write($"  replacing {folder}, which held nothing that runs");
                Directory.Delete(folder, recursive: true);
            }

            // The scanner opens the executable just unpacked, and the folder cannot move until it lets go (FIX-LOG 2026-10-07).
            AtomicFile.MoveFolder(staging, folder);
            write($"  unpacked into {Path.Combine(folder, Tools.Package)}");
            log.Info("tool.download.verified", ("tool", tool), ("version", version));
            return Path.Combine([folder, Tools.Package, .. offered.Exe.Split('/')]);
        }
        catch (ToolRefusal refusal)
        {
            log.Warn("tool.download.refused", ("tool", tool), ("version", version), ("check", refusal.Check));
            throw;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            log.Info("tool.download.stopped", ("tool", tool), ("version", version));
            throw;
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            // A refusal leaves nothing under the tool's folder, not even the folder the staging made.
            var parent = Path.Combine(home, Tools.Folder, tool);
            if (Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent);
        }
    }

    /// <summary>The record, its keys in <see cref="RecordKeys"/>' order: what the CLI's <c>JSON.stringify</c> writes.</summary>
    private static void WriteRecord(string path, string tool, OfferedVersion offered, string platform, string url)
    {
        var record = new JsonObject
        {
            ["tool"] = tool,
            ["version"] = offered.Version,
            ["platform"] = platform,
            ["sha256"] = offered.Sha256,
            ["size"] = offered.Size,
            ["archive"] = offered.Archive,
            ["url"] = url,
            ["lists"] = new JsonArray([.. offered.Lists.Select(each => (JsonNode?)each)]),
            ["exe"] = offered.Exe,
            ["paths"] = new JsonArray([.. offered.Paths.Select(each => (JsonNode?)each)]),
            ["at"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
        };
        AtomicFile.WriteText(path, record.ToJsonString(Written) + "\n");
    }

    /// <summary>The archive, from the first address whose bytes are the list's (rule 3).</summary>
    /// <returns>That address.</returns>
    private static async Task<string> FetchVerifiedAsync(
        HttpClient http, ToolDeclaration tool, OfferedVersion offered, string to, Action<string> write, CancellationToken ct)
    {
        var reasons = new List<string>();
        ToolRefusal? last = null;
        for (var at = 0; at < offered.Urls.Count; at++)
        {
            var url = offered.Urls[at];
            ToolRefusal failed;
            try
            {
                if (!Tools.IsAddress(url))
                {
                    throw new ToolRefusal("address", $"{url} is not https://, or http:// to this machine — nothing was fetched from it");
                }

                write($"  downloading {url} ({offered.Size / 1024.0 / 1024.0:0.0} MB)");
                var saved = await SaveAsync(http, url, to, ct).ConfigureAwait(false)
                    ?? throw new ToolRefusal("unreachable", $"{url} has nothing there");
                if (saved.Size != offered.Size)
                {
                    throw new ToolRefusal("size", $"{url} served {saved.Size} bytes, and the list says {offered.Size}");
                }
                if (saved.Sha256 != offered.Sha256)
                {
                    throw new ToolRefusal("hash", $"{url} served bytes whose SHA-256 is {saved.Sha256}, and the list says {offered.Sha256}");
                }

                return url;
            }
            catch (ToolRefusal refusal)
            {
                failed = refusal;
            }

            if (File.Exists(to)) File.Delete(to);
            write($"  {failed.Message}{(at < offered.Urls.Count - 1 ? " — trying the next address" : "")}");
            reasons.Add(failed.Message);
            last = failed;
        }

        throw new ToolRefusal(last?.Check ?? "unreachable", $"{tool.Name} {offered.Version} was not downloaded: "
            + $"{(reasons.Count > 0 ? string.Join("; ", reasons) : "no address names it")}. Nothing was kept");
    }

    /// <summary>
    /// Delete a downloaded version nothing uses (§3.6, rule 5). The CLI's <c>deleteVersion</c>.
    /// </summary>
    /// <returns>The folder deleted.</returns>
    /// <exception cref="ToolRefusal"><c>version</c>, <c>missing</c>, <c>file</c> (which version runs cannot be read),
    /// <c>in-use</c>, or <c>held</c> with the system's reason when something still holds a file in it.</exception>
    public static string Delete(string home, string tool, string version)
    {
        var declared = Declared(tool);
        if (!Tools.IsExactVersion(version))
        {
            throw new ToolRefusal("version", $"`{version}` is not an exact version — one to four numbers, like 2.51.0 — so nothing was deleted");
        }

        var folder = VersionFolder(home, tool, version);
        if (!Directory.Exists(folder)) throw new ToolRefusal("missing", $"{declared.Name} {version} is not downloaded ({folder}) — nothing was deleted");

        var read = Tools.Read(home);
        if (read.Problem is not null)
        {
            throw new ToolRefusal("file", $"{read.Problem} — nothing was deleted, since which version runs cannot be read");
        }
        var entry = read.Entries[tool];
        if (entry.Way == ToolWay.Managed && entry.Version == version)
        {
            throw new ToolRefusal("in-use", $"{declared.Name} runs {version} — `daoris tool use {tool} system`, or another version, first; "
                + "nothing was deleted");
        }

        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new ToolRefusal("held", $"{folder} could not be deleted — {error.Message} A program may be running from it: stop it "
                + "and delete again; part of it may already be gone");
        }

        return folder;
    }

    /// <summary>
    /// Fetch each resource location, in order, each bounded (§3.7, rule 6). A list that reads replaces its copy, the bytes
    /// as fetched; anything else keeps the last copy and says its age. Downloads nothing. The CLI's <c>lookLocations</c>.
    /// </summary>
    /// <param name="ct">The person's stop.</param>
    /// <param name="bound">How long one location may take; <see cref="LookBound"/> unless a test says otherwise.</param>
    public static async Task<IReadOnlyList<LocationLook>> LookAsync(
        string home, string? platform, CancellationToken ct = default, HttpMessageHandler? transport = null, TimeSpan? bound = null,
        MachineLog? log = null, DateTimeOffset? now = null)
    {
        log ??= MachineLog.None;
        var when = now ?? DateTimeOffset.UtcNow;
        var looks = new List<LocationLook>();
        using var http = Client(transport);
        var locations = Tools.Read(home).Locations;
        for (var position = 0; position < locations.Count; position++)
        {
            var address = locations[position];
            var copy = ToolResources.LocationCopy(home, address);
            var before = ToolResources.ReadFile(copy, address);
            string Kept() => before.Exists ? $"its copy from {Age(copy, when)} ago is kept" : "it has never been fetched, so it names nothing";
            LocationLook Failed(string why) => new(address, "failed", $"{address} could not be fetched ({why}); {Kept()}", [], []);

            byte[]? bytes;
            using (var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                bounded.CancelAfter(bound ?? LookBound);
                try
                {
                    bytes = await BytesAsync(http, address, bounded.Token).ConfigureAwait(false);
                }
                catch (ToolRefusal refusal)
                {
                    looks.Add(Failed(refusal.Message.TrimEnd('.')));
                    log.Warn("tool.location.failed", ("position", position + 1), ("check", refusal.Check));
                    continue;
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    var seconds = (int)Math.Round((bound ?? LookBound).TotalSeconds);
                    looks.Add(Failed($"{new Uri(address).Host} did not answer within {seconds} seconds"));
                    log.Warn("tool.location.failed", ("position", position + 1), ("check", "unreachable"));
                    continue;
                }
            }

            if (bytes is null)
            {
                looks.Add(Failed("nothing there"));
                log.Warn("tool.location.failed", ("position", position + 1), ("check", "unreachable"));
                continue;
            }

            var list = ToolResources.Parse(Encoding.UTF8.GetString(bytes), address);
            if (list.Problem is not null)
            {
                looks.Add(new LocationLook(address, "unread", $"{address} answered a list this build does not read ({list.Problem}); {Kept()}", [], []));
                log.Warn("tool.location.failed", ("position", position + 1), ("check", "unread"));
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            AtomicFile.WriteBytes(copy, bytes);
            var was = OfferedHere(before, platform);
            var isNow = OfferedHere(list, platform);
            var added = isNow.Where(each => !was.Contains(each)).ToList();
            var dropped = was.Where(each => !isNow.Contains(each)).ToList();
            var parts = new List<string> { $"fetched — {isNow.Count} version{(isNow.Count == 1 ? "" : "s")} for {platform ?? "no platform"}" };
            if (added.Count > 0) parts.Add($"added {string.Join(", ", added)}");
            if (dropped.Count > 0) parts.Add($"dropped {string.Join(", ", dropped)}");
            if (before.Exists && added.Count == 0 && dropped.Count == 0) parts.Add("nothing changed since the copy before");
            looks.Add(new LocationLook(address, "fetched", $"{address}: {string.Join("; ", parts)}", added, dropped));
            log.Info("tool.location.fetched", ("position", position + 1), ("versions", isNow.Count));
        }

        return looks;
    }

    /// <summary>What a list offers for one platform, <c>&lt;tool&gt; &lt;version&gt;</c>, in the declared order and newest first.</summary>
    private static List<string> OfferedHere(ResourceList list, string? platform)
    {
        if (platform is null) return [];
        var newestFirst = Comparer<string>.Create((a, b) => ToolResources.CompareVersions(b, a));
        return
        [
            .. Tools.Declared.SelectMany(tool => list.Tools.TryGetValue(tool.Id, out var offered)
                ? offered.Versions.Where(pair => pair.Value.ContainsKey(platform)).Select(pair => pair.Key).Order(newestFirst)
                    .Select(version => $"{tool.Id} {version}")
                : []),
        ];
    }

    /// <summary>How long ago a file was written, in the largest whole unit.</summary>
    private static string Age(string file, DateTimeOffset now)
    {
        var seconds = (long)Math.Max(0, Math.Round((now - File.GetLastWriteTimeUtc(file)).TotalSeconds));
        var (count, unit) = seconds < 120 ? (seconds, "second") : seconds < 7200 ? ((long)Math.Round(seconds / 60.0), "minute")
            : seconds < 172800 ? ((long)Math.Round(seconds / 3600.0), "hour") : ((long)Math.Round(seconds / 86400.0), "day");
        return $"{count} {unit}{(count == 1 ? "" : "s")}";
    }

    // ── A host ─────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A client that follows no redirect on its own: <see cref="GetAsync"/> follows each, held to the address rule. A
    /// download outlives any fixed timeout; the person's stop is the token, and a look's bound is its own.
    /// </summary>
    private static HttpClient Client(HttpMessageHandler? transport)
    {
        var http = transport is null
            ? new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan }
            : new HttpClient(transport, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("daoris");
        return http;
    }

    /// <summary>The response at an address, every hop held to the address rule; null for a 404, an answer the caller words.</summary>
    private static async Task<HttpResponseMessage?> GetAsync(HttpClient http, string url, CancellationToken ct)
    {
        var at = url;
        for (var hops = 0; ; hops++)
        {
            if (!Tools.IsAddress(at))
            {
                throw new ToolRefusal("address", hops == 0
                    ? $"{at} is not https://, or http:// to this machine — nothing was fetched from it."
                    : $"{url} redirected to {at}, which is not https://, or http:// to this machine — nothing was fetched from it.");
            }

            HttpResponseMessage response;
            try
            {
                response = await http.GetAsync(at, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException error)
            {
                throw new ToolRefusal("unreachable", $"could not reach {new Uri(at).Host} — {error.Message}. Nothing was installed.");
            }

            var status = (int)response.StatusCode;
            if (status is >= 300 and < 400 && response.Headers.Location is { } location)
            {
                response.Dispose();
                if (hops >= Redirects)
                {
                    throw new ToolRefusal("unreachable", $"{url} redirected more than {Redirects} times — nothing was fetched from it.");
                }

                at = new Uri(new Uri(at), location).AbsoluteUri;
                continue;
            }

            if (status == 404)
            {
                response.Dispose();
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                response.Dispose();
                throw new ToolRefusal("unreachable", $"{at} answered {status} — nothing was installed.");
            }

            return response;
        }
    }

    private static async Task<byte[]?> BytesAsync(HttpClient http, string url, CancellationToken ct)
    {
        using var response = await GetAsync(http, url, ct).ConfigureAwait(false);
        return response is null ? null : await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Stream a download to disk, hashing on the way — never held whole.</summary>
    private static async Task<(string Sha256, long Size)?> SaveAsync(HttpClient http, string url, string to, CancellationToken ct)
    {
        using var response = await GetAsync(http, url, ct).ConfigureAwait(false);
        if (response is null) return null;

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long size = 0;
        await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var target = File.Create(to))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                size += read;
                await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            }
        }

        return (Convert.ToHexStringLower(hash.GetHashAndReset()), size);
    }
}
