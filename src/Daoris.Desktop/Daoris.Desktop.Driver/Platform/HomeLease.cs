using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>Who holds a home: an instance for its life, or a verb, a migration or a swap for its act.</summary>
public enum LeaseKind
{
    /// <summary>The desktop application's loop: an instance.</summary>
    Desktop,

    /// <summary>A terminal's <c>daoris drive</c>: an instance.</summary>
    Headless,

    /// <summary>A terminal's verb acting alone, for its mutation.</summary>
    Verb,

    /// <summary>The desktop's first-start move of an older home's state into the install's.</summary>
    Migration,

    /// <summary>The launcher's swap of <c>app/</c> for a staged build.</summary>
    Swap,
}

/// <summary>The live holder a home's lease names, said in a refusal so the person knows which one to wait for or stop.</summary>
/// <param name="Kind">What holds it.</param>
/// <param name="Pid">Its process.</param>
/// <param name="Since">When it took the home.</param>
/// <param name="What">A verb's own words (its verb, never its arguments), or null.</param>
public sealed record LeaseHolder(LeaseKind Kind, int Pid, DateTimeOffset Since, string? What = null)
{
    /// <summary>Whether the holder is an instance, which holds the home for its life rather than for one act.</summary>
    public bool IsInstance => Kind is LeaseKind.Desktop or LeaseKind.Headless;

    /// <summary>The holder as a sentence names it: what it is, its process, and since when.</summary>
    public string Named =>
        Kind switch
        {
            LeaseKind.Desktop => "the desktop",
            LeaseKind.Headless => "a headless Daoris",
            LeaseKind.Migration => "the desktop's first-start move of an older home",
            LeaseKind.Swap => "the launcher's update of the application",
            _ => What is { Length: > 0 } words ? $"a terminal's `{words}`" : "a terminal's verb",
        }
        + $" (pid {Pid}, since {Since.ToLocalTime():yyyy-MM-dd HH:mm:ss})";
}

/// <summary>A home's lease that changed under every attempt, or could not be written: a tool error, said in a sentence.</summary>
public sealed class HomeLeaseException(string message, Exception? inner = null) : IOException(message, inner);

/// <summary>
/// One writer per home: <c>&lt;home&gt;/home.lease</c>, the driver's lock
/// (<c>driver.lock</c>) grown to cover every writer rather than a second file beside it.
/// </summary>
/// <remarks>
/// <para><b>Who holds it.</b> An instance (the desktop's loop, a headless <c>drive</c>) for its life; a verb acting alone for its
/// mutation only, never for a read; the desktop's first-start migration; and the launcher's swap of <c>app/</c>,
/// so no verb the install carries runs alone from a folder being replaced. A verb that finds an instance's lease
/// acts beside it (<see cref="Beside"/>), holding nothing, until a pipe makes it the running instance's client.</para>
///
/// <para><b>Taken without replacing.</b> Written beside, then moved into place with no overwrite, so of two takers at once only
/// one takes it. Released only while it still says what its holder wrote. It names its holder's kind, process and start, a
/// verb's words, and the home's id (<see cref="HomeIdentity"/>), so a refusal says who holds it.</para>
///
/// <para><b>A stale lease never blocks.</b> One whose process is gone, whose process id now belongs to a process started at
/// another time, or that does not read, is replaced. A process whose start time cannot be read is taken as alive: "cannot
/// tell" must never become "take it".</para>
///
/// <para><b>Every path to the folder takes the same lease</b>: it is a file in the folder, so a junction, a mapped drive or
/// another case of the path meets the one file.</para>
///
/// <para><b>An older build's <c>driver.lock</c></b> is read as that build's lease while its holder lives, so a desktop or a loop
/// of that build still holds the home against this one; a stale one is left where it is and ignored. So is a lease at
/// an older place of the <c>home-lease</c> entry, outside <c>run/</c>.</para>
///
/// <para><b>Compiled into the launcher too</b>, with <see cref="HomeIdentity"/> and <see cref="DaorisHome"/>, whose table names
/// the lease's place (<c>Daoris.Desktop.Launcher.csproj</c>), so it uses nothing but the base library. Every verb that holds the home holds it through here.</para>
/// </remarks>
public sealed class HomeLease : IDisposable
{
    /// <summary>The lease's name under the home.</summary>
    public const string FileName = "home.lease";

