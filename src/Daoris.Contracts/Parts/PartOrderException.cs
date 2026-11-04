namespace Daoris.Contracts.Parts;

/// <summary>A root's parts that cannot be ordered or built, refused at its start rather than at the look or request that would run them.</summary>
public sealed class PartOrderException(string message, Exception? inner = null) : InvalidOperationException(message, inner);
