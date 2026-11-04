namespace Daoris.Knowledge;

/// <summary>What a finished tree held that no commit holds, by name (§2.1's last row): at most the walk's limit of names, the rest counted.</summary>
/// <param name="Count">Every such path, named or not.</param>
/// <param name="Cut">The names stop at the limit; <paramref name="Count"/> says how many there were.</param>
public sealed record WorkRecordLeft(IReadOnlyList<string> Names, int Count, bool Cut);