    /// <summary>The older build's lock: read as that build's lease while its holder lives.</summary>
    public const string LegacyFileName = "driver.lock";

    /// <summary>How long a verb, a migration or a swap waits on another act's lease, and an instance before it says so.</summary>
    public static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    /// <summary>How often a wait looks again.</summary>
    public static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(250);

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _path;

    // What this lease wrote, or null when it runs beside an instance and holds nothing.
    private readonly string? _written;
    private int _released;

    private HomeLease(string path, string? written, LeaseHolder? beside)
    {
        _path = path;
        _written = written;
        Beside = beside;
    }

    /// <summary>Whether this process holds its home's lease.</summary>
    public bool Holds => _written is not null;

    /// <summary>The live instance this one runs beside, holding nothing, or null when it holds the home.</summary>
    public LeaseHolder? Beside { get; }

    /// <summary>
    /// Where this build takes a home's lease: the home's <c>home-lease</c> entry, <c>run/home.lease</c>,
    /// whichever spelling of the home is asked.
    /// </summary>
    public static string PathIn(string home) => DaorisHome.Places(home, HomeEntry.HomeLease)[0];

    /// <summary>
    /// Every other place a lease of this home may be: the <c>home-lease</c> entry's old place, where a build from
    /// before the switch takes it, and an older build's <c>driver-lock</c> at either of its places. A live holder at any of them
    /// holds the home; a stale one is left where it is.
    /// </summary>
    private static IEnumerable<string> OlderIn(string home) =>
        DaorisHome.Places(home, HomeEntry.HomeLease).Skip(1).Concat(DaorisHome.Places(home, HomeEntry.DriverLock));

    /// <summary>Take the home, or null with the live holder that already has it.</summary>
    /// <param name="what">A verb's own words, which a refusal names; never its arguments.</param>
    /// <exception cref="HomeLeaseException">The lease changed under every attempt, or could not be written.</exception>
    public static HomeLease? TryAcquire(string home, LeaseKind kind, out LeaseHolder? holder, string? what = null)
    {
        Directory.CreateDirectory(home);
        foreach (var older in OlderIn(home))
        {
            if (Live(older) is { } elsewhere)
            {
                holder = elsewhere;
                return null;
            }
        }

        var path = PathIn(home);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Exception? last = null;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var mine = Describe(kind, what, home);
            if (TryCreate(path, mine, ref last))
            {
                holder = null;
                return new HomeLease(path, mine, beside: null);
            }

            var seen = Read(path, out var busy);
            if (seen is null)
            {
                // Gone between the create and the read, or busy for a moment: look again.
                if (busy) Thread.Sleep(10 + attempt * 10);
                continue;
            }

            if (Parse(seen) is { } named && ProcessLiveness.Alive(named.Holder.Pid, named.Started))
            {
                holder = named.Holder;
                return null;
            }

            // Stale. Removed only while it still says what is judged stale, so a lease another has just taken is not the one
            // removed.
            if (Read(path, out _) == seen) TryDelete(path);
        }

