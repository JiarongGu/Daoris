using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// Where Daoris's own install may run (the tree-pool design §29.7 step 2): the repository's lock only where its rule's last install
/// here wrote git's common folder or it has none yet, then one install at a time per key, then a place in the machine's admission as a
/// heavy step, each taken in that order so two installs never wait on each other.
/// </summary>
internal static class InstallAdmission
{
    internal static async Task<IDisposable> TakeAsync(string home, string repository, string tree, StoreDependency dependency, string key, CancellationToken ct)
    {
        var holds = new List<IDisposable>();
        try
        {
            if (WroteCommon(home, repository, dependency.Path) != false)
            {
                holds.Add(await Task.Run(() => RecordLock.Take(RecordLock.PathOf(home, "deps-install-" + Short(repository)), DependencyCommands.InstallBound), ct).ConfigureAwait(false));
            }

            holds.Add(await Task.Run(() => RecordLock.Take(RecordLock.PathOf(home, "deps-key-" + key[..Math.Min(32, key.Length)]), DependencyCommands.InstallBound), ct).ConfigureAwait(false));
            holds.Add(await InstallRunner.Current.AdmitAsync(home, $"installing `{dependency.Folder}`", repository, tree, ct).ConfigureAwait(false));
            return new Held(holds);
        }
        catch
        {
            foreach (var hold in holds) hold.Dispose();
            throw;
        }
    }

    /// <summary>Whether the rule's last install in the repository wrote git's common folder: null where it has none yet.</summary>
    internal static bool? WroteCommon(string home, string repository, string path)
    {
        try
        {
            var file = RecordOf(home, repository);
            if (!File.Exists(file)) return null;
            using var document = JsonDocument.Parse(AtomicFile.ReadText(file));
            return document.RootElement.TryGetProperty(path, out var wrote) ? wrote.GetBoolean() : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>What the rule's install just did to git's common folder, kept for its next admission.</summary>
    internal static void Wrote(string home, string repository, string path, bool common)
    {
        var file = RecordOf(home, repository);
        using var held = RecordLock.Take(RecordLock.PathOf(home, "deps-common-" + Short(repository)));
        JsonObject state;
        try
        {
            state = File.Exists(file) ? JsonNode.Parse(AtomicFile.ReadText(file)) as JsonObject ?? [] : [];
        }
        catch (Exception error) when (error is IOException or JsonException)
        {
            state = [];
        }

        state[path] = common;
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        AtomicFile.WriteText(file, state.ToJsonString());
    }

    private static string RecordOf(string home, string repository) => DaorisHome.Entry(home, HomeEntry.TreeFiles, $"common.{Short(repository)}.json");

    private static string Short(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];

    private sealed class Held(List<IDisposable> holds) : IDisposable
    {
        public void Dispose()
        {
            for (var at = holds.Count - 1; at >= 0; at--) holds[at].Dispose();
        }
    }
}
