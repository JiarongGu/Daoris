using System.Diagnostics;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>Where a conversation's turns stood when its owning process last told (CLITURN1).</summary>
/// <param name="Taking">A turn is on its way to the harness or running there.</param>
/// <param name="Opening">The door is still opening: what waits, waits for it.</param>
/// <param name="Queued">How many words wait behind the turn; the words stay with the owner.</param>
/// <param name="LastTurnEnded">When the owner last saw a turn end; null before any.</param>
public sealed record TurnMark(bool Taking, bool Opening, int Queued, DateTimeOffset? LastTurnEnded);

/// <summary>
/// A conversation's turns, told by the process that runs it to every other Daoris process on the home (CLITURN1):
/// <c>&lt;home&gt;/sessions/requests/&lt;id&gt;.turn</c>, written whole by the owner at each change and removed when it leaves.
/// A terminal's chat owns its runner, and the window's queue reader holds none for it, so without this the window could
/// only say idle for a turn that was running.
/// </summary>
/// <remarks>
/// <para><b>Read-only to everyone but the owner.</b> A mark carries state and no control: a window reading it can show a turn
/// and can send nothing to it. A stop is asked through the request folder (<see cref="SessionRequests"/>), which only the process
/// that runs the session honours.</para>
///
/// <para><b>A mark is believed while its owner lives.</b> It names the owner by process id and start time, as a session's marker
/// does, so a crashed terminal's leftover reads as no mark and is removed by the next look (<see cref="Changed"/>). No mark is
/// not idle: a reader that finds none for a session another process runs knows nothing of its turns.</para>
/// </remarks>
public sealed class TurnMarks(string home)
{
    /// <summary>A mark's ending, beside the requests' <c>.json</c> and the told notes' <c>.told</c>, so neither reads it.</summary>
    public const string Suffix = ".turn";

    public string Folder => new SessionRequests(home).Folder;

    private string PathOf(string session) =>
        SessionEventText.IsLocalId(session)
            ? Path.Combine(Folder, session + Suffix)
            : throw new DriverException($"`{session}` is not a session id of this machine's, so no turn mark can name it.");

    /// <summary>The owner tells where its conversation's turns stand, as this process. False where it could not be written; telling never fails a turn.</summary>
    public bool Write(string session, ChatQueue queue)
    {
        try
        {
            using var self = Process.GetCurrentProcess();
            var owner = (self.Id, self.StartTime.ToUniversalTime().Ticks);
            Directory.CreateDirectory(Folder);
            AtomicFile.WriteText(PathOf(session), ToJson(queue, owner));
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException
                                          or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>The owner has left: its mark goes with it.</summary>
    public void Remove(string session)
    {
        try
        {
            File.Delete(PathOf(session));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left, it names a process that is gone, which the next look removes.
        }
    }

    /// <summary>What the session's owner last told, or null where it told nothing, its mark does not read, or its owner is gone.</summary>
    public TurnMark? Read(string session)
    {
        try
        {
            return Parse(AtomicFile.ReadText(PathOf(session))) is { } read && SessionProcesses.IsRunning(read.Pid, read.Started) ? read.Mark : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Which sessions' marks changed since <paramref name="seen"/> was last given: written, rewritten or gone. A mark whose owner
    /// has gone is removed here and counts as gone. <paramref name="seen"/> is the caller's, kept between looks.
    /// </summary>
    public IReadOnlyList<string> Changed(IDictionary<string, long> seen)
    {
        var changed = new List<string>();
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var path in Directory.Exists(Folder) ? Directory.EnumerateFiles(Folder, "*" + Suffix) : [])
            {
                var session = Path.GetFileNameWithoutExtension(path);
                if (!SessionEventText.IsLocalId(session)) continue;
                if (Read(session) is null)
                {
                    // Told by a process that is gone (or unreadable for good): no mark, and the reader knows nothing again.
                    Remove(session);
                    continue;
                }

                present.Add(session);
                var stamp = File.GetLastWriteTimeUtc(path).Ticks;
                if (!seen.TryGetValue(session, out var before) || before != stamp)
                {
                    seen[session] = stamp;
                    changed.Add(session);
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A folder held for a moment: the next look reads it again.
            return changed;
        }

        foreach (var gone in seen.Keys.Where(session => !present.Contains(session)).ToArray())
        {
            seen.Remove(gone);
            changed.Add(gone);
        }

        return changed;
    }

    private static (TurnMark Mark, int Pid, long Started)? Parse(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("pid", out var pid) || !pid.TryGetInt32(out var id)
                || !root.TryGetProperty("started", out var started) || !started.TryGetInt64(out var ticks)
                || !root.TryGetProperty("taking", out var taking) || taking.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return null;
            }

            var last = root.TryGetProperty("lastTurn", out var moment) && moment.ValueKind == JsonValueKind.String
                ? Moments.Read(moment.GetString())
                : null;
            return (new TurnMark(
                taking.GetBoolean(),
                root.TryGetProperty("opening", out var opening) && opening.ValueKind == JsonValueKind.True,
                root.TryGetProperty("queued", out var queued) && queued.TryGetInt32(out var count) ? count : 0,
                last), id, ticks);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Written by hand, as the driver's other files are, for the AOT reason <see cref="DriverConfig"/> gives.</summary>
    private static string ToJson(ChatQueue queue, (int Pid, long Started) owner) => JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        writer.WriteNumber("pid", owner.Pid);
        writer.WriteNumber("started", owner.Started);
        writer.WriteBoolean("taking", queue.Taking);
        writer.WriteBoolean("opening", queue.Opening);
        writer.WriteNumber("queued", queue.Queued.Count);
        if (queue.LastTurnEnded is { } last) writer.WriteString("lastTurn", Moments.Write(last));
        writer.WriteEndObject();
    });
}
