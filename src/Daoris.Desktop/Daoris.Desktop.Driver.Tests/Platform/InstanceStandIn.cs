using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>What the stand-in does with a press.</summary>
public enum StandInPress
{
    /// <summary>Answers it with <see cref="InstanceStandIn.Data"/>, after telling <see cref="InstanceStandIn.Progress"/> where set.</summary>
    Answer,

    /// <summary>Refuses it with <see cref="InstanceStandIn.Code"/>, as the pipe's own refusals are answered.</summary>
    Refuse,

    /// <summary>Closes the connection without answering: the terminal's answer is lost, and <c>RESULT</c> says how it ended.</summary>
    Drop,

    /// <summary>Never answers on that connection.</summary>
    Hold,
}

/// <summary>
/// A stand-in for the running Daoris's pipe (RPC1b): the wire of the terminal RPC design §6 as RPC1a's <c>InstancePipe</c> speaks
/// it, a hello, a press and <c>RESULT</c>, each answered as a test sets it, with every line it was sent kept. In process, on a
/// name of the test home's own.
/// </summary>
internal sealed class InstanceStandIn : IAsyncDisposable
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private readonly CancellationTokenSource _stop = new();
    private readonly List<JsonElement> _sent = [];
    private readonly List<NamedPipeServerStream> _open = [];
    private readonly Task _serving;
    private int _connections;

    /// <param name="name">The pipe's name.</param>
    /// <param name="homeId">The home it answers as.</param>
    /// <param name="refusing">
    /// Served with an ACL that refuses this user (PIPEOWNER1), as another account's pipe refuses a terminal: the connect is
    /// refused before any line crosses. Made with the first instance, so a constructor's, not a setting's.
    /// </param>
    public InstanceStandIn(string name, string homeId, bool refusing = false)
    {
        Name = name;
        HomeId = homeId;
        Refusing = refusing;
        _serving = ServeAsync();
    }

    public string Name { get; }

    /// <summary>Whether its pipe's ACL refuses this user.</summary>
    public bool Refusing { get; }

    /// <summary>The home it answers as.</summary>
    public string HomeId { get; set; }

    /// <summary>The hello's refusal code, or null to answer it.</summary>
    public string? RefuseHello { get; set; }

    /// <summary>The <c>why</c> a <c>stopping</c> refusal names.</summary>
    public string StoppingWhy { get; set; } = "close";

    public StandInPress Press { get; set; } = StandInPress.Answer;

    /// <summary>A press's answer.</summary>
    public object Data { get; set; } = new { done = true, message = "done" };

    /// <summary>An answer made from the route and its payload, where one press's answer differs from the next's; else <see cref="Data"/>.</summary>
    public Func<string, JsonElement, object?>? Respond { get; set; }

    /// <summary>A press's refusal code, and its sentence.</summary>
    public string Code { get; set; } = "confirm-in-window";

    /// <summary>A progress line told before the answer, or null for none.</summary>
    public string? Progress { get; set; }

    /// <summary>
    /// Whether a press is first told to wait for the person in the window (RPC1c), as the pipe tells a person's press, before it is
    /// answered or refused as <see cref="Press"/> says: how the window's Confirm, Refuse or two minutes reach the terminal.
    /// </summary>
    public bool Confirming { get; set; }

    /// <summary>With <see cref="Confirming"/>, why the person's standing confirmation did not cover the press (TERMTRUST1a), or null for none.</summary>
    public string? StandingWhy { get; set; }

    /// <summary>Whether a press is told it runs under the person's standing confirmation (TERMTRUST1a, <c>STANDING</c>) before it is answered.</summary>
    public bool Standing { get; set; }

    /// <summary>
    /// <c>RESULT</c>'s state for a press it was asked about: <c>answered</c> (with <see cref="Data"/>, or <see cref="ResultRefused"/>'s
    /// refusal), <c>running</c>, <c>waiting</c> (CONFIRMWAIT1a-i's), <c>unknown</c>, or <c>id-taken</c>, the refusal of another key's id.
    /// </summary>
    public string Result { get; set; } = "answered";

    /// <summary>With <see cref="Result"/> <c>answered</c>, the code of the refusal the press ended with, or null for <see cref="Data"/>.</summary>
    public string? ResultRefused { get; set; }

    /// <summary>
    /// <c>WITHDRAW</c>'s answer (CONFIRMWAIT1b, the confirm-wait design §4.3): <c>withdrawn</c>, <c>running</c>, <c>ended</c> (with
    /// <see cref="Data"/>, or <see cref="ResultRefused"/>'s refusal), <c>id-taken</c>, or <c>no-route</c>, as today's instance answers a
    /// route of the pipe it does not have.
    /// </summary>
    public string Withdraw { get; set; } = "withdrawn";

    /// <summary>The nonce its hello draws (SUPERVISOR1a-ii): a fake server's own, or one it relays from a real instance.</summary>
    public string Nonce { get; set; } = Daoris.Driver.SupervisorProof.Nonce();

    public int Connections => Volatile.Read(ref _connections);

    /// <summary>Every request it was sent, the hello and <c>RESULT</c> among them.</summary>
    public IReadOnlyList<JsonElement> Sent
    {
        get
        {
            lock (_sent) return [.. _sent];
        }
    }

    /// <summary>The presses it was sent: every request that is not the pipe's own.</summary>
    public IReadOnlyList<JsonElement> Presses => [.. Sent.Where(each => each.GetProperty("module").GetString() != "DAORIS.PIPE")];

    /// <summary>The <c>RESULT</c>s it was asked.</summary>
    public IReadOnlyList<JsonElement> Results => [.. Sent.Where(each => each.GetProperty("type").GetString() == "RESULT")];

    /// <summary>The <c>WITHDRAW</c>s it was asked.</summary>
    public IReadOnlyList<JsonElement> Withdraws => [.. Sent.Where(each => each.GetProperty("type").GetString() == "WITHDRAW")];

    private async Task ServeAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            var server = Refusing && OperatingSystem.IsWindows()
                ? NamedPipeServerStreamAcl.Create(
                    Name, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous, 0, 0, RefusingThisUser())
                : new NamedPipeServerStream(
                    Name, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await server.WaitForConnectionAsync(_stop.Token);
            }
            catch (OperationCanceledException)
            {
                await server.DisposeAsync();
                return;
            }

            Interlocked.Increment(ref _connections);
            lock (_open) _open.Add(server);
            _ = ConverseAsync(server);
        }
    }

    /// <summary>An ACL that lets this user read and write nothing, as a pipe another account serves.</summary>
    [SupportedOSPlatform("windows")]
    private static PipeSecurity RefusingThisUser()
    {
        using var self = WindowsIdentity.GetCurrent();
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(self.User!, PipeAccessRights.ReadWrite, AccessControlType.Deny));
        security.AddAccessRule(new PipeAccessRule(self.User!, PipeAccessRights.FullControl, AccessControlType.Allow));
        return security;
    }

    private async Task ConverseAsync(NamedPipeServerStream server)
    {
        try
        {
            using var reader = new StreamReader(server, Utf8, leaveOpen: true);
            while (await reader.ReadLineAsync(_stop.Token) is { } line)
            {
                var request = JsonDocument.Parse(line).RootElement.Clone();
                lock (_sent) _sent.Add(request);
                var id = request.GetProperty("id").GetString()!;
                var module = request.GetProperty("module").GetString();
                var type = request.GetProperty("type").GetString();
                if (module == "DAORIS.PIPE" && type == "HELLO")
                {
                    await WriteAsync(server, RefuseHello is { } code
                        ? Refusal(id, code, code == "stopping" ? new { why = StoppingWhy } : null)
                        : Success(id, new { protocol = 1, home = HomeId, version = "0", pid = Environment.ProcessId, nonce = Nonce }));
                    continue;
                }

                if (module == "DAORIS.PIPE" && type == "RESULT")
                {
                    var asked = request.GetProperty("payload").GetProperty("id").GetString()!;
                    if (Result == "id-taken")
                    {
                        await WriteAsync(server, Refusal(id, "id-taken", null));
                        continue;
                    }

                    await WriteAsync(server, Success(id, Result == "answered"
                        ? new { state = "answered", answer = JsonNode.Parse(Ending(asked)) }
                        : (object)new { state = Result }));
                    continue;
                }

                if (module == "DAORIS.PIPE" && type == "WITHDRAW")
                {
                    var asked = request.GetProperty("payload").GetProperty("id").GetString()!;
                    await WriteAsync(server, Withdraw switch
                    {
                        "id-taken" => Refusal(id, "id-taken", null),
                        "no-route" => Refusal(id, "NO_ROUTE", null),
                        "ended" => Success(id, new { state = "ended", answer = JsonNode.Parse(Ending(asked)) }),
                        _ => Success(id, new { state = Withdraw }),
                    });
                    continue;
                }

                if (Confirming)
                {
                    await WriteAsync(server, JsonSerializer.Serialize(new
                    {
                        category = "notification",
                        id = Guid.NewGuid().ToString("N"),
                        payload = new[]
                        {
                            new
                            {
                                module = "DAORIS.PIPE", type = "CONFIRMING",
                                payload = (object)(StandingWhy is { } why
                                    ? new { id, expires = DateTimeOffset.UtcNow.AddMinutes(2), standing = why }
                                    : new { id, expires = DateTimeOffset.UtcNow.AddMinutes(2) }),
                            },
                        },
                    }));
                }

                if (Standing)
                {
                    await WriteAsync(server, JsonSerializer.Serialize(new
                    {
                        category = "notification",
                        id = Guid.NewGuid().ToString("N"),
                        payload = new[] { new { module = "DAORIS.PIPE", type = "STANDING", payload = new { id } } },
                    }));
                }

                switch (Press)
                {
                    case StandInPress.Drop:
                        return;
                    case StandInPress.Hold:
                        continue;
                    case StandInPress.Refuse:
                        await WriteAsync(server, Refusal(id, Code, null));
                        continue;
                }

                if (Progress is { } told)
                {
                    await WriteAsync(server, JsonSerializer.Serialize(new
                    {
                        category = "notification",
                        id = Guid.NewGuid().ToString("N"),
                        payload = new[] { new { module = "SHENORA.REQUESTS", type = "REQUEST_UPDATED", payload = new { id, detail = new { text = told } } } },
                    }));
                }

                await WriteAsync(server, Success(id, Respond is { } respond && request.TryGetProperty("payload", out var sent)
                    ? respond(type!, sent)
                    : Data));
            }
        }
        catch (Exception error) when (error is IOException or OperationCanceledException or ObjectDisposedException)
        {
            // The terminal went, or the stand-in is ending.
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    /// <summary>How a press it was asked about ended: <see cref="Data"/>, or the refusal <see cref="ResultRefused"/> names.</summary>
    private string Ending(string asked) => ResultRefused is { } code ? Refusal(asked, code, null) : Success(asked, Data);

    /// <summary>
    /// The pattern of the line a verb named <paramref name="verb"/> says as its press waits for the window (CONFIRMWAIT1b, the
    /// confirm-wait design §4.1): the request's id, whatever it is, and the two verbs that read and take it back.
    /// </summary>
    public static string Waiting(string verb) =>
        System.Text.RegularExpressions.Regex.Escape(verb)
        + @": waiting for you to confirm this in Daoris's window \(request (?<id>[0-9a-f-]{36})\); `daoris-driver result \k<id>` says how it "
        + @"ended, and `daoris-driver withdraw \k<id>` takes it back…";

    /// <summary><see cref="Waiting"/> as the whole of what was said: that one line.</summary>
    public static string WaitingAlone(string verb) => @"\A" + Waiting(verb) + @"\r?\n\z";

    private static string Success(string id, object? data) =>
        JsonSerializer.Serialize(new { category = "ipc", id, success = true, data });

    private static string Refusal(string id, string code, object? parameters) =>
        JsonSerializer.Serialize(new { category = "ipc", id, success = false, error = new { code, message = $"refused {code}.", parameters } });

    private static async Task WriteAsync(Stream server, string line)
    {
        await server.WriteAsync(Utf8.GetBytes(line + "\n"));
        await server.FlushAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        lock (_open)
        {
            foreach (var server in _open) server.Dispose();
        }

        try
        {
            await _serving;
        }
        catch (Exception error) when (error is IOException or OperationCanceledException or ObjectDisposedException)
        {
            // Ending.
        }

        _stop.Dispose();
    }
}
