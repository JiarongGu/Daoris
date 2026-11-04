using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>One place a saved workflow is chosen for, which appending a version to it reaches at the next start (<c>WorkflowRunBindings.Plan</c> binds the newest).</summary>
/// <param name="Level"><c>repository</c> or <c>workspace</c>.</param>
/// <param name="Role"><c>default</c>, <c>kind</c> or <c>landed</c>.</param>
public sealed record WorkflowScope(string Level, string Name, string Role, string? Kind)
{
    /// <summary>The scope as the person authorizes it.</summary>
    public string Key => Kind is null ? $"{Level}:{Name}:{Role}" : $"{Level}:{Name}:{Role}:{Kind}";
}
