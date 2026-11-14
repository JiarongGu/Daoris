namespace Daoris.Ports.Repositories;

/// <summary>
/// A read that tells a git which found nothing from one that did not answer (a timeout, a start that failed): a reader that treats
/// nothing as a fact, such as a branch gone, must not read a silence as one.
/// </summary>
/// <param name="Value">What git named; null where it named none.</param>
/// <param name="Unanswered">Whether git gave no answer at all, so that <paramref name="Value"/> says nothing.</param>
public readonly record struct GitFound(string? Value, bool Unanswered = false);
