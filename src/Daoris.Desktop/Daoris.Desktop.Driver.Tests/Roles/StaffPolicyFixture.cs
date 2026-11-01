using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// STAFFPOLICY1a's one table, <c>fixtures/staff-policy.json</c>: a policy and its facts in, the arrangement, the picks, the action and the
/// judge's answer out. A policy's choice written as <c>{ "choice": n }</c> is choice n of the table's <c>choices</c>.
/// </summary>
internal static class StaffPolicyFixture
{
    private static readonly Lazy<JsonObject> Table = new(() => JsonNode.Parse(File.ReadAllText(
        Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "staff-policy.json")))!.AsObject());

    public static IReadOnlyList<ActorChoice> Choices { get; } = [.. Table.Value["choices"]!.AsArray().Select(each => new ActorChoice(
        each![0]!.GetValue<string>(), each[1]?.GetValue<string>(), each[2]?.GetValue<string>()))];

    public static DateTimeOffset Now => DateTimeOffset.Parse(Table.Value["now"]!.GetValue<string>(), CultureInfo.InvariantCulture);

    /// <summary>The rows of a section, each by its name, for a theory.</summary>
    public static TheoryData<string> Names(string section)
    {
        var data = new TheoryData<string>();
        foreach (var row in Table.Value[section]!.AsArray()) data.Add(row!["name"]!.GetValue<string>());
        return data;
    }

    public static JsonObject Row(string section, string name) =>
        Table.Value[section]!.AsArray().Single(row => row!["name"]!.GetValue<string>() == name)!.AsObject();

    /// <summary>Choice n of the table as a key.</summary>
    public static StaffKey Key(int choice) => StaffKey.Of(Choices[choice - 1]);

    /// <summary>The row's policy with each <c>{ "choice": n }</c> written out as its tuple, or null for none.</summary>
    public static StaffPolicy? Policy(JsonObject row)
    {
        if (row["policy"] is not JsonObject policy) return null;
        var written = policy.DeepClone().AsObject();
        foreach (var entry in written["choices"]!.AsArray())
        {
            var each = entry!.AsObject();
            if (each["choice"] is not { } number) continue;
            var choice = Choices[number.GetValue<int>() - 1];
            each.Remove("choice");
            each["agent"] = choice.Agent;
            if (choice.Model is { } model) each["model"] = model;
            if (choice.Effort is { } effort) each["effort"] = effort;
        }

        var (read, refused) = StaffPolicy.Parse(written.ToJsonString());
        Assert.True(refused is null, $"{row["name"]}: {refused?.Said}");
        return read;
    }

    /// <summary>The row's order: its own <c>order</c> pairs, else choices 1 to <c>choices</c>, numbered.</summary>
    public static IReadOnlyList<ActorEffective> Order(JsonObject row)
    {
        if (row["order"] is JsonArray pairs)
        {
            return [.. pairs.Select(pair => Entry(pair![0]!.GetValue<int>(), pair[1]!.GetValue<string>()))];
        }

        return [.. Enumerable.Range(1, row["choices"]!.GetValue<int>()).Select(n => Entry(n, n.ToString(CultureInfo.InvariantCulture)))];
    }

    private static ActorEffective Entry(int choice, string from) => new(Choices[choice - 1].Agent, Choices[choice - 1].Model, Choices[choice - 1].Effort, from);

    /// <summary>The row's facts at the table's now.</summary>
    public static StaffFacts Facts(JsonObject? facts)
    {
        var now = Now;
        facts ??= [];
        return new StaffFacts
        {
            Now = now,
            Window = facts["window"] is JsonArray window ? [.. window.Select(each => Key(each!.GetValue<int>()))] : [],
            Reserved = Counts(facts["reserved"]),
            Today = Counts(facts["today"]),
            Tokens = facts["tokens"] is JsonObject tokens
                ? tokens.ToDictionary(each => Key(int.Parse(each.Key, CultureInfo.InvariantCulture)), each => new StaffTokens(each.Value![0]!.GetValue<long>(), each.Value[1]!.GetValue<bool>()))
                : new Dictionary<StaffKey, StaffTokens>(),
            NotAnswering = facts["notAnswering"] is JsonObject marks
                ? marks.ToDictionary(each => each.Key, each => now.AddMinutes(each.Value!.GetValue<int>()), StringComparer.Ordinal)
                : new Dictionary<string, DateTimeOffset>(),
            MovedOff = facts["movedOff"] is JsonObject moved
                ? moved.ToDictionary(each => Key(int.Parse(each.Key, CultureInfo.InvariantCulture)), each => now.AddMinutes(each.Value!.GetValue<int>()))
                : new Dictionary<StaffKey, DateTimeOffset>(),
        };
    }

    private static IReadOnlyDictionary<StaffKey, int> Counts(JsonNode? node) =>
        node is JsonObject counts
            ? counts.ToDictionary(each => Key(int.Parse(each.Key, CultureInfo.InvariantCulture)), each => each.Value!.GetValue<int>())
            : new Dictionary<StaffKey, int>();

    public static string? Text(JsonNode? node) => node?.GetValueKind() == JsonValueKind.String ? node.GetValue<string>() : null;

    public static int? Int(JsonNode? node) => node?.GetValueKind() == JsonValueKind.Number ? node.GetValue<int>() : null;
}
