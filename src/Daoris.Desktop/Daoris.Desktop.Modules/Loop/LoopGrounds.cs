using System.Security.Cryptography;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The loop's readers of a person's press's grounds (CONFIRMWAIT1a-i, the confirm-wait design §5.2), handed to the pipe as the standing
/// confirmation's are: the service's records of a session, a quest and an ask, its registry, a branch's tip by one <c>git rev-parse</c>
/// in the repository's checkout, a session's tree's head as the landing reads its tree, the driver's own reads called in process
/// through the pipe's dispatcher, the waits the loop holds that no read lists by the press's id, and the files a press copies, runs or
/// would lose. Each throws where it cannot read, which the pipe takes as a read that failed.
/// </summary>
public sealed class LoopGrounds(DriverLoop loop, InstancePipe pipe) : IPipeGroundWorld
{
    public async Task<(string State, DateTimeOffset Updated)?> SessionAsync(string id, CancellationToken cancellationToken)
    {
        var records = await Service().SessionRecordsAsync(cancellationToken).ConfigureAwait(false);
        return records.FirstOrDefault(each => each.Id == id) is { } found ? (found.State, found.Updated) : null;
    }

    public async Task<bool> RegisteredAsync(string name, CancellationToken cancellationToken)
    {
        var registry = await Service().RegistryAsync(cancellationToken).ConfigureAwait(false);
        return registry.Any(each => string.Equals(each.Repository, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(each.Workspace, name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<string?> QuestAsync(string id, CancellationToken cancellationToken) =>
        (await Service().FindQuestAsync(id, cancellationToken).ConfigureAwait(false))?.Status;

    public async Task<string?> AskAsync(string id, CancellationToken cancellationToken) =>
        (await Service().FindAskAsync(id, cancellationToken).ConfigureAwait(false))?.State;

    public async Task<string?> TipAsync(string repository, string branch, CancellationToken cancellationToken)
    {
        var registry = await Service().RegistryAsync(cancellationToken).ConfigureAwait(false);
        var root = registry.FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase))?.Root;
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return null;
        return await WorkingTree.TipAsync(root, branch, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> SessionHeadAsync(string session, CancellationToken cancellationToken)
    {
        var tree = await TreeOfAsync(session, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(tree) || !Directory.Exists(tree)) return null;
        return await WorkingTree.HeadAsync(tree, cancellationToken).ConfigureAwait(false);
    }

    public Task<JsonElement> ReadAsync(string module, string route, object? payload, CancellationToken cancellationToken) =>
        pipe.ReadAsync(module, route, payload, cancellationToken);

    public async Task<string?> HeldAsync(string wait, JsonElement? payload, CancellationToken cancellationToken)
    {
        switch (wait)
        {
            case "opinion":
                // A second opinion being read (STOP_OPINION): its state, which the stop judges by.
                return PipeGround.Text(payload, "opinion") is { Length: > 0 } opinion
                    ? (await Service().Opinions.ReadAsync(opinion, cancellationToken).ConfigureAwait(false))?.State
                    : null;
            case "workflow-node":
            {
                // A run's node (WORKFLOW_NODE): the run's end, a word already kept for the node, and where the node stands.
                if (PipeGround.Id(PipeGround.Text(payload, "run")) is not { } run || PipeGround.Text(payload, "node") is not { } node) return null;
                var records = new NodeRecordFiles(loop.Home);
                if (new GraphRuns(records).Find(run) is not { } bound) return null;
                if (bound.Ended is not null) return "ended";
                var record = records.Read(bound.Unit);
                if (record is null) return "unrecorded";
                if (record.Pending.Any(each => each.Node == node)) return "worded";
                return record.Statuses.GetValueOrDefault(node)?.State ?? "start";
            }

            case "go-ahead":
            {
                // An ask's go-ahead (SESSION_GO_AHEAD), as the service holds it by its number: open while it has no answer.
                if (PipeGround.Id(PipeGround.Text(payload, "ask")) is not { } ask
                    || payload is not { ValueKind: JsonValueKind.Object } body || !body.TryGetProperty("number", out var named)
                    || !named.TryGetInt32(out var number))
                {
                    return null;
                }

                var view = await Service().FindAskAsync(ask, cancellationToken).ConfigureAwait(false);
                return view?.GoAheads?.FirstOrDefault(each => each.Number == number) is { } goAhead
                    ? goAhead.Answer is null ? "open" : "answered"
                    : null;
            }

            case "trust":
                // The trust hold the loop keeps for a folder (TRUST_FOLDER), from its last tick.
                return PipeGround.Text(payload, "folder") is { } folder && PipeGround.Text(payload, "trustFile") is { } file
                    && loop.Trust.Holding(folder, file) is not null
                    ? "held"
                    : null;
            case "help":
                // An Ask Daoris proposal (HELP_APPLY, HELP_DISMISS), read from its record: its list's read settles and draws what it lists.
                return PipeGround.Text(payload, "id") is { } id ? HelpProposals.Find(loop.Home, PipeGround.Id(id) ?? id)?.State : null;
            default:
                throw new ArgumentOutOfRangeException(nameof(wait), wait, "no such wait.");
        }
    }

    public async Task<string> FingerprintAsync(string what, JsonElement? payload, CancellationToken cancellationToken)
    {
        switch (what)
        {
            case "plugin-folder":
                return Files(Rooted(PipeGround.Text(payload, "folder")), cancellationToken);
            case "plugin-new":
                return Rooted(PipeGround.Text(payload, "folder")) is { } parent && PipeGround.Text(payload, "id") is { Length: > 0 } made
                    ? Files(Path.Combine(parent, made), cancellationToken)
                    : "none";
            case "plugin-try":
            {
                var installed = PipeGround.Text(payload, "id") is { Length: > 0 } plugin
                    ? PluginCatalog.Load(loop.Home).Plugins.FirstOrDefault(each => string.Equals(each.Manifest.Id, plugin, StringComparison.OrdinalIgnoreCase))?.Folder
                    : null;
                return Files(installed ?? Rooted(PipeGround.Text(payload, "folder")), cancellationToken);
            }

            case "plugin-offer":
            {
                var offer = PipeGround.Text(payload, "offer");
                var offered = PluginOffers.Load(PluginOffers.FolderFor(loop.Home, AppContext.BaseDirectory), loop.Home, AdapterSet.Built().Names)
                    .FirstOrDefault(each => string.Equals(each.Id, offer, StringComparison.OrdinalIgnoreCase));
                return Files(offered?.Folder, cancellationToken);
            }

            case "plugin-update":
            {
                // Its own preview, and the files of the source it copies from (the confirm-wait design §5.1).
                var preview = await pipe.ReadAsync(
                    DriverModule.Name, "PLUGIN_UPDATE", new { id = PipeGround.Text(payload, "id"), shipped = Flag(payload, "shipped") }, cancellationToken)
                    .ConfigureAwait(false);
                var from = preview.TryGetProperty("from", out var source) && source.ValueKind == JsonValueKind.String ? source.GetString() : null;
                return PipeGround.Fingerprint([
                    preview.TryGetProperty("refusal", out var refusal) ? refusal.GetRawText() : null,
                    preview.TryGetProperty("changes", out var changes) ? changes.GetRawText() : null,
                    Files(from, cancellationToken),
                ]);
            }

            case "tree-uncommitted":
            {
                // A forced discard skips its proof, so what it would lose is its ground: each uncommitted path, by its bytes.
                var tree = PipeGround.Text(payload, "id") is { Length: > 0 } session
                    ? await TreeOfAsync(session, cancellationToken).ConfigureAwait(false)
                    : Rooted(PipeGround.Text(payload, "path"));
                if (tree is null || !Directory.Exists(tree)) return "none";
                var paths = await WorkingTree.UncommittedAsync(tree, cancellationToken).ConfigureAwait(false)
                    ?? throw new IOException("git did not say what the tree holds uncommitted.");
                return PipeGround.Fingerprint(paths.Order(StringComparer.Ordinal).Select(path => $"{path}={Hash(Path.Combine(tree, path))}"));
            }

            case "file":
                return Hash(Rooted(PipeGround.Text(payload, "file")));
            case "declaration":
                return Rooted(PipeGround.Text(payload, "path")) is { } repository ? Hash(Path.Combine(repository, "daoris.json")) : "none";
            case "notices":
            {
                if (PipeGround.Text(payload, "id") is { Length: > 0 } dismissed)
                {
                    return Notices.Read(loop.Home, DateTimeOffset.UtcNow)?.FirstOrDefault(each => each.Id == dismissed) is { } notice
                        ? $"{notice.Id}|{notice.Read}"
                        : "gone";
                }

                return PipeGround.Fingerprint(loop.Notices.Scoped(PipeGround.Text(payload, "workspace")).Order(StringComparer.Ordinal));
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(what), what, "no such files.");
        }
    }

    private async Task<string?> TreeOfAsync(string session, CancellationToken cancellationToken) =>
        (await Service().SessionGroundAsync(session, cancellationToken).ConfigureAwait(false)).Tree;

    /// <summary>Every file under <paramref name="folder"/> by its path relative to it and its SHA-256; <c>none</c> where it is absent.</summary>
    private static string Files(string? folder, CancellationToken cancellationToken)
    {
        if (folder is null || !Directory.Exists(folder)) return "none";
        var files = new List<string>();
        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            files.Add($"{Path.GetRelativePath(folder, file).Replace('\\', '/')}={Hash(file)}");
        }

        return PipeGround.Fingerprint(files.Order(StringComparer.Ordinal));
    }

    /// <summary>A file's SHA-256, <c>none</c> where it is absent.</summary>
    private static string Hash(string? path)
    {
        if (path is null || !File.Exists(path)) return "none";
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static string? Rooted(string? path) => path is { Length: > 0 } && Path.IsPathRooted(path) ? Path.GetFullPath(path) : null;

    private static bool Flag(JsonElement? payload, string name) =>
        payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private ServiceClient Service() => loop.Service ?? throw new InvalidOperationException("the service is not ready.");
}
