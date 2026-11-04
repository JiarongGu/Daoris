using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>The verification run holding the home's verification slot: its process, its run and attempt, its repository, since when.</summary>
public sealed record VerifyHolder(int Pid, long Started, string Run, string Invocation, string Repository, DateTimeOffset Since);

/// <summary>An attempt the executor was sent and has not started, in the order sent, and what it waits for now.</summary>
public sealed record VerifyWaiter(int Pid, long Started, string Run, string Invocation, string Repository, DateTimeOffset Since, int Order)
{
    /// <summary>What it waits for: the run holding a slot, or the runner's own run; null before its first look.</summary>
    public string? For { get; init; }
}

/// <summary>The home's verification slot as it stands: its holder and the attempts waiting, each only while its process runs.</summary>
public sealed record VerifyLook(VerifyHolder? Holder, IReadOnlyList<VerifyWaiter> Waiters);

/// <summary>
/// The home's one verification slot (the pipeline design §4.2): one verification run in the home at a time, its handle the lock as a
/// repository's heavy-run slot's is, its holder and its waiters said beside it under <c>locks/verify/</c>.
/// </summary>
public sealed class VerifySlot : IDisposable
{
    private readonly FileStream _file;
    private readonly string _record;

    private VerifySlot(FileStream file, string record) => (_file, _record) = (file, record);

    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.Locks, "verify", "home.lock");

    public static string RecordOf(string home) => DaorisHome.Entry(home, HomeEntry.Locks, "verify", "home.json");

    public static string WaitersOf(string home) => DaorisHome.Entry(home, HomeEntry.Locks, "verify", "waits");

    /// <summary>The slot, or null while another run holds it. Never waits.</summary>
    public static VerifySlot? TryTake(string home)
    {
        var path = PathOf(home);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            return new VerifySlot(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None), RecordOf(home));
        }
        catch (IOException held) when (!OperatingSystem.IsWindows() || (held.HResult & 0xFFFF) is 32 or 33)
        {
            return null;
        }
    }

    public void Keep(VerifyHolder holder) => AtomicFile.WriteText(_record, Json(writer =>
    {
        writer.WriteNumber("pid", holder.Pid);
        writer.WriteNumber("started", holder.Started);
        writer.WriteString("run", holder.Run);
        writer.WriteString("invocation", holder.Invocation);
        writer.WriteString("repository", holder.Repository);
        writer.WriteString("since", holder.Since.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    }));

    /// <summary>Let go: the record first, then the slot.</summary>
    public void Dispose()
    {
        try
        {
            File.Delete(_record);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left: its process is gone soon, and a holder whose process is gone holds nothing.
        }

        _file.Dispose();
    }

    /// <summary>The slot's holder and its waiters, the first sent first, each only where its process runs. Reads files only.</summary>
    public static VerifyLook Look(string home, Func<int, long, bool>? alive = null)
    {
        alive ??= HeavySlot.Running;
        VerifyHolder? holder = null;
        if (Read(RecordOf(home)) is { } held && Text(held, "run") is { } run && Text(held, "invocation") is { } invocation
            && held.TryGetProperty("pid", out var pid) && pid.TryGetInt32(out var id) && held.TryGetProperty("started", out var started) && started.TryGetInt64(out var ticks)
            && held.TryGetProperty("since", out var since) && since.TryGetDateTimeOffset(out var at) && alive(id, ticks))
        {
            holder = new VerifyHolder(id, ticks, run, invocation, Text(held, "repository") ?? "", at);
        }

        var waiters = new List<VerifyWaiter>();
        var folder = WaitersOf(home);
        foreach (var file in Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*.json").ToList() : [])
        {
            if (Read(file) is { } one && Waiter(one) is { } waiter && alive(waiter.Pid, waiter.Started)) waiters.Add(waiter);
        }

        return new VerifyLook(holder, [.. waiters.OrderBy(waiter => waiter.Since).ThenBy(waiter => waiter.Order)]);
    }

    /// <summary>Say an attempt waits, in a file of its own named by its invocation; one that cannot be written costs the page a waiter.</summary>
    internal static void Wait(string home, VerifyWaiter waiter)
    {
        try
        {
            Directory.CreateDirectory(WaitersOf(home));
            AtomicFile.WriteText(Path.Combine(WaitersOf(home), waiter.Invocation + ".json"), Json(writer =>
            {
                writer.WriteNumber("pid", waiter.Pid);
                writer.WriteNumber("started", waiter.Started);
                writer.WriteString("run", waiter.Run);
                writer.WriteString("invocation", waiter.Invocation);
                writer.WriteString("repository", waiter.Repository);
                writer.WriteString("since", waiter.Since.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                writer.WriteNumber("order", waiter.Order);
                if (waiter.For is { } what) writer.WriteString("for", what);
            }));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }

    internal static void Forget(string home, string invocation)
    {
        try
        {
            File.Delete(Path.Combine(WaitersOf(home), invocation + ".json"));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static VerifyWaiter? Waiter(JsonElement root) =>
        root.TryGetProperty("pid", out var pid) && pid.TryGetInt32(out var id) && root.TryGetProperty("started", out var started) && started.TryGetInt64(out var ticks)
        && Text(root, "run") is { } run && Text(root, "invocation") is { } invocation
        && root.TryGetProperty("since", out var since) && since.TryGetDateTimeOffset(out var at)
            ? new VerifyWaiter(id, ticks, run, invocation, Text(root, "repository") ?? "", at,
                root.TryGetProperty("order", out var order) && order.TryGetInt32(out var number) ? number : 0) { For = Text(root, "for") }
            : null;

    private static JsonElement? Read(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream);
            return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string Json(Action<Utf8JsonWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray()).ReplaceLineEndings("\n") + "\n";
    }
}
