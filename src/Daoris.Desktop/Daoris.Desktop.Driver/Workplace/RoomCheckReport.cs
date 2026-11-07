using System.Text;

namespace Daoris.Driver;

/// <summary>
/// What the room check found (<see cref="RoomCheck"/>): the rooms in the order declared, its claim groups (rooms whose build closures
/// meet, one session in place per group), every reference across rooms, what is said to be in no room, the repository's own rules
/// for what git does not manage, and why the declaration does not pass.
/// </summary>
internal sealed record RoomCheckReport(
    IReadOnlyList<string> Rooms, IReadOnlyList<IReadOnlyList<string>> Groups, IReadOnlyList<RoomCrossing> Crossings, IReadOnlyList<string> InNone,
    IReadOnlyList<FileRule> Rules, IReadOnlyList<string> Problems)
{
    internal bool Passes => Problems.Count == 0;

    /// <summary>The report in sentences: the groups first, then what lies in no room, the crossings, the classes and the problems.</summary>
    internal string Said()
    {
        var said = new StringBuilder();
        if (Rooms.Count == 0)
        {
            said.Append("The lanes file declares no rooms: every session works on the fallback.\n");
        }
        else
        {
            said.Append(Count(Rooms.Count, "room")).Append(", in ").Append(Count(Groups.Count, "claim group")).Append(": ")
                .Append(string.Join("; ", Groups.Select(group => string.Join(", ", group)))).Append(".\n");
            if (InNone.Count > 0) said.Append("In no room: ").Append(string.Join("; ", InNone)).Append(".\n");
            if (Crossings.Count > 0)
            {
                said.Append("Where builds cross rooms:\n");
                foreach (var crossing in Crossings) said.Append("  ").Append(crossing.Said).Append('\n');
            }
        }

        said.Append(Rules.Count > 0
            ? $"The repository's rules for what git does not manage: {string.Join(", ", Rules.GroupBy(rule => rule.Kind).Select(group => $"{group.Count()} {group.Key.Word()}"))}.\n"
            : "The repository states no rule for what git does not manage.\n");
        if (Problems.Count > 0)
        {
            said.Append("The declaration does not pass:\n");
            foreach (var problem in Problems) said.Append("  ").Append(problem).Append('\n');
        }

        return said.ToString();
    }

    private static string Count(int count, string noun) => $"{count} {noun}{(count == 1 ? "" : "s")}";
}

/// <summary>A room's build reading or writing a path another room holds or writes, or one in no room (<see cref="NoRoom"/>).</summary>
internal sealed record RoomCrossing(string Room, string Verb, string Path, string Other)
{
    internal const string NoRoom = "";

    internal string Said => $"{Room} {Verb} {Path}, {(Other == NoRoom ? "in no room" : $"{Other}'s")}";
}
