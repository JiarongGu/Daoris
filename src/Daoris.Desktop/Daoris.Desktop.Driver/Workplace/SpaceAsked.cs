using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>One unit a press asked removed, as the page or the terminal sends it back from the list.</summary>
public sealed record SpaceAsked(string Kind, string Id, bool Early, string? Why, bool Own)
{
    /// <summary>
    /// A set-aside folder's class as the list gave it (<see cref="ListedClass"/>), and the ref it named the folder's tip on: the
    /// run removes it only while both still read so, else keeps it <c>changed</c>. Null from a list
    /// that gave none, an older page's.
    /// </summary>
    public string? Class { get; init; }

    /// <inheritdoc cref="Class"/>
    public string? Ref { get; init; }
}
