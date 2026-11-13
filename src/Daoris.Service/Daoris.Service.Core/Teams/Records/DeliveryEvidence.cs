namespace Daoris.Knowledge;

/// <summary>The place an edge's reason stands on: a file's range in a repository with its blob id, or a provider's stage condition (the definition as <c>File</c>, its revision as <c>Blob</c>).</summary>
/// <param name="Repository">The repository the file or the definition belongs to.</param>
/// <param name="File">The repository-relative path, or the provider's definition and stage.</param>
/// <param name="Range">The lines read, as <c>12-40</c>; blank for the whole or for a stage.</param>
/// <param name="Blob">The git blob id, or the definition's revision, as the lead read it: the edge is stale once the same place holds another.</param>
public sealed record DeliveryEvidence(string Repository, string File, string Range, string Blob)
{
    /// <summary>What a reader of the current blobs looks the evidence up by.</summary>
    public string Key => $"{Repository}:{File}";
}
