using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The file step of the act journal (GITAPP2j, D204; the git-writes design §5, §8 Resolve): a worktree file's bytes replaced by the plan's, only
/// where it still holds the bytes the plan was judged on. The bytes it replaces and the bytes it writes are files beside the record
/// (<see cref="Dir"/>), made before the step and let go of with the record's expiry; the postcondition is the file's digest, never an exit.
/// </summary>
internal static class ActFiles
{
    /// <summary>Where an act's files are kept: beside the desktop's store, one folder per act.</summary>
    public static string Dir(string home, string act) => Path.Combine(home, "act-files", act);

    /// <summary>An act's files let go of, with its record's expiry; nothing where there are none.</summary>
    public static void Remove(string home, string act)
    {
        try
        {
            if (Directory.Exists(Dir(home, act))) Directory.Delete(Dir(home, act), recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A folder that would not go is Daoris's own, named for its act, and goes with the next look.
        }
    }

    /// <summary>The digest a step compares: SHA-256 of the bytes, in hex.</summary>
    public static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>The step's effect: the file's bytes compared, then replaced atomically; a difference refuses, the file left as it is.</summary>
    public static async Task<ActStepEnd> EffectAsync(ActStep step)
    {
        var plan = JsonNode.Parse(step.Arguments)!;
        var (path, before, written) = (Text(plan, "path"), Text(plan, "before"), Text(plan, "written"));
        var relative = Text(plan, "relative");
        byte[] now;
        byte[] next;
        try
        {
            now = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
            next = await File.ReadAllBytesAsync(written).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new ActStepEnd(null, false, $"`{relative}` or the bytes kept for it could not be read ({error.Message}), so nothing was written", true);
        }

        if (Digest(now) != before) return new ActStepEnd(null, false, $"`{relative}`'s bytes changed since they were read, so nothing was written; the recovery is kept", true);
        if (Digest(next) != Text(plan, "after")) return new ActStepEnd(null, false, $"the bytes kept for `{relative}` are not the plan's, so nothing was written", true);

        var temp = path + $".daoris-{step.Act}";
        try
        {
            await File.WriteAllBytesAsync(temp, next).ConfigureAwait(false);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temp, File.GetUnixFileMode(path));
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Delete(temp);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // The copy beside the file is Daoris's own, named for its act.
            }

            return new ActStepEnd(null, false, $"`{relative}` could not be written ({error.Message}), so it was left as it was", true);
        }

        return new ActStepEnd(0, false, $"`{relative}`'s bytes were replaced", false);
    }

    /// <summary>What the file holds now: done at the plan's digest, not done at the one it replaced, else unknown; null where it cannot be read.</summary>
    public static async Task<string?> ReadAsync(JsonNode plan, CancellationToken ct)
    {
        try
        {
            var now = Digest(await File.ReadAllBytesAsync(Text(plan, "path"), ct).ConfigureAwait(false));
            return now == Text(plan, "after") ? ActOutcomes.Done : now == Text(plan, "before") ? ActOutcomes.NotDone : ActOutcomes.Unknown;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string Text(JsonNode node, string name) => node[name]!.GetValue<string>();
}
