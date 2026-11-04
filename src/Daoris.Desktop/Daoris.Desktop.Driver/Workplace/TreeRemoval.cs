namespace Daoris.Driver;

/// <param name="Removed">Whether the tree is gone. False is a refusal, not a failure.</param>
/// <param name="Message">What happened, or what would have been lost and how to mean it.</param>
public sealed record TreeRemoval(bool Removed, string Message);

