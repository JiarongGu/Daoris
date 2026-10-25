namespace Daoris.Driver;

/// <summary>
/// The machine log's landing line (LAND2b, design §8, D94): <c>landing.auto</c>, one per try, with codes and counts and the
/// plugin's id, never a sentence, a branch or a path.
/// </summary>
public sealed record LandingLine(string Event, IReadOnlyList<(string Key, object? Value)> Data);
