using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// The run's vacancies as the pass judged them (<see cref="StepVacancies"/>), <c>workflows/vacancies/&lt;key&gt;.json</c>: written by the pass
/// that holds the lease, read by every row, view and list that says a step is vacant. A file that does not read holds none.
/// </summary>
public sealed class StepVacancyFiles(string home) : IStepVacancyStore
{
    public static string FolderOf(string home) => DaorisHome.Entry(home, HomeEntry.Workflows, "vacancies");

    public static string PathOf(string home, string unit) => Path.Combine(FolderOf(home), NodeRecordFiles.KeyOf(unit) + ".json");

    public IReadOnlyList<StepVacancy> Of(string unit)
    {
        var path = PathOf(home, unit);
        if (!File.Exists(path)) return [];
        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object || Text(document.RootElement, "unit") != unit
                || !document.RootElement.TryGetProperty("vacancies", out var all) || all.ValueKind != JsonValueKind.Array) return [];
            var read = new List<StepVacancy>();
            foreach (var each in all.EnumerateArray())
            {
                if (each.ValueKind == JsonValueKind.Object && Text(each, "node") is { } node && each.TryGetProperty("attempt", out var number)
                    && number.TryGetInt32(out var attempt) && Moments.Read(each, "since") is { } since)
                {
                    read.Add(new StepVacancy(node, attempt, since, Flag(each, "told"), Flag(each, "refused")));
                }
            }

            return read;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return [];
        }
    }

    public IReadOnlyList<string> Units()
    {
        if (!Directory.Exists(FolderOf(home))) return [];
        var units = new List<string>();
        try
        {
            foreach (var path in Directory.GetFiles(FolderOf(home), "*.json"))
            {
                try
                {
                    using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
                    if (document.RootElement.ValueKind == JsonValueKind.Object && Text(document.RootElement, "unit") is { Length: > 0 } unit && PathOf(home, unit) == path) units.Add(unit);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
                {
                    // A file that does not read names no run; the next look reads it again.
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return units;
        }

        return units;
    }

    /// <summary>The vacancy of <paramref name="node"/>'s attempt <paramref name="attempt"/>, or null where the pass judged none.</summary>
    public StepVacancy? Current(string unit, string node, int attempt) => Of(unit).FirstOrDefault(each => each.Node == node && each.Attempt == attempt);

    public void Keep(string unit, IReadOnlyList<StepVacancy> vacancies)
    {
        var path = PathOf(home, unit);
        if (vacancies.Count == 0)
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }

        var written = new JsonObject
        {
            ["unit"] = unit,
            ["vacancies"] = new JsonArray([.. vacancies.Select(each => (JsonNode)new JsonObject
            {
                ["node"] = each.Node, ["attempt"] = each.Attempt, ["since"] = Moments.Write(each.Since), ["told"] = each.Told, ["refused"] = each.Refused,
            })]),
        };
        Directory.CreateDirectory(FolderOf(home));
        AtomicFile.WriteText(path, written.ToJsonString() + "\n");
        HomeWrites.Mine(path);
    }

    private static bool Flag(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
