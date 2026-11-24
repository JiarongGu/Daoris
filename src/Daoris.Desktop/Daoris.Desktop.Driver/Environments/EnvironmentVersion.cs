using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The version of an environment at a tree: a digest of its declared entry and the content id each input has there. Pure; the
/// caller reads the ids from git (one tree or blob id per declared input path, at the line's tip or at a session's branch). The same content
/// is the same version in any tree, so a session whose inputs equal the line's only uses the environment, and one whose inputs differ
/// changes it.
/// </summary>
public static class EnvironmentVersion
{
    /// <summary>How many hex digits a version keeps: enough that two live versions of one environment never meet.</summary>
    public const int Length = 12;

    /// <param name="spec">The environment as the tree's own manifest declares it.</param>
    /// <param name="inputIds">The content id of each input path in the tree; a path with none, or an empty id, is absent there.</param>
    public static string Of(EnvironmentSpec spec, IReadOnlyDictionary<string, string> inputIds)
    {
        var text = new StringBuilder().Append("environment ").Append(spec.Id).Append('\n').Append("start ").Append(spec.Start).Append('\n');
        foreach (var input in spec.Inputs.Order(StringComparer.Ordinal))
        {
            text.Append("input ").Append(input).Append(' ').Append(inputIds.TryGetValue(input, out var id) ? id : string.Empty).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..Length].ToLowerInvariant();
    }
}
