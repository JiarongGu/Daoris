using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A home's identity (HOMEID1, D171, the terminal RPC design §3.1): <c>&lt;home&gt;/home.json</c>,
/// <c>{ "id": "&lt;32 lower-case hex&gt;", "minted": "&lt;ISO 8601&gt;" }</c>. One folder has many paths (a junction, a mapped
/// or <c>subst</c> drive, another case, a short name), and every one of them reads this file alike, so a running service, an
/// address file and the home's lease are told apart by it and never by a path.
/// </summary>
/// <remarks>
/// <para><b>Minted once</b>, by the first Daoris process that needs it (the desktop at its start, a host, a verb), and written
/// as a new file only: written beside, then moved into place with no overwrite, so of two minting at once one file stands and
/// both read it. It is never changed in place.</para>
///
/// <para><b>A file that does not read is said and never replaced</b>: the person's home is not guessed at, so such a home has no
/// id, and nothing that checks one takes it.</para>
///
/// <para>One of three twins: the service's <c>HomeIdentity</c> and the CLI's <c>homeId</c> (<c>home.ts</c>) hold the same file
/// and share no code (<c>twins.md</c>). It uses nothing but the base library.</para>
/// </remarks>
public static class HomeIdentity
{
    /// <summary>The file's name under the home.</summary>
    public const string FileName = "home.json";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Where a home's identity is.</summary>
    public static string PathIn(string home) => Path.Combine(home, FileName);

    /// <summary>Whether a value is an id as this file holds one: 32 lower-case hex characters.</summary>
    public static bool IsId(string? value) =>
        value is { Length: 32 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    /// <summary>The home's id, or null where it has none yet or its file does not read.</summary>
    public static string? Read(string home) => Read(home, out _);

    /// <summary>
    /// The home's id, or null: with <paramref name="unread"/> null where there is no file, and the sentence that says so where
    /// there is one that does not read.
    /// </summary>
    public static string? Read(string home, out string? unread)
    {
        unread = null;
        var path = PathIn(home);
        string? text = null;
        for (var attempt = 0; text is null; attempt++)
        {
            try
            {
                text = File.ReadAllText(path, Utf8);
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            {
                return null;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Held this moment (a file just moved into place is opened by others, a scanner among them): look again, a
                // few times, before it is said. Seen once under a full suite's load, where one of eight minting read none.
                if (attempt < 8)
                {
                    Thread.Sleep(10 + attempt * 10);
                    continue;
                }

                unread = $"{path} could not be read ({error.Message.TrimEnd('.')}), so this home has no id this time.";
                return null;
            }
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
                && IsId(id.GetString()))
            {
                return id.GetString();
            }
        }
        catch (JsonException)
        {
            // Said below, as any other file that does not read.
        }

        unread = $"{path} does not read as a Daoris home's id, and is left as it is: move it aside for a new one to be minted.";
        return null;
    }

    /// <summary>The home's id, minted where it has none; null where its file does not read, which is never replaced.</summary>
    public static string? Ensure(string home) => Ensure(home, out _);

    /// <summary>
    /// The home's id, minted where it has none; null, with the sentence in <paramref name="unread"/>, where its file does not
    /// read or none could be written.
    /// </summary>
    public static string? Ensure(string home, out string? unread)
    {
        if (Read(home, out unread) is { } id) return id;
        if (unread is not null) return null;

        var path = PathIn(home);
        var beside = $"{path}.{Guid.NewGuid():N}.writing";
        try
        {
            Directory.CreateDirectory(home);
            File.WriteAllText(beside, Minted(), Utf8);
            File.Move(beside, path, overwrite: false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Almost always: another process minted it first. Its file is the home's id, read below like any other.
            if (!File.Exists(path))
            {
                unread = $"{path} could not be written ({error.Message.TrimEnd('.')}), so this home has no id this time.";
                return null;
            }
        }
        finally
        {
            if (File.Exists(beside))
            {
                try { File.Delete(beside); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        return Read(home, out unread);
    }

    /// <summary>A new id's file: 128 random bits and when they were drawn.</summary>
    private static string Minted()
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("id", Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant());
            writer.WriteString("minted", DateTimeOffset.UtcNow);
            writer.WriteEndObject();
        }

        return Utf8.GetString(buffer.ToArray()).Replace("\r\n", "\n") + "\n";
    }
}
