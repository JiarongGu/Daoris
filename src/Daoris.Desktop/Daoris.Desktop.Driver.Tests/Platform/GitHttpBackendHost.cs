using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A code host on this machine's loopback serving bare repositories over git's smart HTTP, for GITAUTH1b's proof: each request handed to
/// <c>git http-backend</c> from the git that runs the test, as a CGI program, its answer relayed with the connection closed. A raw socket
/// rather than <c>HttpListener</c>, as <see cref="RefusingHttpHost"/> is, which asks Windows for a reservation an ordinary account may
/// not hold. Asks no sign-in and holds no credential of anyone's; pushes are served (<c>http.receivepack</c>), so whatever refuses one is
/// a door.
/// </summary>
internal sealed class GitHttpBackendHost : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _serving;
    private readonly string _backend;
    private readonly string _projects;
    private readonly List<string> _requests = [];

    /// <param name="backend">git's <c>git-http-backend</c> program.</param>
    /// <param name="projects">The folder whose bare repositories are served, by their paths under it.</param>
    public GitHttpBackendHost(string backend, string projects)
    {
        _backend = backend;
        _projects = projects;
        _listener.Start();
        _serving = ServeAsync();
    }

    /// <summary>The address a repository under the served folder is reached at.</summary>
    public string Address(string path) => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/{path}";

    /// <summary>Every request line answered, in order.</summary>
    public IReadOnlyList<string> Requests
    {
        get { lock (_requests) return [.. _requests]; }
    }

    private async Task ServeAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (Exception error) when (error is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }

            _ = Task.Run(() => AnswerAsync(client));
        }
    }

    private async Task AnswerAsync(TcpClient client)
    {
        using var _ = client;
        var stream = client.GetStream();
        try
        {
            var head = ReadHead(stream);
            if (head is null) return;
            var lines = head.Split("\r\n");
            var parts = lines[0].Split(' ');
            lock (_requests) _requests.Add(lines[0]);
            var headers = lines.Skip(1).Where(line => line.Contains(':'))
                .Select(line => (Name: line[..line.IndexOf(':')].Trim(), Value: line[(line.IndexOf(':') + 1)..].Trim()))
                .GroupBy(pair => pair.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);
            var body = ReadBody(stream, headers);
            if (headers.TryGetValue("Content-Encoding", out var encoding) && encoding.Equals("gzip", StringComparison.OrdinalIgnoreCase))
            {
                using var gzip = new GZipStream(new MemoryStream(body), CompressionMode.Decompress);
                using var plain = new MemoryStream();
                gzip.CopyTo(plain);
                body = plain.ToArray();
            }

            var target = parts[1];
            var query = target.IndexOf('?');
            var info = new ProcessStartInfo(_backend)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            info.Environment["GIT_PROJECT_ROOT"] = _projects;
            info.Environment["GIT_HTTP_EXPORT_ALL"] = "1";
            info.Environment["REQUEST_METHOD"] = parts[0];
            info.Environment["PATH_INFO"] = Uri.UnescapeDataString(query < 0 ? target : target[..query]);
            info.Environment["QUERY_STRING"] = query < 0 ? "" : target[(query + 1)..];
            info.Environment["CONTENT_TYPE"] = headers.GetValueOrDefault("Content-Type") ?? "";
            info.Environment["CONTENT_LENGTH"] = body.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
            info.Environment["REMOTE_ADDR"] = "127.0.0.1";
            if (headers.TryGetValue("Git-Protocol", out var protocol)) info.Environment["GIT_PROTOCOL"] = protocol;

            using var backend = Process.Start(info)!;
            var output = new MemoryStream();
            var reading = backend.StandardOutput.BaseStream.CopyToAsync(output);
            var errors = backend.StandardError.ReadToEndAsync();
            await backend.StandardInput.BaseStream.WriteAsync(body);
            backend.StandardInput.Close();
            await reading;
            await errors;
            await backend.WaitForExitAsync();

            var answer = output.ToArray();
            var split = IndexOf(answer, "\r\n\r\n"u8, out var gap);
            var cgi = split < 0 ? "" : Encoding.ASCII.GetString(answer, 0, split);
            var content = split < 0 ? answer : answer[(split + gap)..];
            var status = "200 OK";
            var reply = new StringBuilder();
            foreach (var line in cgi.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0))
            {
                if (line.StartsWith("Status:", StringComparison.OrdinalIgnoreCase)) status = line[7..].Trim();
                else reply.Append(line).Append("\r\n");
            }

            var top = $"HTTP/1.1 {status}\r\n{reply}Content-Length: {content.Length}\r\nConnection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(top));
            await stream.WriteAsync(content);
            await stream.FlushAsync();
        }
        catch (Exception error) when (error is IOException or SocketException or ObjectDisposedException)
        {
            // The client went first; the next request is its own.
        }
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

    private static int IndexOf(byte[] data, ReadOnlySpan<byte> crlf, out int gap)
    {
        var at = data.AsSpan().IndexOf(crlf);
        gap = 4;
        if (at >= 0) return at;
        gap = 2;
        return data.AsSpan().IndexOf("\n\n"u8);
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        try { await _serving; } catch (OperationCanceledException) { }
        _stop.Dispose();
    }
}
