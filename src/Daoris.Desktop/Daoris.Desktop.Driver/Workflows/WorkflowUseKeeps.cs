using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>What clearing a default leaves to the kinds a place still maps: the driver's sentence, and each kind with its workflow.</summary>
public sealed record WorkflowUseKeeps(string Said, IReadOnlyList<KeyValuePair<string, string>> Kinds);
