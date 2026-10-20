using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What a home's address file names: the service, and the process that holds or adopted it.</summary>
/// <param name="Url">The service's address, without a trailing slash.</param>
/// <param name="Pid">The desktop's process.</param>
/// <param name="Started">That process's start time, in UTC ticks, so a process id used again is not taken for it.</param>
/// <param name="Home">The home's id (HOMEID1, <see cref="HomeIdentity"/>), or null in a file written before it or where the home has none.</param>
/// <param name="Pipe">
/// The pipe the instance answers on (RPC1a, <see cref="PipeName"/>), or null before it serves one, in a file from before, or where
/// the name is not the pipe of the home the file names.
/// </param>
public sealed record RunningEntry(string Url, int Pid, long Started, string? Home = null, string? Pipe = null);

/// <summary>
/// The address the running desktop leaves in its home (FINDDAORIS1, D171 point 1): a terminal outside Daoris's own panel is
/// handed no variable, so a program the install carries reads where the service is from here (<see cref="RunningService"/>).
/// </summary>
/// <remarks>
/// <para><b>Written whole, once the service answers</b>, held or adopted, naming the address and the process, as the driver
/// lease names its holder (<see cref="HomeLease"/>): a reader never meets half of it, and a process id used again by another
/// program is told apart by its start time.</para>
///
/// <para><b>Removed by its writer at a clean exit, and only while it still says what that writer wrote</b>, so a second Daoris
/// on the same home that wrote since keeps its own. One that ended without its exit leaves it, and a reader judges it stale.</para>
///
/// <para>No key, ever: a file under the home is no place for a credential, and a local host takes none (D21).</para>
/// </remarks>
public sealed class RunningAddress : IDisposable
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _path;
    private readonly string _written;
    private int _removed;

    private RunningAddress(string path, string written)
    {
        _path = path;
        _written = written;
    }

    /// <summary>Where a home's address file is.</summary>
    public static string PathIn(string home) => DaorisHome.Entry(home, HomeEntry.Running);

    /// <summary>
    /// Leave this process's address in <paramref name="home"/>, or null when it could not be written: the window runs either
    /// way, and a terminal then says Daoris is not running rather than this start failing.
    /// </summary>
    /// <param name="pipe">
    /// The pipe this instance serves (RPC1a, the terminal RPC design §6.3), written only once it is served, so a client that
    /// reads one finds it served; null names none.
    /// </param>
    public static RunningAddress? Write(string home, string serviceUrl, string? pipe = null)
    {
        try
        {
            using var self = Process.GetCurrentProcess();
            // HOMEID1: the home's id beside the address, minted where it has none (fields added, never changed in place).
            var id = HomeIdentity.Ensure(home);
            var text = JsonFile.Serialize(writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("url", serviceUrl.Trim().TrimEnd('/'));
                writer.WriteNumber("pid", Environment.ProcessId);
                writer.WriteNumber("started", self.StartTime.ToUniversalTime().Ticks);
                writer.WriteString("since", DateTimeOffset.UtcNow);
                if (id is not null) writer.WriteString("home", id);
                if (pipe is not null) writer.WriteString("pipe", pipe);
                writer.WriteEndObject();
            });
            Directory.CreateDirectory(home);
            var path = PathIn(home);
            // The folder the address is in (`run/` since HOMELAYOUT3), which a new home has not made yet.
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            AtomicFile.WriteText(path, text);
            return new RunningAddress(path, text);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException
                                          or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    /// <summary>What <paramref name="home"/>'s address file names, or null when there is none or it does not read.</summary>
    public static RunningEntry? Read(string home)
    {
        string text;
        try
        {
            text = File.ReadAllText(PathIn(home), Utf8);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("url", out var url) || url.ValueKind != JsonValueKind.String
                || url.GetString() is not { Length: > 0 } address
                || !root.TryGetProperty("pid", out var pid) || !pid.TryGetInt32(out var id)
                || !root.TryGetProperty("started", out var started) || !started.TryGetInt64(out var ticks))
            {
                return null;
            }

            var homeId = Text(root, "home") is { } named && HomeIdentity.IsId(named) ? named : null;
            // RPC1a: a pipe is read only where it is the pipe of the home this file names (the CLI's `isPipeOf` holds the same rows).
            var pipe = Text(root, "pipe") is { } served && PipeName.IsOf(served, homeId) ? served : null;
            return new RunningEntry(address, id, ticks, homeId, pipe);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The string <paramref name="root"/> holds in <paramref name="field"/>, or null where it holds none or another kind.</summary>
    private static string? Text(JsonElement root, string field) =>
        root.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Take the address away — only while the file still says what this one wrote.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _removed, 1) == 1) return;
        try
        {
            if (File.ReadAllText(_path, Utf8) == _written) File.Delete(_path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Gone already, or held this moment: a reader judges what is left by its process.
        }
    }
}
