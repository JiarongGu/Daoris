using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// One record's lineage lines in Codex's words (row l): a connector call names its thread, the thread's parent and the root. A thread named
/// with no parent is the root, else the root its metadata names; a call that names a field that is no id is unknown.
/// </summary>
internal sealed class CodexChildLines : IChildLines
{
    private readonly HashSet<string> _roots = new(StringComparer.Ordinal);
    private string? _named;

    public string? Root => _roots.FirstOrDefault() ?? _named;

    public IReadOnlyCollection<string> Roots => Root is { } root ? [.. _roots, root] : _roots;

    public void Learn(JsonObject line)
    {
        if (LineageText.Text(line, "by") != "call") return;
        if (LineageText.Id(line, "thread") is { } thread && LineageText.Id(line, "parent") is null && !LineageText.Malformed(line)) _roots.Add(thread);
        _named ??= LineageText.Id(line, "root");
    }

    public ChildCall Call(JsonObject line)
    {
        if (LineageText.Id(line, "thread") is not { } thread || LineageText.Malformed(line)) return ChildCall.Unknown;
        return _roots.Contains(thread) || thread == Root ? ChildCall.Root : ChildCall.Of(thread);
    }

    /// <summary>A call names its thread; one named with no parent is a root, which moves a reading only where it holds that child.</summary>
    public static ChildCall? Wake(JsonObject line)
    {
        if (LineageText.Text(line, "by") != "call") return null;
        if (LineageText.Malformed(line) || LineageText.Id(line, "thread") is not { } thread) return ChildCall.None;
        return LineageText.Id(line, "parent") is null ? new ChildCall(ChildCallKind.Root, thread) : ChildCall.Of(thread);
    }
}
