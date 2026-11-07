namespace Daoris.Knowledge;

/// <summary>
/// A remote's answer to a person's retirement of a machine: whether it retired one (false where it had heard nothing
/// from it), and the handshake as it stands after it.
/// </summary>
public sealed record QuestRetirement(bool Retired, QuestClosure Closure);
