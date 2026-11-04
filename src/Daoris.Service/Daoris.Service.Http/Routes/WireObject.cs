using System.Text.Json.Nodes;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>A wire reader's JSON text as the object a route answers, so the wire a machine's client reads is the one written.</summary>
public static class WireObject
{
    public static JsonObject Of(string json) => JsonNode.Parse(json)!.AsObject();
}
