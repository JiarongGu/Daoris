using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>Starts a role took on one choice: <c>1</c> to <c>4</c>, <c>before</c> or <c>rule-default</c> (<see cref="ActorEntry"/>'s position).</summary>
public sealed record RoleUseChoice(string Choice, int Starts);

/// <summary>How many starts passed over a choice for one code of the walk (<c>cooling</c>, <c>signed-out</c>, ...).</summary>
public sealed record RoleUseWhy(string Code, int Starts);

/// <summary>One role's week: its starts, where they landed, how many fell to a later choice and the codes that moved them.</summary>
public sealed record RoleUseRow(string Role, int Starts, IReadOnlyList<RoleUseChoice> Choices, int Fell, IReadOnlyList<RoleUseWhy> Why);

/// <summary>The weeks' rows, and how many days of the machine log they were read over (at most <see cref="RoleUse.Week"/>; none with no log).</summary>
public sealed record RoleUseRead(int Days, IReadOnlyList<RoleUseRow> Roles);

/// <summary>
/// Each role's use over the last seven days (MODELROLE1f, D205), summed from the machine log's <c>actor.chose</c> lines, which a counted
/// start writes with the choice it took and the passes before it as <c>choice:agent:code</c>. Names and counts only. A role with no start in the
/// week has no row: absent is never zero.
/// </summary>
public static class RoleUse
{
    /// <summary>The days read.</summary>
    public const int Week = 7;

    public static RoleUseRead Of(string home, DateTimeOffset now)
    {
        string folder;
        try
        {
            folder = DaorisHome.Entry(home, HomeEntry.Logs);
        }
        catch (Exception error) when (error is HomeEntryTwiceException or IOException or UnauthorizedAccessException)
        {
            return new(0, []);
        }

        var lines = MachineLogReader.Read(folder, new LogFilter(Since: now.AddDays(-Week), Event: "actor.chose")).Lines;
        var days = DaysKept(folder, now);
        var rows = lines
            .Where(line => line.Data.ValueKind == JsonValueKind.Object && Text(line.Data, "actor") is not null)
            .GroupBy(line => Text(line.Data, "actor")!, StringComparer.Ordinal)
            .Select(group => RowOf(group.Key, group.Select(line => (Text(line.Data, "choice") ?? "", Passed(line.Data))).ToList()))
            .OrderBy(row => row.Role, StringComparer.Ordinal)
            .ToList();
        return new(days, rows);
    }

    private static RoleUseRow RowOf(string role, List<(string Choice, List<string> Passed)> starts) => new(
        role, starts.Count,
        [.. starts.Where(start => start.Choice.Length > 0).GroupBy(start => start.Choice, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal).Select(group => new RoleUseChoice(group.Key, group.Count()))],
        starts.Count(start => start.Passed.Count > 0),
        [.. starts.SelectMany(start => start.Passed.Select(CodeOf).Where(code => code.Length > 0).Distinct(StringComparer.Ordinal))
            .GroupBy(code => code, StringComparer.Ordinal).OrderByDescending(group => group.Count()).ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new RoleUseWhy(group.Key, group.Count()))]);

    /// <summary>The code of a pass word <c>choice:agent:code</c>: after its second colon.</summary>
    private static string CodeOf(string word)
    {
        var first = word.IndexOf(':', StringComparison.Ordinal);
        var second = first < 0 ? -1 : word.IndexOf(':', first + 1);
        return second < 0 ? "" : word[(second + 1)..];
    }

    private static string? Text(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;

    private static List<string> Passed(JsonElement data) =>
        data.TryGetProperty("passed", out var value) && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!)]
            : [];

    /// <summary>The days the log holds up to today, at most a week: its oldest file's day to now, so a young log says its span.</summary>
    private static int DaysKept(string folder, DateTimeOffset now)
    {
        try
        {
            if (!Directory.Exists(folder)) return 0;
            var oldest = Directory.EnumerateFiles(folder, "*.jsonl")
                .Select(path => Path.GetFileName(path))
                .Where(name => name.Length > 10 && DateTime.TryParseExact(name[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                .Select(name => DateTime.ParseExact(name[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture))
                .DefaultIfEmpty(DateTime.MaxValue).Min();
            return oldest == DateTime.MaxValue ? 0 : Math.Clamp((int)(now.UtcDateTime.Date - oldest).TotalDays + 1, 0, Week);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
