namespace Daoris.Driver;

/// <summary>
/// A resume reason as the notes read it: its code, supplied sentence and ordered facts. Agents owns the
/// rule that produces a reason; Events reads only these values, without judging whether the work may continue, waits or is lost.
/// </summary>
public interface IContinueReason
{
    string Code { get; }
    string Sentence { get; }
    IReadOnlyList<(string Name, string Value)> Values { get; }
}
