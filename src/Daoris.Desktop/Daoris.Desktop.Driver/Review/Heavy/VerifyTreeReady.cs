using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>What makes a verification tree ready: its dependencies by the lockfiles that key them and the declared local inputs, as the files stage makes a session's; a seam for a test.</summary>
internal interface IVerifyPreparation
{
    /// <summary>The words that say which inputs the tree stands on (each dependency's key and the declared local inputs), or null where git cannot say.</summary>
    Task<string?> StampAsync(string home, string checkout, string tree, string repository, CancellationToken ct);

    /// <summary>Prepares <paramref name="tree"/> from <paramref name="checkout"/>'s declaration: the sentence of what did not finish, or null.</summary>
    Task<string?> RunAsync(string home, string checkout, string tree, string repository, CancellationToken ct);
}

internal sealed class VerifyPreparation : IVerifyPreparation
{
    internal static readonly VerifyPreparation Real = new();

    public async Task<string?> StampAsync(string home, string checkout, string tree, string repository, CancellationToken ct)
    {
        var head = await WorkingTree.HeadAsync(tree, ct).ConfigureAwait(false);
        if (head is null) return null;
        var deps = await StoreDependencyRules.ReadAsync(home, tree, repository, head, ct).ConfigureAwait(false);
        var keys = await DependencyKeys.EachAsync(home, tree, head, repository, deps.Each, ct).ConfigureAwait(false);
        return Of(keys, TreeLocalInputs.Declared(checkout), checkout);
    }

    /// <summary>The stamp over every input the preparation reads; a new input goes in here, and the stamp test moves it.</summary>
    internal static string Of(IReadOnlyList<string> keys, IReadOnlyList<LocalInput> localInputs, string checkout)
    {
        var words = new List<string>(keys);
        words.AddRange(localInputs.Select(input => "local:" + input.Path + "=" + ContentOf(Path.Combine(checkout, input.Path))));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', words))));
    }

    private static string ContentOf(string file)
    {
        try
        {
            return File.Exists(file) ? Convert.ToHexString(SHA256.HashData(AtomicFile.ReadBytes(file))) : "absent";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return "absent";
        }
    }

    public async Task<string?> RunAsync(string home, string checkout, string tree, string repository, CancellationToken ct)
    {
        TreeFilesRecord.Mark(home, tree);
        var said = new List<Noted>();
        var outcome = await TreeFilesStep.RunAsync(home, checkout, tree, repository, said.Add, ct).ConfigureAwait(false);
        TreeFilesStep.Hand(home, tree);
        var failed = outcome.Said.Where(line => outcome.Stopped || line.Parts.Any(part => part.Code is { } code
            && (code == StartNoteCodes.StartedFilesFailed.Code || code == StartNoteCodes.StartedDepsUnplaced.Code))).ToList();
        return failed.Count == 0 && !outcome.Stopped ? null : string.Join(' ', failed.Select(line => line.Note));
    }
}

/// <summary>
/// A check's verification tree prepared before its gates run: the preparation asked once for the tree, then again only where the
/// inputs it stands on moved (the stamp is the dependency keys the stage itself reads and the declared local inputs).
/// </summary>
internal static class VerifyTreeReady
{
    /// <summary>The sentence a failed preparation ends its attempt with; null where the tree stands ready.</summary>
    internal static async Task<string?> EnsureAsync(string home, VerifySend send, string tree, IVerifyPreparation preparation, CancellationToken ct)
    {
        var stamp = await preparation.StampAsync(home, send.Checkout, tree, send.Repository, ct).ConfigureAwait(false);
        var file = Path.Combine(VerifyFiles.LinesFolder(home), "prepared", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tree)))[..16] + ".stamp");
        if (stamp is not null && ReadStamp(file) == stamp) return null;
        if (await preparation.RunAsync(home, send.Checkout, tree, send.Repository, ct).ConfigureAwait(false) is { } failed) return failed.Length == 0 ? "it did not finish" : failed;
        if (stamp is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            AtomicFile.WriteText(file, stamp);
        }

        return null;
    }

    private static string? ReadStamp(string file)
    {
        try
        {
            return File.Exists(file) ? AtomicFile.ReadText(file) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
