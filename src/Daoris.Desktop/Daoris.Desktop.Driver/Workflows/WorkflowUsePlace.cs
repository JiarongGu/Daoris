using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>A repository whose workflow a choice changes: what it follows now and after, and what lowers the person's part there.</summary>
public sealed record WorkflowUsePlace(string Repository, string Was, string Now, IReadOnlyList<string> Lowers);
