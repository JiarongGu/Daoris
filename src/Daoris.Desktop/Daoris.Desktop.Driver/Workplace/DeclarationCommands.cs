using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// Whether a <c>daoris.gates.json</c> runs nothing the one accepted does not: the rule that keeps an acceptance across a change, and lets a
/// check run under the accepted declaration where a work's own change alters no command. Two declarations run the same where they are
/// equal once their inert parts are set aside: every member whose name begins with <c>_</c> (words, at any depth); each check's
/// <c>paths</c> (patterns), <c>timeoutMinutes</c>, <c>timeoutWhy</c> and <c>memoryGb</c> (budgets); and the top-level <c>tests</c> (test sets,
/// read by the repository's own tools inside an accepted command), <c>tiers</c> and <c>docs</c> (patterns). Everything else is what runs or
/// what grants: a check's name, command, folder, hand-back and flags, a check added, removed or moved, and every other part, one the rule
/// does not know included, waits for the person.
/// </summary>
public static class DeclarationCommands
{
    private static readonly HashSet<string> InertParts = new(StringComparer.Ordinal) { "tests", "tiers", "docs" };

    private static readonly HashSet<string> InertCheckParts = new(StringComparer.Ordinal) { "paths", "timeoutMinutes", "timeoutWhy", "memoryGb" };

    /// <summary>
    /// Whether <paramref name="now"/> runs only what <paramref name="accepted"/> runs: equal once both are read as JSON and their inert parts
    /// set aside. A text that does not read as a JSON object keeps only an identical text; nothing accepted keeps only nothing.
    /// </summary>
    public static bool Keeps(string? accepted, string? now)
    {
        if (string.IsNullOrWhiteSpace(accepted) || string.IsNullOrWhiteSpace(now)) return string.IsNullOrWhiteSpace(accepted) && string.IsNullOrWhiteSpace(now);
        if (Parse(accepted) is not { } was || Parse(now) is not { } then) return string.Equals(accepted, now, StringComparison.Ordinal);
        return JsonNode.DeepEquals(Runs(was), Runs(then));
    }

    /// <summary>The parts of a declaration that run or grant something: <paramref name="declaration"/> with its inert parts taken out.</summary>
    private static JsonObject Runs(JsonObject declaration)
    {
        Strip(declaration);
        foreach (var part in InertParts) declaration.Remove(part);
        if (declaration["gates"] is JsonArray checks)
        {
            foreach (var check in checks.OfType<JsonObject>())
            {
                foreach (var part in InertCheckParts) check.Remove(part);
            }
        }

        return declaration;
    }

    /// <summary>Every member named with a leading <c>_</c>, at any depth, taken out.</summary>
    private static void Strip(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject members:
                foreach (var name in members.Select(member => member.Key).Where(name => name.StartsWith('_')).ToList()) members.Remove(name);
                foreach (var member in members) Strip(member.Value);
                break;
            case JsonArray items:
                foreach (var item in items) Strip(item);
                break;
        }
    }

    private static JsonObject? Parse(string text)
    {
        try
        {
            return JsonNode.Parse(text, null, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
