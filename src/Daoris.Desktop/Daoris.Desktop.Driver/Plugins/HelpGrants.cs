using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What a plugin hands Ask Daoris, as the person approved it (HELPPLUG1a's tightening, D170's note): a manifest describes
/// authority and grants none. A server declared for Ask Daoris reaches it only once the person approved its exact tools, on the
/// plugin's page or at its terminal twin, and asks again when an update adds a tool or moves one between reading and writing.
/// </summary>
/// <remarks>
/// <para><b>A write is never allowed silently</b> (D89, every change is confirmed): a tool the plugin marks as changing something
/// outside Daoris is allowed only where the person accepted it as a write; any other is asked, which Ask Daoris's asking mode
/// refuses. A write an update adds after the approval is asked the same way.</para>
///
/// <para><b>No generic execution tool, whatever the manifest says</b> (HELP4): a server naming a tool that runs commands, a
/// shell, a script or an eval, by its name, is refused for Ask Daoris with that reason and cannot be approved. A wildcard is no
/// tool's name, so the manifest's own rules already refuse one (<see cref="PluginCatalog"/>).</para>
///
/// <para><b>Machine-local and the driver's alone</b>: <c>&lt;home&gt;/plugin-approvals.json</c>, written by the page's press and
/// <c>daoris-driver plugins approve|withdraw</c>, read at each spawn of Ask Daoris. A file that does not read approves nothing,
/// the safe direction, and is never written over; an edit keeps every key it does not own (KEEPALL1d).</para>
/// </remarks>
public static class HelpGrants
{
    /// <summary>The file's name, as a refusal says it.</summary>
    public const string FileName = "plugin-approvals.json";

    /// <summary>Where a home's approvals are: the home's <c>plugin-approvals</c> entry (D172, HOMELAYOUT2).</summary>
    public static string PathIn(string home) => DaorisHome.Entry(home, HomeEntry.PluginApprovals);

    /// <summary>The person approved its exact tools: it is handed, its reads and accepted writes allowed.</summary>
    public const string Approved = "approved";

    /// <summary>Not approved, or changed since: handed nothing until the person approves it.</summary>
    public const string Waits = "waits";

    /// <summary>It names a generic execution tool, so it is never handed to Ask Daoris.</summary>
    public const string Refused = "refused";

    /// <summary>The words of a tool's name that make it a generic execution tool, its name split at case, <c>_</c> and <c>-</c>.</summary>
    private static readonly HashSet<string> Running = new(StringComparer.Ordinal)
    {
        "shell", "bash", "sh", "zsh", "fish", "cmd", "powershell", "pwsh", "exec", "execute", "eval", "evaluate", "terminal",
        "spawn", "subprocess", "command", "commands", "script", "repl", "run",
    };

    private static readonly Regex Words = new("[A-Z]?[a-z0-9]+|[A-Z]+(?![a-z])", RegexOptions.CultureInvariant);

    /// <summary>Why a server is never handed to Ask Daoris, or null: a tool it names runs commands, judged by the tool's name.</summary>
    public static string? Refusal(PluginServer server)
    {
        var running = server.Allow.Concat(server.Writes).FirstOrDefault(tool =>
            Words.Matches(tool).Any(word => Running.Contains(word.Value.ToLowerInvariant())));
        return running is null
            ? null
            : $"its tool `{running}` runs commands, by its name, and Ask Daoris is never handed one that does (HELP4, D170): a shell, a "
              + "command runner or an eval would let it change anything with nobody's press.";
    }

