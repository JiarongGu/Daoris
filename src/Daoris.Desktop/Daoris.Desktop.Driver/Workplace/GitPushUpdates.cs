using System.Text;

namespace Daoris.Driver;

/// <summary>
/// What a push asks of one ref. <see cref="Plain"/> is a branch made or fast-forwarded; every other kind is Dangerous: a force (it moves
/// the ref off its history), a delete, a tag, a mirror (a ref outside <c>refs/heads/</c> and <c>refs/tags/</c>) and a prune (a delete
/// beside updates in one push).
/// </summary>
public enum GitPushKind
{
    Plain,
    Force,
    Delete,
    Tag,
    Mirror,
    Prune,
}

/// <summary>One ref a push updates, as git's receive-pack commands name it: the ref's old and new object, and its name.</summary>
public sealed record GitPushUpdate(string Old, string New, string Ref)
{
    /// <summary>Whether it deletes the ref: its new object is all zeros.</summary>
    public bool Deletes => New.All(c => c == '0');

    /// <summary>Whether it makes the ref: its old object is all zeros.</summary>
    public bool Makes => Old.All(c => c == '0');
}

/// <summary>
/// The updates a push holds, each judged by its kind at both doors, for every agent, whatever its harness's
/// rules say. The ssh door reads receive-pack's commands from git's side before it forwards them; the remote helper builds the same
/// updates from the remote's refs and its sources' objects (<see cref="GitHelperPush"/>). Each asks git whether an update keeps its
/// ref's history.
/// </summary>
/// <remarks>The base library alone: the doors compile it in (<c>Daoris.Desktop.GitDoor</c>).</remarks>
public static class GitPushUpdates
{
    /// <summary>The line a refused dangerous update says, where git prints it.</summary>
    public static string DeniedLine(GitPushKind kind) =>
        $"daoris: Daoris's git access refuses {Said(kind)} from here: Dangerous is the person's to allow, for the repository, the workspace or this machine.";

    private static string Said(GitPushKind kind) => kind switch
    {
        GitPushKind.Force => "a force-push",
        GitPushKind.Delete => "a delete of a ref",
        GitPushKind.Tag => "a push of a tag",
        GitPushKind.Mirror => "a mirror push",
        GitPushKind.Prune => "a prune of refs",
        _ => "this push",
    };

    /// <summary>
    /// What one receive-pack update asks: a delete where its new object is zeros; a tag under <c>refs/tags/</c>; a mirror's ref outside
    /// <c>refs/heads/</c>; a force where it moves an existing ref to an object its old one is not an ancestor of
    /// (<paramref name="keepsHistory"/>, asked of git where the push runs); else a plain update.
    /// </summary>
    public static GitPushKind KindOf(GitPushUpdate update, Func<string, string, bool> keepsHistory) =>
        update.Deletes ? GitPushKind.Delete
        : update.Ref.StartsWith("refs/tags/", StringComparison.Ordinal) ? GitPushKind.Tag
        : !update.Ref.StartsWith("refs/heads/", StringComparison.Ordinal) ? GitPushKind.Mirror
        : update.Makes || keepsHistory(update.Old, update.New) ? GitPushKind.Plain
        : GitPushKind.Force;

    /// <summary>The kind of each update in a batch: a delete beside any other update is a prune.</summary>
    public static IReadOnlyList<GitPushKind> KindsOf(IReadOnlyList<GitPushUpdate> updates, Func<string, string, bool> keepsHistory)
    {
        var kinds = updates.Select(update => KindOf(update, keepsHistory)).ToList();
        if (updates.Any(update => !update.Deletes))
        {
            for (var index = 0; index < kinds.Count; index++)
            {
                if (kinds[index] == GitPushKind.Delete) kinds[index] = GitPushKind.Prune;
            }
        }

        return kinds;
    }

    /// <summary>
    /// Read receive-pack's commands from git's side of an ssh push, as pkt-lines, up to the flush that ends them: every byte read, to be
    /// forwarded as it was, and each update they name. A shallow line, a push certificate's other lines and the capabilities after the
    /// first command's NUL name no update. Null where the stream ends or a pkt-line does not read before the flush.
    /// </summary>
    public static (byte[] Read, IReadOnlyList<GitPushUpdate> Updates)? ReadCommands(Stream input)
    {
        var read = new MemoryStream();
        var updates = new List<GitPushUpdate>();
        var head = new byte[4];
        while (true)
        {
            if (!Fill(input, head, read)) return null;
            if (!int.TryParse(Encoding.ASCII.GetString(head), System.Globalization.NumberStyles.HexNumber, null, out var length)) return null;
            if (length == 0) return (read.ToArray(), updates);
            if (length < 4) continue;
            var payload = new byte[length - 4];
            if (!Fill(input, payload, read)) return null;
            if (Update(Encoding.UTF8.GetString(payload)) is { } update) updates.Add(update);
        }
    }

    /// <summary>One command's update: <c>&lt;old&gt; &lt;new&gt; &lt;ref&gt;</c>, its capabilities after a NUL left off; null for any other line.</summary>
    internal static GitPushUpdate? Update(string line)
    {
        var text = line.Split('\0')[0].TrimEnd('\n');
        var words = text.Split(' ');
        return words.Length == 3 && IsObject(words[0]) && IsObject(words[1]) && words[2].Length > 0 ? new GitPushUpdate(words[0], words[1], words[2]) : null;
    }

    internal static bool IsObject(string word) => word.Length is 40 or 64 && word.All(char.IsAsciiHexDigitLower);

    private static bool Fill(Stream input, byte[] buffer, MemoryStream read)
    {
        var at = 0;
        while (at < buffer.Length)
        {
            var got = input.Read(buffer, at, buffer.Length - at);
            if (got <= 0) return false;
            at += got;
        }

        read.Write(buffer, 0, buffer.Length);
        return true;
    }
}
