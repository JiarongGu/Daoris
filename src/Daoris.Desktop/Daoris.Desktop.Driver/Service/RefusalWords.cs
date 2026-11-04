using System.Text.Json;
using Daoris.Contracts;
using Daoris.Contracts.Sessions;

namespace Daoris.Driver;

/// <summary>The words a refused reply carries, read as the contracts' refusal records spell them.</summary>
internal static class RefusalWords
{
    /// <summary>The host's own sentence of a refusal; null where the text is no refusal, a host older than the door answering a bare 404 or 405.</summary>
    internal static string? ErrorWords(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize(payload, ContractJson.Options.GetTypeInfo(typeof(ErrorResponse))) is ErrorResponse refusal ? refusal.Error : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The service's own sentence, or a bare status when it did not compose one.</summary>
    internal static string ServiceSaid(ServiceReply reply) => ErrorWords(reply.Payload) ?? $"the service answered {reply.Status}";

    /// <summary>
    /// A reply's text read as a session door's refusal (its sentence, its word and the facts the word names); null where the text is no JSON,
    /// which a host older than the door answers.
    /// </summary>
    internal static SessionSayRefusalResponse? Refused(ServiceReply reply)
    {
        try
        {
            return JsonSerializer.Deserialize(reply.Payload, ContractJson.Options.GetTypeInfo(typeof(SessionSayRefusalResponse))) as SessionSayRefusalResponse;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
