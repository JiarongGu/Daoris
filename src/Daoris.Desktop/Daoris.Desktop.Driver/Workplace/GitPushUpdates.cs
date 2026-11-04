using System.Text;

namespace Daoris.Driver;

/// <summary>What a push asks of one ref: a plain update, a force (it may move the ref off its history), or a delete.</summary>
public enum GitPushKind
{
    Plain,
    Force,
    Delete,
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
/// The force-pushes and branch deletes a push holds (D233 choice 3), read at both doors so the doors judge them for every agent, whatever
/// its harness's rules say. The remote helper reads git's <c>push</c> lines (<c>+src:dst</c> forces, <c>:dst</c> deletes); the ssh door
/// reads receive-pack's commands from git's side before it forwards them, and asks git whether each update keeps the ref's history.
/// </summary>
/// <remarks>The base library alone: the doors compile it in (<c>Daoris.Desktop.GitDoor</c>).</remarks>
public static class GitPushUpdates
{
    /// <summary>The line a refused force-push or branch delete says, where git prints it.</summary>
    public const string DeniedLine =
        "daoris: Daoris's git access refuses a force-push or a branch delete from here: it stays the person's, unless their git access allows it.";

    /// <summary>What a remote helper's <c>push &lt;refspec&gt;</c> line asks; null for any other line.</summary>
    public static GitPushKind? OfHelperPush(string line)
    {
        if (!line.StartsWith("push ", StringComparison.Ordinal)) return null;
        var spec = line["push ".Length..].Trim();
        return spec.StartsWith(':') ? GitPushKind.Delete : spec.StartsWith('+') ? GitPushKind.Force : GitPushKind.Plain;
    }

    /// <summary>The most a push asks: a delete or a force over a plain update.</summary>
    public static GitPushKind Most(IEnumerable<GitPushKind> kinds) =>
        kinds.Aggregate(GitPushKind.Plain, (most, kind) => kind == GitPushKind.Plain ? most : most == GitPushKind.Plain ? kind : most);

    /// <summary>
    /// What one receive-pack update asks: a delete where its new object is zeros; a force where it moves an existing ref to an object its
    /// old one is not an ancestor of (<paramref name="keepsHistory"/>, asked of git where the push runs); else a plain update.
    /// </summary>
    public static GitPushKind KindOf(GitPushUpdate update, Func<string, string, bool> keepsHistory) =>
        update.Deletes ? GitPushKind.Delete : update.Makes || keepsHistory(update.Old, update.New) ? GitPushKind.Plain : GitPushKind.Force;

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

    private static bool IsObject(string word) => word.Length is 40 or 64 && word.All(char.IsAsciiHexDigitLower);

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