        throw new HomeLeaseException(
            $"the home's lease ({path}) could not be taken: it changed under every attempt, or would not be written"
            + $"{(last is null ? "" : $" ({last.Message})")}. Try again.", last);
    }

    /// <summary>
    /// Take the home where it is free, or run beside the live instance holding it (<c>--share</c>): the rare deliberate case,
    /// which never takes another's lease. Null with the holder where an act (a verb, a migration, a swap) holds it.
    /// </summary>
    public static HomeLease? Share(string home, LeaseKind kind, out LeaseHolder? holder)
    {
        var lease = TryAcquire(home, kind, out holder);
        if (lease is not null || holder is not { IsInstance: true } instance) return lease;
        holder = null;
        return new HomeLease(PathIn(home), written: null, beside: instance);
    }

    /// <summary>The live holder of the home, or null when none holds it.</summary>
    public static LeaseHolder? HeldBy(string home) =>
        Live(PathIn(home)) ?? OlderIn(home).Select(Live).FirstOrDefault(holder => holder is not null);

    /// <summary>
    /// The live holder the home's lease names, or null; <paramref name="doubt"/> where the lease could not be read this moment or names
    /// what this build cannot read (a newer build's), which a reader that must never wait on a doubt takes as held (the events relay's bound).
    /// </summary>
    public static LeaseHolder? HeldBy(string home, out bool doubt)
    {
        var text = Read(PathIn(home), out var busy);
        var named = text is null ? null : Parse(text);
        doubt = busy || (text is not null && named is null);
        return named is { } live && ProcessLiveness.Alive(live.Holder.Pid, live.Started) ? live.Holder : null;
    }

    /// <summary>
    /// Whether this process holds the home's lease: its instance's loop, or a verb acting alone under the lease
    /// it took. A verb beside an instance holds nothing, so what only the lease's holder writes, such as an end, it never writes. Read
    /// from the lease file, so any path to the home meets the one answer.
    /// </summary>
    public static bool HeldHere(string home) => HeldBy(home) is { } holder && holder.Pid == Environment.ProcessId;

    /// <summary><see cref="HeldHere"/>, or null where the lease file could not be read this moment (another process's lock on it).</summary>
    public static bool? HeldHereRead(string home)
    {
        Read(PathIn(home), out var busy);
        return busy ? null : HeldHere(home);
    }

    /// <summary>What a terminal's loop says when an instance holds the home.</summary>
    public static string Refusal(string home, LeaseHolder holder) =>
        $"{home} is already driven by {holder.Named}. A second loop on one home races the first for the "
        + "same quests — stop that one first, or pass --share to run beside it on purpose.";

    /// <summary>What a verb, a loop or a swap says when another act held the home for the whole of its wait.</summary>
    public static string WaitRefusal(string home, LeaseHolder holder) =>
        $"{home} is held by {holder.Named}, and was for the {Wait.TotalSeconds:0} seconds this waited: nothing was done. "
        + "Run this again once that one ends.";

    /// <summary>
    /// The lease for a verb acting alone: taken where the home is free; beside an instance,
    /// holding nothing, until a pipe makes it that instance's client; and where another verb, a
    /// migration or a swap holds it, waited for up to <paramref name="within"/>, then null with the refusal naming which.
    /// </summary>
    public static HomeLease? ForVerb(
        string home, string what, out string? refusal,
        TimeSpan? within = null, Action<TimeSpan>? sleep = null, Func<DateTimeOffset>? clock = null)
    {
        var lease = Await(home, LeaseKind.Verb, what, within ?? Wait, out var holder, besideInstance: true, sleep, clock);
        refusal = lease is null ? WaitRefusal(home, holder!) : null;
        return lease;
    }

    /// <summary>
    /// The lease a long verb publishes under: a download's end, and in its rows a
    /// sign-in's, a key's and a pin's. Waited for and never refused, as the desktop's loop waits for an act: what the person waited
    /// for is never thrown away for another act's hold. Taken the moment that hold lets go; beside an instance (the desktop started
    /// meanwhile), run holding nothing, each file's keep lock the publication's protection.
    /// </summary>
    /// <param name="what">The verb's own words, which another's refusal names.</param>
    /// <param name="told">Said once the hold has lasted <paramref name="tellAfter"/>: who holds the home.</param>
    /// <param name="ct">The person's Ctrl+C, which ends the wait; nothing is taken.</param>
    /// <param name="waiting">Called once, the first time another act is found holding the home: a test's sign that this waits.</param>
    /// <param name="clock">The moment, read for <paramref name="tellAfter"/>: the machine's, or a test's.</param>
    /// <exception cref="OperationCanceledException">The wait is stopped.</exception>
    public static async Task<HomeLease> ForPublicationAsync(
        string home, string what, Action<LeaseHolder> told, CancellationToken ct, Action? waiting = null, Func<DateTimeOffset>? clock = null,
        TimeSpan? tellAfter = null, TimeSpan? poll = null)
    {
        clock ??= () => DateTimeOffset.UtcNow;
        DateTimeOffset? since = null;
        var said = false;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (TryAcquire(home, LeaseKind.Verb, out var holder, what) is { } lease) return lease;
            if (holder is { IsInstance: true } instance) return new HomeLease(PathIn(home), written: null, beside: instance);

            if (since is null)
            {
                since = clock();
                waiting?.Invoke();
            }

            if (!said && holder is not null && clock() - since >= (tellAfter ?? Wait))
            {
                told(holder);
                said = true;
            }

            await Task.Delay(poll ?? Poll, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Take the home for <paramref name="kind"/>, waiting up to <paramref name="within"/> while another holds it; null with the
    /// holder once the wait is over. With <paramref name="besideInstance"/>, an instance's lease is not waited for: this one
    /// runs beside it, holding nothing.
    /// </summary>
    public static HomeLease? Await(
        string home, LeaseKind kind, string? what, TimeSpan within, out LeaseHolder? holder,
        bool besideInstance = false, Action<TimeSpan>? sleep = null, Func<DateTimeOffset>? clock = null)
    {
        sleep ??= Thread.Sleep;
        clock ??= () => DateTimeOffset.UtcNow;
        var until = clock() + within;
        while (true)
        {
            if (TryAcquire(home, kind, out holder, what) is { } lease) return lease;
            if (besideInstance && holder is { IsInstance: true } instance)
            {
                holder = null;
                return new HomeLease(PathIn(home), written: null, beside: instance);
            }

            if (clock() >= until) return null;
            sleep(Poll);
        }
    }

    /// <summary>Let the home go — only while the lease still says what this one wrote.</summary>
    public void Dispose()
    {
        if (_written is null || Interlocked.Exchange(ref _released, 1) == 1) return;
        if (Read(_path, out _) == _written) TryDelete(_path);
    }

    /// <summary>The live holder a file names, or null.</summary>
    private static LeaseHolder? Live(string path) =>
        Read(path, out _) is { } seen && Parse(seen) is { } named && ProcessLiveness.Alive(named.Holder.Pid, named.Started) ? named.Holder : null;

    /// <summary>This process, as its lease names it, with the home's id, minted where it has none.</summary>
    private static string Describe(LeaseKind kind, string? what, string home)
    {
        using var self = Process.GetCurrentProcess();
        var id = HomeIdentity.Ensure(home);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("kind", Spelled(kind));
            writer.WriteNumber("pid", Environment.ProcessId);
            writer.WriteNumber("started", self.StartTime.ToUniversalTime().Ticks);
            writer.WriteString("since", DateTimeOffset.UtcNow);
            if (what is { Length: > 0 }) writer.WriteString("what", what);
            if (id is not null) writer.WriteString("home", id);
            writer.WriteEndObject();
        }

        return Utf8.GetString(buffer.ToArray()).Replace("\r\n", "\n") + "\n";
    }

    private static string Spelled(LeaseKind kind) => kind switch
    {
        LeaseKind.Desktop => "desktop",
        LeaseKind.Headless => "headless",
        LeaseKind.Verb => "verb",
        LeaseKind.Migration => "migration",
        _ => "swap",
    };

    /// <summary>Written beside, then moved into place only where nothing is — never over another lease.</summary>
    private static bool TryCreate(string path, string text, ref Exception? last)
    {
        var beside = $"{path}.{Guid.NewGuid():N}.writing";
        try
        {
            File.WriteAllText(beside, text, Utf8);
            File.Move(beside, path, overwrite: false);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Almost always: a lease is already there. Otherwise the loop's read says what is.
            last = error;
            return false;
        }
        finally
        {
            if (File.Exists(beside))
            {
                try { File.Delete(beside); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }

    /// <summary>The lease's text, or null when there is none — or none readable this moment (<paramref name="busy"/>).</summary>
    private static string? Read(string path, out bool busy)
    {
        busy = false;
        try
        {
            return BesideReads.Text(path);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            busy = true;
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The holder a lease names and its process's start time, or null for one that does not read.</summary>
    private static (LeaseHolder Holder, long Started)? Parse(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            LeaseKind? kind = root.TryGetProperty("kind", out var named) && named.ValueKind == JsonValueKind.String
                ? named.GetString() switch
                {
                    "desktop" => LeaseKind.Desktop,
                    "headless" => LeaseKind.Headless,
                    "verb" => LeaseKind.Verb,
                    "migration" => LeaseKind.Migration,
                    "swap" => LeaseKind.Swap,
                    _ => null,
                }
                : null;
            if (kind is null
                || !root.TryGetProperty("pid", out var pid) || !pid.TryGetInt32(out var id)
                || !root.TryGetProperty("started", out var started) || !started.TryGetInt64(out var ticks))
            {
                return null;
            }

            var since = root.TryGetProperty("since", out var at) && at.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(at.GetString(), System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var moment)
                    ? moment
                    : new DateTimeOffset(ticks, TimeSpan.Zero);
            var what = root.TryGetProperty("what", out var words) && words.ValueKind == JsonValueKind.String ? words.GetString() : null;
            return (new LeaseHolder(kind.Value, id, since, what), ticks);
        }
        catch (Exception error) when (error is JsonException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
