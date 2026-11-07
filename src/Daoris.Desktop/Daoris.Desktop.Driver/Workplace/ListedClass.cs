using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The class a set-aside folder is listed by: its <see cref="SetAsideClass"/>, but a stopped or refused
/// folder whose check said its branch stands is <see cref="Stands"/>, and a finished folder unmarked by a failed check is <see cref="Unheld"/>.
/// </summary>
internal static class ListedClass
{
    /// <summary>Stopped or refused, and the last check said its branch stands.</summary>
    public const string Stands = "stands";

    /// <summary>A finished tree's folder whose holding check failed: what holds its commits could not be confirmed.</summary>
    public const string Unheld = "unheld";

    internal static string Of(SetAsideListed folder) => Of(folder.Class, folder.Why, folder.Finished);

    /// <summary>A class as it is listed, from the folder's class, its manifest's why and whether it is a finished tree's.</summary>
    internal static string Of(string cls, string? why, bool finished) => cls switch
    {
        SetAsideClass.Stopped or SetAsideClass.Refused when why == SetAsideEligibility.BranchStands => Stands,
        SetAsideClass.Unmarked when finished => Unheld,
        var other => other,
    };
}
