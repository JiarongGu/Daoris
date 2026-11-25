using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The version of an environment at a tree: a digest of its declared entry and the content id each input has there. Pure; the
/// caller reads the ids from git: an input is a repository-relative pathspec (a path or a glob), and its content id is a digest of the
/// (path, blob id) pairs it matches at the tree, sorted by path, read at the line's tip, at a session's branch and at its fork. The same
/// content is the same version in any tree, so a session whose inputs equal the ones it forked with only uses the environment, and one
/// whose inputs differ changes it.
/// </summary>
public static class EnvironmentVersion
{
    /// <summary>How many hex digits a version keeps: enough that two live versions of one environment never meet.</summary>
    public const int Length = 12;

    /// <param name="spec">The environment as the tree's own manifest declares it.</param>
    /// <param name="inputIds">The content id of each input pathspec in the tree; a path with none, or an empty id, is absent there.</param>
    public static string Of(EnvironmentSpec spec, IReadOnlyDictionary<string, string> inputIds)
    {
        var text = new StringBuilder().Append("environment ").Append(spec.Id).Append('\n').Append("start ").Append(spec.Start).Append('\n');
        foreach (var port in spec.Ports.Order(StringComparer.Ordinal)) text.Append("port ").Append(port).Append('\n');
        foreach (var input in spec.Inputs.Order(StringComparer.Ordinal))
        {
            text.Append("input ").Append(input).Append(' ').Append(inputIds.TryGetValue(input, out var id) ? id : string.Empty).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..Length].ToLowerInvariant();
    }
}
