using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.ContractsGen;

/// <summary>
/// The provider censuses' one table, written from the driver's registrations (<see cref="ProviderVocabulary"/>) so the service's, the
/// page's and the tools' censuses read the ids and words the providers declare, and no list is kept by hand. Written as one JSON object,
/// so a census in C# or in plain JavaScript reads it with no TypeScript.
/// </summary>
public static class PageProviderVocabulary
{
    /// <summary>The block appended to the service types' file.</summary>
    public static string Write()
    {
        var ids = ProviderVocabulary.Ids.Select(row => JsonSerializer.Serialize(new[] { row.Id, row.Owner }));
        var words = ProviderVocabulary.Words.Select(row => JsonSerializer.Serialize(new[] { row.Word, row.Owner, row.Kind }));

        var text = new StringBuilder();
        text.Append("\n/** The ids an agent or a door goes by, each with the provider that owns it, and the variables, files and wire keys each provider ");
        text.Append("declares (`variable`, `file`, `wire`): the provider censuses' one table, read off the driver's registrations. */\n");
        text.Append("export type ProviderVocabulary = { readonly ids: readonly (readonly [id: string, owner: string])[]; ");
        text.Append("readonly words: readonly (readonly [word: string, owner: string, kind: string])[] };\n");
        // One row a line, and the object's end a line holding `};` alone, so a census reads the block as JSON.
        text.Append("export const PROVIDER_VOCABULARY: ProviderVocabulary = {\n");
        text.Append("  \"ids\": [\n").Append(string.Join(",\n", ids.Select(row => $"    {row}"))).Append("\n  ],\n");
        text.Append("  \"words\": [\n").Append(string.Join(",\n", words.Select(row => $"    {row}"))).Append("\n  ]\n");
        text.Append("};\n");
        return text.ToString();
    }
}
