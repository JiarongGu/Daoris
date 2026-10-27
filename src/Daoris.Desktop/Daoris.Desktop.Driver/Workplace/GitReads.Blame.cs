using System.Globalization;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// <c>git blame --porcelain</c>, read as it comes (GIT1b, D147 §2.5): each line of the file with the commit that last
/// changed it, a commit's details said once, and D111's bound and binary test applied as the lines arrive.
/// </summary>
/// <remarks>
/// <para><b>The porcelain form.</b> Every line of the file is a header (<c>&lt;id&gt; &lt;its line there&gt; &lt;its line
/// here&gt;</c>, and a count on a group's first), the commit's details the first time it appears (<c>author</c>,
/// <c>summary</c>, <c>boundary</c> and the rest), the <c>previous</c> commit and path and the <c>filename</c> where git writes
/// them, then the line itself after a tab. A commit seen before is its header alone, and keeps the file name it had.</para>
///
/// <para><b>D111's bound and binary test hold.</b> A NUL in the first <see cref="FilePreview.BinaryProbe"/> bytes is git's own
/// test for a binary file, which has a history and no blame. A file is shown to its first <see cref="FilePreview.Budget"/>
/// bytes, cut at a line's end, and the blame says the last line it shows. Either way nothing more of git's answer is wanted,
/// and <see cref="Take"/> answers false so git is stopped.</para>
/// </remarks>
public sealed class GitBlameSplit
{
    private readonly StringBuilder _pending = new();
    private readonly Dictionary<string, GitBlameCommit> _commits = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Path, string? Previous, string? PreviousPath)> _origins = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _details = new(StringComparer.Ordinal);
    private readonly List<GitBlameRun> _runs = [];
    private readonly List<GitBlameLine> _lines = [];

    private bool _header = true;
    private string _commit = "";
    private int _original;
    private int _final;
    private GitBlameRun? _run;
    private long _bytes;
    private bool _binary;
    private int? _stoppedAt;

    /// <summary>Whether nothing more of git's answer is wanted: the file is binary, or the bound is reached.</summary>
    public bool Stopped { get; private set; }

    /// <summary>Read the next piece of git's answer; false once nothing more is wanted.</summary>
    public bool Take(ReadOnlySpan<char> piece)
    {
        while (piece.Length > 0 && !Stopped)
        {
            var newline = piece.IndexOf('\n');
            if (newline < 0)
            {
                _pending.Append(piece);
                break;
            }

            _pending.Append(piece[..newline]);
            piece = piece[(newline + 1)..];
            var line = _pending.ToString();
            _pending.Clear();
            Line(line);
        }

        return !Stopped;
    }

    /// <summary>The blame as a page shows it, once git's answer has ended or been stopped.</summary>
    public GitBlame End(string commit, string path)
    {
        if (!Stopped && _pending.Length > 0)
        {
            Line(_pending.ToString());
            _pending.Clear();
        }

        Close();
        if (_binary) return new GitBlame(commit, path) { Binary = true };

        var shown = _runs.Select(run => run.Commit).ToHashSet(StringComparer.Ordinal);
        return new GitBlame(commit, path)
        {
            Runs = [.. _runs],
            Commits = _commits.Where(each => shown.Contains(each.Key)).ToDictionary(each => each.Key, each => each.Value, StringComparer.Ordinal),
            StoppedAt = _stoppedAt,
        };
    }

    private void Line(string line)
    {
        if (line.Length > 0 && line[0] == '\t')
        {
            Content(line[1..]);
            _header = true;
            return;
        }

        if (_header)
        {
            // `<id> <its line there> <its line here> [<lines in the group>]`
            var parts = line.Split(' ');
            if (parts.Length < 3 || !GitReads.IsWholeId(parts[0])
                || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out _original)
                || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out _final))
            {
                return;
            }

            _commit = parts[0].ToLowerInvariant();
            _details.Clear();
            _header = false;
            return;
        }

        var space = line.IndexOf(' ');
        if (space < 0) _details[line] = "";
        else _details[line[..space]] = line[(space + 1)..];
    }

    private void Content(string text)
    {
        if (_commit.Length == 0) return;

        var bytes = (long)Encoding.UTF8.GetByteCount(text) + 1;
        var nul = text.IndexOf('\0');
        if (nul >= 0 && _bytes + Encoding.UTF8.GetByteCount(text.AsSpan(0, nul)) < FilePreview.BinaryProbe)
        {
            _binary = true;
            Stopped = true;
            return;
        }

        if (_bytes + bytes > FilePreview.Budget)
        {
            _stoppedAt = _final - 1;
            Stopped = true;
            return;
        }

        _bytes += bytes;
        Remember();
        var (path, previous, previousPath) = _origins.GetValueOrDefault(_commit, ("", null, null));
        if (_run is null || _run.Commit != _commit || _run.Path != path || _run.Line + _lines.Count != _final)
        {
            Close();
            _run = new GitBlameRun(_commit, _final, path) { Previous = previous, PreviousPath = previousPath };
        }

        _lines.Add(new GitBlameLine(_original, text));
    }

    /// <summary>What this line's header block said of its commit: its details the first time, its file and previous where written.</summary>
    private void Remember()
    {
        if (!_commits.ContainsKey(_commit) && _details.ContainsKey("author"))
        {
            _commits[_commit] = new GitBlameCommit(
                _commit, _details.GetValueOrDefault("author", ""), Address(_details.GetValueOrDefault("author-mail", "")),
                _details.GetValueOrDefault("summary", ""))
            {
                AuthoredAt = Time(_details.GetValueOrDefault("author-time"), _details.GetValueOrDefault("author-tz")),
                Boundary = _details.ContainsKey("boundary"),
            };
        }

        if (_details.TryGetValue("filename", out var file))
        {
            string? previous = null, previousPath = null;
            if (_details.TryGetValue("previous", out var said) && said.IndexOf(' ') is var space and > 0 && GitReads.IsWholeId(said[..space]))
            {
                previous = said[..space].ToLowerInvariant();
                previousPath = GitFormats.Unquoted(said[(space + 1)..]);
            }

            _origins[_commit] = (GitFormats.Unquoted(file), previous, previousPath);
        }

        _details.Clear();
    }

    private void Close()
    {
        if (_run is null) return;
        _runs.Add(_run with { Lines = [.. _lines] });
        _lines.Clear();
        _run = null;
    }

    private static string Address(string mail) => mail.Length >= 2 && mail[0] == '<' && mail[^1] == '>' ? mail[1..^1] : mail;

    /// <summary>A porcelain time: seconds since the epoch and the zone it was made in (<c>+1100</c>), or null where either does not read.</summary>
    private static DateTimeOffset? Time(string? seconds, string? zone)
    {
        if (!long.TryParse(seconds, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var at)) return null;
        var when = DateTimeOffset.FromUnixTimeSeconds(at);
        if (zone is not { Length: 5 } || zone[0] is not ('+' or '-')
            || !int.TryParse(zone.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            || !int.TryParse(zone.AsSpan(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes))
        {
            return when;
        }

        var offset = new TimeSpan(hours, minutes, 0);
        return when.ToOffset(zone[0] == '-' ? -offset : offset);
    }
}
