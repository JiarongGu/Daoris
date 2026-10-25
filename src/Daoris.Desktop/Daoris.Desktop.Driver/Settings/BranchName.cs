namespace Daoris.Driver;

/// <summary>
/// A branch name the line may be set to (WSR2): git's own rules for a ref name, the ones a person could
/// break by typing.
/// </summary>
public static class BranchName
{
    public static bool IsValid(string name) =>
        name.Length is > 0 and <= 200
        && name != "@"
        && !name.StartsWith('-') && !name.StartsWith('/') && !name.EndsWith('/') && !name.EndsWith('.')
        && !name.Contains("..", StringComparison.Ordinal)
        && !name.Contains("//", StringComparison.Ordinal)
        && !name.Contains("@{", StringComparison.Ordinal)
        && !name.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c is '~' or '^' or ':' or '?' or '*' or '[' or '\\')
        && name.Split('/').All(part => !part.StartsWith('.') && !part.EndsWith(".lock", StringComparison.Ordinal));
}
