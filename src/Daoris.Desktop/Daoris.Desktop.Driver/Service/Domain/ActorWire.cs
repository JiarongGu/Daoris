using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// How an actor's record fields travel to the service and back (<c>asked</c>, <c>ran</c> and <c>override</c>, D205 §9.3): the one set of
/// serializer options every reader and writer of them uses, the web's defaults. The service's client reads and writes them through it;
/// <see cref="ActorRun.Json"/> is the same options, for the run's own reads.
/// </summary>
public static class ActorWire
{
    /// <summary>The options the record's actor fields are serialized by.</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
}
