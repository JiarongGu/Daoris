namespace Daoris.Driver;

/// <summary>One edit of a unit's record that says what it did.</summary>
internal static class NodeRecordEdits
{
    /// <summary>One edit of a unit's record that says what it did; an edit refused (no lease, a record that does not read) says why.</summary>
    internal static WorkPerformed Edited(this NodeRecordFiles records, string unit, string node, Func<NodeRecord, (NodeRecord? Next, WorkPerformed Result)> change)
    {
        var (refused, result) = records.Edit(unit, change);
        return refused is null ? result! : new(unit, node, PerformedWhat.Refused, refused);
    }
}
