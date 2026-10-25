namespace Daoris.Driver;

/// <summary>
/// A running driven session's permission prompts changed under it (ACCESSSWAP1b, D183's note): what it started under and what its
/// repository's setup names now, each a permission prompts value. Its run ends at that turn's end, its held words handed back.
/// </summary>
public sealed record SetupSwap(string From, string To);
