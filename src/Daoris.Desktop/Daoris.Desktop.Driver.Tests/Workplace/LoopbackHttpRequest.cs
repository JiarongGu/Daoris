using System.Text;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// One HTTP/1.1 request as a loopback stand-in host reads it off its socket: the request line, the headers by name, and the body by
/// <c>Content-Length</c> or chunks. Every stand-in answers once and closes, so nothing past the body is read.
/// </summary>
internal sealed record LoopbackHttpRequest(string Method, string Target, IReadOnlyDictionary<string, string> Headers, byte[] Body)
{
    /// <summary>The request line as it was sent (<c>GET /path HTTP/1.1</c>).</summary>
    public string Line => $"{Method} {Target} HTTP/1.1";

    /// <summary>The target's path, unescaped, without its query.</summary>
    public string Path => Uri.UnescapeDataString(Target.Split('?')[0]);

    /// <summary>The target's query, without its <c>?</c>; empty for none.</summary>
    public string Query => Target.IndexOf('?') is var at and >= 0 ? Target[(at + 1)..] : "";

    /// <summary>The request read from <paramref name="stream"/>, or null where the client closed before a head.</summary>
    public static LoopbackHttpRequest? Read(Stream stream)
    {
        var head = ReadHead(stream);
        if (head is null) return null;
        var lines = head.Split("\r\n");
        var parts = lines[0].Split(' ');
        var headers = lines.Skip(1).Where(line => line.Contains(':'))
            .Select(line => (Name: line[..line.IndexOf(':')].Trim(), Value: line[(line.IndexOf(':') + 1)..].Trim()))
            .GroupBy(pair => pair.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);
        return new LoopbackHttpRequest(parts[0], parts.Length > 1 ? parts[1] : "/", headers, ReadBody(stream, headers));
    }

    private static string? ReadHead(Stream stream)
    {
        var head = new MemoryStream();
        while (true)
        {
            var next = stream.ReadByte();
            if (next < 0) return null;
            head.WriteByte((byte)next);
            if (head.Length >= 4 && head.GetBuffer().AsSpan((int)head.Length - 4, 4).SequenceEqual("\r\n\r\n"u8))
                return Encoding.ASCII.GetString(head.GetBuffer(), 0, (int)head.Length - 4);
        }
    }

    private static byte[] ReadBody(Stream stream, Dictionary<string, string> headers)
    {
        if (headers.TryGetValue("Content-Length", out var length) && int.TryParse(length, out var count))
        {
            var body = new byte[count];
            stream.ReadExactly(body);
            return body;
        }

        if (!headers.TryGetValue("Transfer-Encoding", out var transfer) || !transfer.Contains("chunked", StringComparison.OrdinalIgnoreCase))
            return [];

        var all = new MemoryStream();
        while (true)
        {
            var size = Convert.ToInt32(ReadLine(stream).Split(';')[0].Trim(), 16);
            if (size == 0)
            {
                ReadLine(stream);
                return all.ToArray();
            }

            var chunk = new byte[size];
            stream.ReadExactly(chunk);
            all.Write(chunk);
            ReadLine(stream);
        }
    }

    private static string ReadLine(Stream stream)
    {
        var line = new StringBuilder();
        while (true)
        {
            var next = stream.ReadByte();
            if (next < 0 || next == '\n') return line.ToString().TrimEnd('\r');
            line.Append((char)next);
        }
    }
}