    /// <summary>Every server the contributing plugins declare for Ask Daoris, each with the person's approval as it stands.</summary>
    public static HelpGrantSet Read(string home, PluginCatalog catalog)
    {
        var approvals = Approvals(home) ?? [];
        var grants = new List<HelpGrant>();
        foreach (var plugin in catalog.Contributing)
        {
            foreach (var server in plugin.Manifest.Servers.Where(server => server.HandedTo(PluginServerTo.Help)))
            {
                if (Refusal(server) is { } refusal)
                {
                    grants.Add(new HelpGrant(plugin.Manifest.Id, server, Refused, refusal));
                    continue;
                }

                var approval = approvals.FirstOrDefault(each => Same(each, plugin.Manifest.Id, server.Name));
                var exact = approval is not null && approval.Allow.ToHashSet(StringComparer.Ordinal).SetEquals(server.Allow)
                            && approval.Writes.All(tool => server.Writes.Contains(tool, StringComparer.Ordinal));
                // HELPPLUG1c: an approval on file that no longer matches is said as changed since, never as never approved.
                grants.Add(exact
                    ? new HelpGrant(plugin.Manifest.Id, server, Approved, null) { Accepted = approval!.Writes }
                    : new HelpGrant(plugin.Manifest.Id, server, Waits, null) { Changed = approval is not null });
            }
        }

        return new HelpGrantSet(grants);
    }

    /// <summary>
    /// The person's approval of the exact tools a plugin's server declares for Ask Daoris: its reads, and its writes too unless
    /// <paramref name="readsOnly"/>. Answers the grant it makes.
    /// </summary>
    /// <exception cref="DriverException">No such server here for Ask Daoris, one that is refused, or a file that does not read.</exception>
    public static HelpGrant Approve(string home, PluginCatalog catalog, string plugin, string server, bool readsOnly) =>
        Approve(home, catalog, plugin, server, readsOnly ? [] : null);

    /// <summary>
    /// The person's approval of a server's exact reads, and of the writes they accepted, each on its own (HELPPLUG1c): null
    /// accepts every write it declares, an empty list none. The writes not accepted are asked, which Ask Daoris's mode refuses.
    /// </summary>
    /// <exception cref="DriverException">
    /// No such server here for Ask Daoris, one that is refused, a write it does not declare, or a file that does not read.
    /// </exception>
    public static HelpGrant Approve(string home, PluginCatalog catalog, string plugin, string server, IReadOnlyList<string>? writes)
    {
        var (entry, declared) = Declared(catalog, plugin, server);
        if (Refusal(declared) is { } refusal) throw new DriverException($"`{server}` is not approved: {refusal}");
        if (writes?.FirstOrDefault(tool => !declared.Writes.Contains(tool, StringComparer.Ordinal)) is { } unknown)
        {
            throw new DriverException($"`{unknown}` is not a write `{declared.Name}` declares, so nothing was approved: it writes outside Daoris with "
                + (declared.Writes.Count > 0 ? string.Join(", ", declared.Writes.Select(tool => $"`{tool}`")) : "none of its tools") + ".");
        }

        var found = File.Exists(PathIn(home));
        var approvals = Approvals(home) ?? (found
            ? throw new DriverException($"`{FileName}` under the home could not be read, so nothing was approved over it.")
            : []);
        // The declared order, whatever order the person accepted them in, so the file reads as the manifest does.
        var accepted = writes is null ? declared.Writes : [.. declared.Writes.Where(tool => writes.Contains(tool, StringComparer.Ordinal))];
        // An approval on file is changed where it stands, its own unknown keys kept; a new one goes last.
        var existing = approvals.FirstOrDefault(each => Same(each, entry.Manifest.Id, declared.Name));
        var approval = new HelpApproval(entry.Manifest.Id, declared.Name, declared.Allow, accepted, existing?.Row) { Edited = true };
        Save(home, found, approvals, existing is null
            ? [.. approvals, approval]
            : [.. approvals.Where(each => !Same(each, approval.Plugin, approval.Server) || ReferenceEquals(each, existing))
                .Select(each => ReferenceEquals(each, existing) ? approval : each)]);
        return new HelpGrant(approval.Plugin, declared, Approved, null) { Accepted = approval.Writes };
    }

    /// <summary>The person's approval withdrawn: the server is handed nothing again until approved. One never approved is no error.</summary>
    /// <exception cref="DriverException">A file that does not read.</exception>
    public static void Withdraw(string home, string plugin, string server)
    {
        if (!File.Exists(PathIn(home))) return;
        var approvals = Approvals(home)
            ?? throw new DriverException($"`{FileName}` under the home could not be read, so nothing was withdrawn from it.");
        Save(home, found: true, approvals, [.. approvals.Where(each => !Same(each, plugin, server))]);
    }

