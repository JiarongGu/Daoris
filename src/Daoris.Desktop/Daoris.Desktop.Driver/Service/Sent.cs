using System.Text.Json;
using Daoris.Contracts;

namespace Daoris.Driver;

/// <summary>
/// A declared route's reply: the host's status, text and headers as <see cref="Reply"/>, and its answer where it was a success
/// that read as the declaration types it. A success that does not read is reported as the host's refusal, never thrown as
/// JSON's: a host of another build may answer a shape this one does not read.
/// </summary>
public sealed class Sent<T> where T : class
{
    private readonly string _url;
    private readonly string? _unread;

    private Sent(string url, ServiceReply reply, T? answer, string? unread) => (_url, Reply, Answer, _unread) = (url, reply, answer, unread);

    public ServiceReply Reply { get; }

    /// <summary>The answer; null for a refusal and for a success that did not read.</summary>
    public T? Answer { get; }

    public bool Ok => Answer is not null;

    /// <summary>What went wrong, said as a refused read says it: the address, the status and the host's own sentence.</summary>
    public string Refusal => Reply.Ok
        ? $"{_url} answered {Reply.Status} with what this driver does not read: {_unread}"
        : $"{_url} answered {Reply.Status}: {DriverHttp.ErrorOf(Reply.Payload)}";

    /// <summary>The answer, or the refusal thrown as a refused read's is.</summary>
    /// <exception cref="DriverException">A refusal, or a success that did not read.</exception>
    public T Answered() => Answer ?? throw new DriverException(Refusal);

    internal static Sent<T> Read(string url, ServiceReply reply)
    {
        if (!reply.Ok) return new(url, reply, null, null);
        try
        {
            var answer = (T?)JsonSerializer.Deserialize(reply.Payload, ContractJson.Options.GetTypeInfo(typeof(T)));
            return new(url, reply, answer, answer is null ? "null" : null);
        }
        catch (JsonException unread)
        {
            return new(url, reply, null, unread.Message);
        }
    }
}
