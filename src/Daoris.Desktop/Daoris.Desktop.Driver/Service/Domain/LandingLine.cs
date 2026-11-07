namespace Daoris.Driver;

/// <summary>
/// The machine log's landing line: <c>landing.auto</c>, one per try, with codes and counts,
/// never a sentence, a branch or a path.
/// </summary>
public sealed record LandingLine(string Event, IReadOnlyList<(string Key, object? Value)> Data);
