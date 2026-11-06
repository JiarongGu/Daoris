using System.Security.Cryptography;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The session binding files into the desktop store (D232; AGENTPROVIDER1t): each <c>&lt;session&gt;.harness.json</c>, keyed <c>adapter</c>,
/// is read into the store's <c>bindings</c> under an import receipt, its conversation replacing the row's since a file is the later
/// evidence, a copy of the file kept first. The file and the <c>.imported</c> set-aside an earlier build left are taken out, so nothing
/// reads a file after. A file that does not read as a conversation is kept in the copies and holds no binding.
/// </summary>
public sealed class BindingFilesStep : IHomeMigrationStep
{
    private const string Suffix = ".harness.json";
    private const string SetAside = ".imported";

    public string Name => "binding-files";

    public void Run(HomeMigrationRun run)
    {
        var folder = DaorisHome.Entry(run.Home, HomeEntry.Sessions);
        if (!Directory.Exists(folder)) return;
        var files = Directory.EnumerateFiles(folder, "*" + Suffix + "*")
            .Where(path => path.EndsWith(Suffix, StringComparison.Ordinal) || path.EndsWith(Suffix + SetAside, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();
        if (files.Count == 0) return;

        var location = DesktopStore.LocationIn(run.Home);
        run.Keep(location);
        using var store = DesktopStore.Open(location, StoreProcess.Options(() => StoreSettled.Yes));
        if (store.Held is { } held) throw new DriverException($"the desktop's store did not take the binding files: {held}");
        foreach (var path in files.Where(each => each.EndsWith(Suffix, StringComparison.Ordinal)))
        {
            Import(run, store, path);
        }

        foreach (var path in files)
        {
            run.Keep(path);
            File.Delete(path);
            run.Said(path);
        }
    }

    private static void Import(HomeMigrationRun run, DesktopStore store, string path)
    {
        var session = Path.GetFileName(path)[..^Suffix.Length];
        if (!SessionEventText.IsLocalId(session) || Read(path) is not var (door, conversation)) return;
        var bytes = File.ReadAllBytes(path);
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var receipt = Path.GetRelativePath(run.Home, path).Replace('\\', '/');

        // A file whose very content an import already holds was imported before a crash stopped short of taking it out: the store may have
        // kept a later conversation since, which this stale one must not replace.
        if (store.LastImport(BindingStore.Family, receipt) is { } last && last.Import.Sha256 == sha) return;
        var outcome = store.Import(
            new StoreImport(BindingStore.Family, receipt, bytes.Length, sha, 1, ImportClass.SetAside),
            write => BindingStore.Upsert(write, session, door, conversation));
        if (!outcome.Done) throw new DriverException($"the desktop's store did not take a binding file: {outcome.Said ?? "no reason given"}");
    }

    private static (string Door, string Conversation)? Read(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            return Json.Text(root, "adapter")?.Trim() is { Length: > 0 } door && Json.Text(root, "conversation")?.Trim() is { Length: > 0 } conversation
                ? (door, conversation)
                : null;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
