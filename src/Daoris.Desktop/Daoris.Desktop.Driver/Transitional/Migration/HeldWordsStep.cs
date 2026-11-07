using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The held words in the shape this build reads: a press held without its question names its choice and no
/// question, so the head cannot check it is still the question asked; it is taken out, the person's press kept in the copy.
/// </summary>
public sealed class HeldWordsStep : IHomeMigrationStep
{
    public string Name => "held-words";

    public void Run(HomeMigrationRun run) => run.ChangeJson(HeldWordsFile.PathOf(run.Home), root =>
    {
        if (root["held"] is not JsonArray held) return false;
        var unasked = held.OfType<JsonObject>().Where(word => Text(word, "choice") is not null && Text(word, "question") is null).ToList();
        foreach (var word in unasked) held.Remove(word);
        return unasked.Count > 0;
    });

    private static string? Text(JsonObject word, string name) =>
        word[name] is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 ? text : null;
}
