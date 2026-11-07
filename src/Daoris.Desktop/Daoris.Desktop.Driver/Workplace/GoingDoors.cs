using System.Globalization;
using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeWorktrees;

namespace Daoris.Driver;

/// <summary>Which door removes a set-aside folder, as its <see cref="GoingRecord"/> says.</summary>
internal static class GoingDoors
{
    /// <summary>The hour after its record: its holding check runs again before a stopped delete finishes.</summary>
    public const string Hour = "hour";

    /// <summary>Its days.</summary>
    public const string Days = "days";

    /// <summary>The person's press.</summary>
    public const string Press = "press";
}