    /// <summary>The contributing plugin and its server declared for Ask Daoris, as an approval and Ask Daoris's proposal name them.</summary>
    /// <exception cref="DriverException">No such plugin here, switched on and sound, offering Ask Daoris that server.</exception>
    internal static (PluginEntry Entry, PluginServer Server) Declared(PluginCatalog catalog, string plugin, string server)
    {
        var entry = catalog.Contributing.FirstOrDefault(each => string.Equals(each.Manifest.Id, plugin, StringComparison.OrdinalIgnoreCase));
        var declared = entry?.Manifest.Servers.FirstOrDefault(each =>
            string.Equals(each.Name, server, StringComparison.Ordinal) && each.HandedTo(PluginServerTo.Help))
            ?? throw new DriverException(
                $"no plugin `{plugin}` on this machine, switched on and sound, offers Ask Daoris a server `{server}`; `daoris-driver plugins show {plugin}` says what it offers.");
        return (entry!, declared);
    }

    private static bool Same(HelpApproval approval, string plugin, string server) =>
        string.Equals(approval.Plugin, plugin, StringComparison.OrdinalIgnoreCase) && string.Equals(approval.Server, server, StringComparison.Ordinal);

    /// <summary>The approvals as the file holds them; empty with no file, null where it does not read.</summary>
    private static List<HelpApproval>? Approvals(string home)
    {
        var path = PathIn(home);
        if (!File.Exists(path)) return [];
        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            if (!document.RootElement.TryGetProperty("help", out var help) || help.ValueKind != JsonValueKind.Array) return null;
            var approvals = new List<HelpApproval>();
            foreach (var row in help.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object
                    || !row.TryGetProperty("plugin", out var plugin) || plugin.ValueKind != JsonValueKind.String
                    || !row.TryGetProperty("server", out var server) || server.ValueKind != JsonValueKind.String)
                {
                    return null;
                }

                approvals.Add(new HelpApproval(plugin.GetString()!, server.GetString()!, Names(row, "allow"), Names(row, "writes"), row.Clone()));
            }

            return approvals;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> Names(JsonElement row, string field) =>
        row.TryGetProperty(field, out var names) && names.ValueKind == JsonValueKind.Array
            ? [.. names.EnumerateArray().Where(name => name.ValueKind == JsonValueKind.String).Select(name => name.GetString()!)]
            : [];

    /// <summary>
    /// The edit from the approvals read to <paramref name="approvals"/> made onto the file as it stands (KEEPALL1d,
    /// <see cref="JsonKeeping.Save"/>), so a key this build has no field for stays as written, at the top and in a row: a row the edit
    /// did not touch is written as the file had it, and one it changed keeps its own unknown keys. Rebuilt from the approvals alone, it
    /// deleted every other key the file held. <paramref name="found"/> says a file was there at the read. The rows are one array, so
    /// another writer's row landing between this read and the write is not kept; the read and the write are one call.
    /// </summary>
    private static void Save(string home, bool found, IReadOnlyList<HelpApproval> read, IReadOnlyList<HelpApproval> approvals) =>
        JsonKeeping.Save(PathIn(home), found, Written(read), Written(approvals));

    /// <summary>The approvals' own shape: <c>help</c>, a row each.</summary>
    private static string Written(IReadOnlyList<HelpApproval> approvals) => JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        writer.WriteStartArray("help");
        foreach (var approval in approvals) Write(writer, approval);
        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    /// <summary>One row: as the file had it where unchanged; else its own keys in its order, the approval's four set and any missing added.</summary>
    private static void Write(Utf8JsonWriter writer, HelpApproval approval)
    {
        if (approval.Row is { } row && !approval.Edited)
        {
            row.WriteTo(writer);
            return;
        }

        void Field(string name)
        {
            if (name == "plugin") writer.WriteString(name, approval.Plugin);
            else if (name == "server") writer.WriteString(name, approval.Server);
            else
            {
                writer.WriteStartArray(name);
                foreach (var tool in name == "allow" ? approval.Allow : approval.Writes) writer.WriteStringValue(tool);
                writer.WriteEndArray();
            }
        }

        string[] own = ["plugin", "server", "allow", "writes"];
        writer.WriteStartObject();
        if (approval.Row is { } kept)
        {
            foreach (var property in kept.EnumerateObject())
            {
                if (own.Contains(property.Name)) Field(property.Name);
                else property.WriteTo(writer);
            }
        }

        foreach (var name in own.Where(name => approval.Row?.TryGetProperty(name, out _) != true)) Field(name);
        writer.WriteEndObject();
    }

