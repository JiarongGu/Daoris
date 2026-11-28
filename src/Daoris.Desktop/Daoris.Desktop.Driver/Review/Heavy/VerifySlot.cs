using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>The verification run holding the home's verification slot: its process, its run and attempt, its repository, since when.</summary>
public sealed record VerifyHolder(int Pid, long Started, string Run, string Invocation, string Repository, DateTimeOffset Since);

/// <summary>An attempt sent to the executor that has not started, what it waits for now, and its run's class and first arrival, which order it.</summary>
public sealed record VerifyWaiter(int Pid, long Started, string Run, string Invocation, string Repository, DateTimeOffset Since, int Order)
{
    /// <summary>What it waits for, in words, and by code (<see cref="CheckWait"/>) where the admission said; null before its first look.</summary>
    public string? For { get; init; }

    public CheckWait? Wait { get; init; }

    public bool Held { get; init; }

    public string Class { get; init; } = PriorityClasses.Normal;

    public string Source { get; init; } = PriorityReading.Unset;

    public string? By { get; init; }

    public string? Why { get; init; }

    public DateTimeOffset? Arrived { get; init; }

    public DateTimeOffset FirstArrival => Arrived ?? Since;
}

/// <summary>The home's verification slots as they stand: their holders and the attempts waiting, each only while its process runs.</summary>
public sealed record VerifyLook(IReadOnlyList<VerifyHolder> Holders, IReadOnlyList<VerifyWaiter> Waiters);

/// <summary>
/// One of the home's verification slots, the machine's check slots, as <see cref="CheckAdmission"/> admits them: each handle a lock,
/// its holder and the waiters said beside them under <c>locks/verify/</c>, the first slot <c>home.lock</c>, the n-th <c>home-n.lock</c>.
/// </summary>
public sealed class VerifySlot : IDisposable
{
    private readonly FileStream _file;
    private readonly string _record;

    private VerifySlot(FileStream file, string record) => (_file, _record) = (file, record);

    /// <summary>The lock of the slot at <paramref name="index"/>, the first at 0, and its holder's record.</summary>
    public static string PathOf(string home, int index = 0) => DaorisHome.Entry(home, HomeEntry.Locks, "verify", Name(index) + ".lock");

    public static string RecordOf(string home, int index = 0) => DaorisHome.Entry(home, HomeEntry.Locks, "verify", Name(index) + ".json");

    public static string WaitersOf(string home) => DaorisHome.Entry(home, HomeEntry.Locks, "verify", "waits");

    private static string Name(int index) => index == 0 ? "home" : $"home-{index + 1}";

    /// <summary>The first of the first <paramref name="slots"/> slots that is free, or null while runs hold every one. Never waits.</summary>
    public static VerifySlot? TryTake(string home, int slots = 1)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf(home))!);
        for (var index = 0; index < slots; index++)
        {
            try
            {
                return new VerifySlot(new FileStream(PathOf(home, index), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None), RecordOf(home, index));
            }
            catch (IOException held) when (!OperatingSystem.IsWindows() || (held.HResult & 0xFFFF) is 32 or 33)
            {
            }
        }

        return null;
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

    /// <summary>The slots' holders, the first taken first, and their waiters in the queue's order (<see cref="QueueOrder"/>), each only where its process runs. Reads files only.</summary>
    public static VerifyLook Look(string home, Func<int, long, bool>? alive = null)
    {
        alive ??= HeavySlot.Running;
        var holders = new List<VerifyHolder>();
        var verify = Path.GetDirectoryName(PathOf(home))!;
        foreach (var record in Directory.Exists(verify) ? Directory.EnumerateFiles(verify, "home*.json").ToList() : [])
        {
            if (Read(record) is { } held && Text(held, "run") is { } run && Text(held, "invocation") is { } invocation
                && held.TryGetProperty("pid", out var pid) && pid.TryGetInt32(out var id) && held.TryGetProperty("started", out var started) && started.TryGetInt64(out var ticks)
                && held.TryGetProperty("since", out var since) && since.TryGetDateTimeOffset(out var at) && alive(id, ticks))
            {
                holders.Add(new VerifyHolder(id, ticks, run, invocation, Text(held, "repository") ?? "", at));
            }
        }

        var waiters = new List<VerifyWaiter>();
        var folder = WaitersOf(home);
        foreach (var file in Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*.json").ToList() : [])
        {
            if (Read(file) is { } one && Waiter(one) is { } waiter && alive(waiter.Pid, waiter.Started)) waiters.Add(waiter);
        }

        return new VerifyLook(
            [.. holders.OrderBy(holder => holder.Since)],
            [.. QueueOrder.Of(waiters, waiter => waiter.Class, waiter => waiter.FirstArrival).ThenBy(waiter => waiter.Since).ThenBy(waiter => waiter.Order)]);
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
                writer.WriteString("class", waiter.Class);
                writer.WriteString("source", waiter.Source);
                if (waiter.By is { } by) writer.WriteString("by", by);
                if (waiter.Why is { } why) writer.WriteString("why", why);
                if (waiter.Arrived is { } arrived) writer.WriteString("arrived", arrived.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                if (waiter.For is { } what) writer.WriteString("for", what);
                if (waiter.Held) writer.WriteBoolean("held", true);
                if (waiter.Wait is { } wait)
                {
                    writer.WriteString("wait", wait.Code);
                    writer.WriteStartObject("values");
                    foreach (var (name, value) in wait.Values) writer.WriteString(name, value);
                    writer.WriteEndObject();
                }
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
                root.TryGetProperty("order", out var order) && order.TryGetInt32(out var number) ? number : 0)
            {
                For = Text(root, "for"), Wait = WaitOf(root), Held = root.TryGetProperty("held", out var held) && held.ValueKind == JsonValueKind.True,
                Class = PriorityClasses.Parse(Text(root, "class")) ?? PriorityClasses.Normal, Source = Text(root, "source") ?? PriorityReading.Unset,
                By = Text(root, "by"), Why = Text(root, "why"),
                Arrived = root.TryGetProperty("arrived", out var arrived) && arrived.TryGetDateTimeOffset(out var first) ? first : null,
            }
            : null;

    private static CheckWait? WaitOf(JsonElement root) =>
        Text(root, "wait") is { } code && root.TryGetProperty("values", out var values) && values.ValueKind == JsonValueKind.Object
            ? new CheckWait(code, values.EnumerateObject().Where(each => each.Value.ValueKind == JsonValueKind.String)
                .ToDictionary(each => each.Name, each => each.Value.GetString()!, StringComparer.Ordinal))
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
