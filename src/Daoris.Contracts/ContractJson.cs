using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Daoris.Contracts.Knowledge;
using Daoris.Contracts.Quests;

namespace Daoris.Contracts;

/// <summary>
/// The wire's JSON: every domain's source-generated context combined, read with the web's defaults (camel-case names,
/// names matched ignoring case, numbers read from strings) and writing no null. The host puts <see cref="Resolver"/> first
/// in its own options, and the driver and the generator read with <see cref="Options"/>, so the three see one wire.
/// </summary>
public static class ContractJson
{
    /// <summary>Each domain's context, combined; a new domain's context is added here.</summary>
    public static IJsonTypeInfoResolver Resolver { get; } = JsonTypeInfoResolver.Combine(RefusalJson.Default, KnowledgeJson.Default, QuestJson.Default, QuestPersonJson.Default);

    /// <summary>The options a client reads and writes the wire with.</summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = Resolver,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
