using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The one judge of who is at work (COWORKVIEW1a, the design §4.1, rules 11 to 13): each landed composition registers what it read,
/// and the judge, asked by the lineage files' topic, the two slices' pushes and health and the token set's moves, compares each read's
/// own versions and tells <paramref name="wake"/> once where any moved, that read then leaving the union until it is asked again and
/// lands. A line wakes only where it moves what its reading holds (<see cref="ChildBindings.Wakes"/>).
/// </summary>
public sealed class AtWorkWatch
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Action _wake;
    private readonly StateSlice? _groups;
    private readonly StateSlice? _sessions;
    private readonly LineageTokens _tokens;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, AtWorkDependencies> _held = new(StringComparer.Ordinal);

    public AtWorkWatch(Action wake, StateSlice? groups, StateSlice? sessions, LineageTokens tokens)
    {
        (_wake, _groups, _sessions, _tokens) = (wake, groups, sessions, tokens);
        foreach (var slice in new[] { groups, sessions })
        {
            if (slice is null) continue;
            slice.Pushed += () => Judge();
            slice.HealthMoved += () => Judge();
        }

        tokens.Moved += _ => Judge();
    }

    /// <summary>How many reads the union holds.</summary>
    public int Holding
    {
        get
        {
            lock (_gate) return _held.Count;
        }
    }

    /// <summary>A composition landed for <paramref name="key"/>: its dependencies join the union, judged at once.</summary>
    public void Register(string key, AtWorkDependencies read)
    {
        lock (_gate) _held[key] = read;
        Judge();
    }

    /// <summary>The source fence (rule 11): whether every input <paramref name="read"/> read is still as it read it.</summary>
    public bool Stands(AtWorkDependencies read) => Moved(read) is not { Woke: true };

    /// <summary>The lineage topic's judge: whether a read woke.</summary>
    public bool JudgeFiles() => Judge();

    /// <summary>Each read compared with its own versions; one that moved leaves the union, and any tells the wake once.</summary>
    public bool Judge()
    {
        var woke = false;
        lock (_gate)
        {
            foreach (var (key, read) in _held.ToList())
            {
                if (Moved(read) is not { } now)
                {
                    continue;
                }

                if (now.Woke)
                {
                    _held.Remove(key);
                    woke = true;
                }
                else
                {
                    _held[key] = now.Read;
                }
            }
        }

        if (woke) _wake();
        return woke;
    }

    /// <summary>Null where nothing moved; else whether it woke, or the read with its files read on past lines that moved nothing.</summary>
    private (bool Woke, AtWorkDependencies Read)? Moved(AtWorkDependencies read)
    {
        if (!AtWorkRead.SlicesStand(read, _groups, _sessions)) return (true, read);
        var files = new Dictionary<string, AtWorkFile>(read.Files, StringComparer.Ordinal);
        var readOn = false;
        foreach (var (part, file) in read.Files)
        {
            if (file.Reading.Bindings.Readings.Any(each => _tokens.Reading(each.Key) != each.Value)) return (true, read);
            switch (Lines(file))
            {
                case null:
                    continue;
                case { } on when on.Woke:
                    return (true, read);
                case { } on:
                    files[part] = on.File;
                    readOn = true;
                    break;
            }
        }

        return readOn ? (false, read with { Files = files }) : null;
    }

    /// <summary>
    /// What a file's new lines say against what was read: null where its stamp stands; else whether one wakes, or the file read on to
    /// its last full line where none does.
    /// </summary>
    private static (bool Woke, AtWorkFile File)? Lines(AtWorkFile file)
    {
        if (file.Path.Length == 0) return null;
        var (size, ticks) = AtWorkFile.Stamp(file.Path);
        if (size == file.Size && ticks == file.Ticks) return null;
        var reading = file.Reading;
        if (reading.Outcome != LineageReading.Found || size < reading.Length) return (true, file);

        byte[] past;
        try
        {
            using var stream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            stream.Seek(reading.Length, SeekOrigin.Begin);
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            past = buffer.ToArray();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (true, file);
        }

        var whole = Array.LastIndexOf(past, (byte)'\n') + 1;
        foreach (var line in Utf8.GetString(past, 0, whole).Split('\n'))
        {
            if (line.Length > 0 && reading.Bindings.Wakes(line)) return (true, file);
        }

        return (false, file with { Size = size, Ticks = ticks, Reading = reading with { Length = reading.Length + whole } });
    }
}
