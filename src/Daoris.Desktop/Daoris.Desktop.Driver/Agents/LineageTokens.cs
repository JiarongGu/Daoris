using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;

namespace Daoris.Driver;

/// <summary>A lineage token's reading in the Daoris process that issued it (COWORKVIEW1a).</summary>
public enum TokenReading
{
    /// <summary>Issued, its process not yet handed, or handed and not one this set can watch.</summary>
    Unwatched,

    /// <summary>Its process handed and watched, its exit not yet seen.</summary>
    Watched,

    /// <summary>Its process's exit seen, or no process ever ran under it; a token this set never issued too.</summary>
    Gone,
}

/// <summary>
/// The lineage tokens this Daoris process issued (COWORKVIEW1a, the design §3.6 <i>who holds the token</i>): one set per process,
/// shared as <see cref="AgentJobs.Shared"/> is, so every registry's runs read the same tokens. A token is held from its issue, watched
/// once handed its process, and leaves once that process's exit is seen, by its own handle or by Loop's word, or once no process ran
/// under it. Each move of a reading is told: no line may say it.
/// </summary>
public sealed class LineageTokens
{
    public static LineageTokens Shared { get; } = Own();

    private static readonly AsyncLocal<LineageTokens?> s_within = new();

    /// <summary>The set this flow issues from: a test's where it handed one, else this process's.</summary>
    public static LineageTokens Current => s_within.Value ?? Shared;

    private readonly object _gate = new();
    private readonly Dictionary<string, Process?> _held = new(StringComparer.Ordinal);
    private readonly int _id;
    private readonly long _ticks;

    public LineageTokens(int id, DateTime started)
    {
        _id = id;
        _ticks = started.ToUniversalTime().Ticks;
        Issuer = $"{id} {_ticks}";
    }

    /// <summary>What each <c>start</c> names: this process's id and its start time's ticks, as a session's marker keeps them.</summary>
    public string Issuer { get; }

    /// <summary>Told a token each time its reading moves.</summary>
    public event Action<string>? Moved;

    /// <summary>The test seam: until disposed, this flow issues from <paramref name="tokens"/> instead of this process's set.</summary>
    public static IDisposable Within(LineageTokens tokens)
    {
        var before = s_within.Value;
        s_within.Value = tokens;
        return new Ending(before);
    }

    /// <summary>Whether a <c>start</c>'s issuer names this process: its id, and its start time within a second.</summary>
    public bool Issued(string? issuer)
    {
        var parts = issuer?.Split(' ');
        return parts is [var id, var ticks]
            && int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var pid) && pid == _id
            && long.TryParse(ticks, NumberStyles.None, CultureInfo.InvariantCulture, out var started)
            && Math.Abs(started - _ticks) < TimeSpan.TicksPerSecond;
    }

    /// <summary>A new token, sixteen hex characters, held unwatched.</summary>
    public string Issue()
    {
        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
        lock (_gate) _held[token] = null;
        return token;
    }

    public TokenReading Reading(string token)
    {
        lock (_gate)
        {
            if (!_held.TryGetValue(token, out var watch)) return TokenReading.Gone;
            return watch is null ? TokenReading.Unwatched : TokenReading.Watched;
        }
    }

    /// <summary>
    /// Its process, at the spawn: watched through a handle of the set's own, so a caller disposing its <see cref="Process"/> early
    /// moves nothing. One already exited lets the token go; one the set cannot open, or none, keeps it unwatched until
    /// <see cref="Exited"/>.
    /// </summary>
    public void Hand(string token, Process? process)
    {
        Process? watch = null;
        var exited = false;
        try
        {
            exited = process?.HasExited == true;
            if (process is not null && !exited)
            {
                // While the caller's Process holds its handle, the id cannot name another process.
                watch = Process.GetProcessById(process.Id);
                watch.Exited += (_, _) => Exited(token);
                watch.EnableRaisingEvents = true;
            }
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            watch?.Dispose();
            watch = null;
        }

        lock (_gate)
        {
            if (!_held.ContainsKey(token))
            {
                watch?.Dispose();
                return;
            }

            if (!exited) _held[token] = watch;
        }

        if (exited) Leave(token);
        else Moved?.Invoke(token);
        // An exit between the watch's opening and its events being raised.
        if (watch is not null && HasExited(watch)) Exited(token);
    }

    /// <summary>Its process's exit, which Loop read itself: the token leaves.</summary>
    public void Exited(string token) => Leave(token);

    /// <summary>No process ran under it: its spawn made none, or its run was put back before its spawn.</summary>
    public void Unspawned(string token) => Leave(token);

    private void Leave(string token)
    {
        Process? watch;
        lock (_gate)
        {
            if (!_held.Remove(token, out watch)) return;
        }

        watch?.Dispose();
        Moved?.Invoke(token);
    }

    private static bool HasExited(Process watch)
    {
        try
        {
            return watch.HasExited;
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static LineageTokens Own()
    {
        using var self = Process.GetCurrentProcess();
        return new LineageTokens(Environment.ProcessId, self.StartTime);
    }

    private sealed class Ending(LineageTokens? before) : IDisposable
    {
        public void Dispose() => s_within.Value = before;
    }
}
