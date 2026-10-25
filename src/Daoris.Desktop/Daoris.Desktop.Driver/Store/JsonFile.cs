using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The one way the driver writes a JSON file it keeps (DEDUP3a, duplication survey §3): indented, LF on every
/// platform, one final newline, BOM-less UTF-8, written through <see cref="AtomicFile"/> into a folder made on the way.
/// </summary>
/// <remarks>
/// <para><b>Escaping is <c>UnsafeRelaxedJsonEscaping</c>.</b> Chinese text and <c>&lt;</c> <c>&gt;</c> <c>&amp;</c> are kept as
/// written, which is what the CLI's twin writers (<c>JSON.stringify</c>) produce, so a file written by either side is
/// the same bytes. The file is a record kept on disk and read by our own readers, never embedded in HTML, so the
/// default encoder's <c>\uXXXX</c> escapes protect nothing here. Quotes, backslashes and control characters are still escaped.</para>
///
/// <para><b>Why a helper at all.</b> Writers each built their own options: some forgot the line ending (CRLF on
/// Windows, though their comments said LF), some left the default encoder, some wrote plainly past
/// <see cref="AtomicFile"/>. The options below are one object, built once, reflection-free.</para>
/// </remarks>
public static class JsonFile
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>The options every JSON file the driver keeps is written with; one object, shared.</summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The text of a document <paramref name="write"/> composes: indented, LF only, one final newline.</summary>
    public static string Serialize(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions)) write(writer);
        return Utf8.GetString(stream.GetBuffer(), 0, (int)stream.Length) + "\n";
    }

    /// <summary>The text of <paramref name="node"/>: indented, LF only, one final newline.</summary>
    public static string Serialize(JsonNode node) => node.ToJsonString(Options) + "\n";

    /// <summary>Write the document <paramref name="write"/> composes to <paramref name="path"/>, whole or not at all.</summary>
    public static void Write(string path, Action<Utf8JsonWriter> write) => WriteText(path, Serialize(write));

    /// <summary>Write <paramref name="node"/> to <paramref name="path"/>, whole or not at all.</summary>
    public static void Write(string path, JsonNode node) => WriteText(path, Serialize(node));

    private static void WriteText(string path, string text)
    {
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        AtomicFile.WriteText(path, text);
    }
}
