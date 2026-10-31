namespace Daoris.Driver;

/// <summary>What a request node publishes: the quest's key and words, and who asks whom.</summary>
public sealed record RequestAsk(string Key, string From, string To, string Title, string Body);

/// <summary>The exchange's answer to a published request: the quest made (or held already under the key), or the refusal's code and words.</summary>
public sealed record RequestPublished(bool Ok, string? Code, string Message, QuestView? Quest);

/// <summary>The one door a request node publishes through, as a port so a node is judged over a stand-in. <see cref="ServiceClient"/> is the host's.</summary>
public interface IRequestPort
{
    /// <exception cref="HttpRequestException">The host did not answer: the publish is unknown, and the same key is sent again.</exception>
    Task<RequestPublished> PublishAsync(RequestAsk ask, CancellationToken ct);
}