    /// <summary>
    /// One approval as the file keeps it: the plugin, its server, and the exact reads and accepted writes approved; <see cref="Row"/>
    /// is the row as the file held it, null for a new one, and <see cref="Edited"/> whether this edit changed it.
    /// </summary>
    private sealed record HelpApproval(string Plugin, string Server, IReadOnlyList<string> Allow, IReadOnlyList<string> Writes, JsonElement? Row = null)
    {
        public bool Edited { get; init; }
    }
}

/// <summary>A plugin's server declared for Ask Daoris, with the person's approval as it stands (<see cref="HelpGrants"/>).</summary>
/// <param name="State"><see cref="HelpGrants.Approved"/>, <see cref="HelpGrants.Waits"/> or <see cref="HelpGrants.Refused"/>.</param>
/// <param name="Refusal">Why it is never handed, where it is refused.</param>
public sealed record HelpGrant(string Plugin, PluginServer Server, string State, string? Refusal)
{
    /// <summary>The writes the person accepted, where approved: allowed; the server's other writes are asked.</summary>
    public IReadOnlyList<string> Accepted { get; init; } = [];

    /// <summary>
    /// Where it waits: whether the person approved it once and an update has changed its tools since (HELPPLUG1c), so it is
    /// said as asked again rather than as never approved. False for every other state.
    /// </summary>
    public bool Changed { get; init; }
}

/// <summary>Every grant, and what Ask Daoris's spawn takes of them: the approved servers, and the rules for their tools.</summary>
public sealed record HelpGrantSet(IReadOnlyList<HelpGrant> All)
{
    private IEnumerable<HelpGrant> Granted => All.Where(grant => grant.State == HelpGrants.Approved);

    /// <summary>The approved servers, as the protocol door carries one; no other reaches Ask Daoris.</summary>
    public IReadOnlyList<AcpMcpServer> Servers => [.. Granted.Select(grant =>
        new AcpMcpServer(grant.Server.Name, grant.Server.Command[0], grant.Server.Command.Skip(1).ToList(), grant.Server.Environment))];

    /// <summary>Each approved read and accepted write, as the harness spells a server's tool, <c>mcp__&lt;server&gt;__&lt;tool&gt;</c>.</summary>
    public IReadOnlyList<string> Allow => [.. Granted.SelectMany(grant =>
        grant.Server.Allow.Concat(grant.Accepted).Select(tool => $"mcp__{grant.Server.Name}__{tool}"))];

    /// <summary>Each write of an approved server the person did not accept: asked, which Ask Daoris's asking mode refuses.</summary>
    public IReadOnlyList<string> Ask => [.. Granted.SelectMany(grant =>
        grant.Server.Writes.Except(grant.Accepted, StringComparer.Ordinal).Select(tool => $"mcp__{grant.Server.Name}__{tool}"))];

    /// <summary>
    /// The browser's grant of Ask Daoris's configured access (HELPMODE1c3, the modes design §4.1): the server declared for it that
    /// attaches to Daoris's own browser (<see cref="InAppBrowserServers.Placeholder"/> in its command or environment), the first such;
    /// null where no plugin here declares one. *Configured access* draws its state from this, read only, and leads to its plugin's tab.
    /// </summary>
    public HelpGrant? Browser => All.FirstOrDefault(grant =>
        grant.Server.Command.Any(part => part.Contains(InAppBrowserServers.Placeholder, StringComparison.Ordinal))
        || grant.Server.Environment.Values.Any(value => value.Contains(InAppBrowserServers.Placeholder, StringComparison.Ordinal)));
}
