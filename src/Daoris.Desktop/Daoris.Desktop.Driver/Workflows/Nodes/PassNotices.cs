namespace Daoris.Driver;

/// <summary>The news one pass gives (<see cref="NodeRuntimePass.Noticed"/>): begun empty at each pass, which run one at a time.</summary>
internal sealed class PassNotices
{
    private readonly List<LookLine> _lines = [];

    public static implicit operator List<LookLine>(PassNotices notices) => notices._lines;

    public void Begin() => _lines.Clear();

    public IReadOnlyList<LookLine> Handed() => [.. _lines];
}
