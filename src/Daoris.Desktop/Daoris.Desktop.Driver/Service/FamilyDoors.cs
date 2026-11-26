namespace Daoris.Driver;

/// <summary>The two doors a look's fan-out publishes through: a request's quest, and the words of one it published moved.</summary>
public interface IFamilyDoors : IRequestPort
{
    /// <exception cref="HttpRequestException">The host did not answer: the same words are sent at the next look.</exception>
    Task<(bool Ok, string Message)> ReviseWordsAsync(string id, string title, string body, CancellationToken ct);
}
