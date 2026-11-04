using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// One step as a version reads it: its id and kind, and every field its kind takes, in order, absence read as its default. A
/// value is text, null, a switch, a list of names, or a group's members.
/// </summary>
public sealed record NamedStep(string Id, string Kind, IReadOnlyList<WorkflowSetting> Fields)
{
    /// <summary>A field's value as the step reads it.</summary>
    public object? Field(string name) => Fields.FirstOrDefault(each => each.Name == name)?.Value;

    /// <summary>A group's members, or none.</summary>
    public IReadOnlyList<NamedStep> Members => Kind == WorkflowKinds.All && Field("steps") is IReadOnlyList<NamedStep> members ? members : [];
}
