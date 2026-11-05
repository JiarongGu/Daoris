using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A push as the remote helper sees it (D241 point 3; the git-auth design §6.5): git's conversation names refspecs, so the helper keeps the
/// remote's refs from the <c>list for-push</c> answer as it passes, resolves each source to its object once, judges the batch as the ssh
/// door judges receive-pack's commands, and forwards the object ids it judged in place of the source names. A ref that moves after the
/// judgement cannot change what is pushed.
/// </summary>
/// <remarks>The base library alone: the remote helper compiles it in (<c>Daoris.Desktop.GitDoor</c>). One instance per helper process.</remarks>
public sealed class GitHelperPush
{
    private readonly object _gate = new();
    private readonly Dictionary<string, string> _refs = new(StringComparer.Ordinal);
    private readonly StringBuilder _line = new();
    private bool _listing;
    private bool _listed;

    /// <summary>The batch a push asks, judged as updates, and the lines to forward in place of git's.</summary>
    public sealed record Batch(IReadOnlyList<GitPushUpdate> Updates, IReadOnlyList<string> Lines);

    /// <summary>Git asked <c>list for-push</c>: the transport's answer up to its blank line is the remote's refs.</summary>
    public void Listing()
    {
        lock (_gate)
        {
            _refs.Clear();
            _line.Clear();
            _listing = true;
            _listed = false;
        }
    }

    /// <summary>Take the transport's bytes as they pass to git; they are read as the answer to <see cref="Listing"/> and nothing else.</summary>
    public void Feed(byte[] buffer, int count)
    {
        lock (_gate)
        {
            if (!_listing) return;
            for (var index = 0; index < count && _listing; index++)
            {
                if (buffer[index] != '\n')
                {
                    _line.Append((char)buffer[index]);
                    continue;
                }

                var text = _line.ToString().TrimEnd('\r');
                _line.Clear();
                if (text.Length == 0)
                {
                    _listing = false;
                    _listed = true;
                    continue;
                }

                var words = text.Split(' ');
                if (words.Length >= 2 && GitPushUpdates.IsObject(words[0])) _refs[words[1]] = words[0];
            }
        }
    }

    /// <summary>The remote's refs, or null where its answer has not ended.</summary>
    public IReadOnlyDictionary<string, string>? Refs
    {
        get
        {
            lock (_gate) return _listed ? new Dictionary<string, string>(_refs, StringComparer.Ordinal) : null;
        }
    }

    /// <summary>
    /// The batch for git's <c>push</c> lines: each update's old object from <paramref name="remote"/> (zeros for a ref it has not), its new
    /// one from <paramref name="resolve"/> (zeros for a delete). Null where a source does not resolve or a line is no push: nothing is
    /// judged that could not be read, so nothing is forwarded.
    /// </summary>
    public static Batch? Build(IReadOnlyList<string> pushLines, IReadOnlyDictionary<string, string> remote, Func<string, string?> resolve)
    {
        var updates = new List<GitPushUpdate>();
        var lines = new List<string>();
        foreach (var line in pushLines)
        {
            if (!line.StartsWith("push ", StringComparison.Ordinal)) return null;
            var spec = line["push ".Length..].Trim();
            var force = spec.StartsWith('+');
            if (force) spec = spec[1..];
            var colon = spec.IndexOf(':');
            if (colon < 0 || colon == spec.Length - 1) return null;
            var source = spec[..colon];
            var destination = spec[(colon + 1)..];
            var oid = source.Length == 0 ? null : resolve(source);
            if (source.Length > 0 && oid is null) return null;
            var old = remote.TryGetValue(destination, out var there) ? there : null;
            var zeros = new string('0', (oid ?? old)?.Length ?? 40);
            updates.Add(new GitPushUpdate(old ?? zeros, oid ?? zeros, destination));
            lines.Add(oid is null ? $"push :{destination}" : $"push {(force ? "+" : "")}{oid}:{destination}");
        }

        return updates.Count == 0 ? null : new Batch(updates, lines);
    }
}
